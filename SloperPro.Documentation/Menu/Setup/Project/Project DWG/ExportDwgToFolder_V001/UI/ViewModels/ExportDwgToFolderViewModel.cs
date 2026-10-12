using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.ExportDwgToFolder.V001.Core.Models;
using Revit26_Plugin.ExportDwgToFolder.V001.Infrastructure.ExternalEvents;
using Revit26_Plugin.Shared.Models;
using Revit26_Plugin.Shared.Services;

namespace Revit26_Plugin.ExportDwgToFolder.V001.UI.ViewModels
{
    public partial class ExportDwgToFolderViewModel : ObservableObject
    {
        private const string ToolName = "ExportDwgToFolder";

        private readonly DwgExportEventHandler _handler;
        private readonly ExternalEvent _externalEvent;
        private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
        private readonly ExportDwgSettings _settings;

        // ── Metrics ──────────────────────────────────────────────────────────
        public ObservableCollection<SummaryCard> SummaryCards { get; } = new ObservableCollection<SummaryCard>();

        // ── Output folder ────────────────────────────────────────────────────
        [ObservableProperty] private string outputFolder = string.Empty;

        // ── Options ──────────────────────────────────────────────────────────
        [ObservableProperty] private bool exportLinked;
        [ObservableProperty] private bool exportImported;
        [ObservableProperty] private bool subfoldersByViewType;
        [ObservableProperty] private bool includePlan;
        [ObservableProperty] private bool includeSection;
        [ObservableProperty] private bool includeDrafting;
        [ObservableProperty] private bool includeNoViews;
        [ObservableProperty] private bool generateSidecar;
        [ObservableProperty] private bool overwrite;

        // ── State ────────────────────────────────────────────────────────────
        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private double progress;
        [ObservableProperty] private string statusMessage = "Opening…";

        // ── Log ──────────────────────────────────────────────────────────────
        public ObservableCollection<LogEntry> LogEntries { get; } = new ObservableCollection<LogEntry>();

        public ExportDwgToFolderViewModel(DwgExportEventHandler handler, ExternalEvent externalEvent)
        {
            _handler = handler;
            _externalEvent = externalEvent;

            // Load settings into backing fields (not properties) so On*Changed
            // doesn't fire during construction before the handler is wired.
            _settings = SettingsService<ExportDwgSettings>.Load(ToolName);
            outputFolder = _settings.LastOutputFolder;
            exportLinked = _settings.ExportLinked;
            exportImported = _settings.ExportImported;
            subfoldersByViewType = _settings.SubfoldersByViewType;
            includePlan = _settings.IncludePlan;
            includeSection = _settings.IncludeSection;
            includeDrafting = _settings.IncludeDrafting;
            includeNoViews = _settings.IncludeNoViews;
            generateSidecar = _settings.GenerateSidecar;
            overwrite = _settings.Overwrite;

            _handler.RequestCompleted += OnRequestCompleted;
            _handler.ProgressChanged += OnProgressChanged;

            // Kick off the initial document scan
            Raise(DwgExportRequest.Scan, "Scanning document for DWG instances…");
        }

        // ── Commands ─────────────────────────────────────────────────────────

        [RelayCommand(CanExecute = nameof(NotBusy))]
        private void Rescan()
        {
            LogSection("Re-scan");
            Raise(DwgExportRequest.Scan, "Scanning document for DWG instances…");
        }

        [RelayCommand]
        private void BrowseOutputFolder()
        {
            // Use a SaveFileDialog in folder-selection mode (no Windows.Forms dependency).
            // The user types a path or navigates to a folder and clicks Save.
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Select output folder — type a folder name and click Save",
                Filter = "Folder|*.thisfiledoesnotexist",
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select folder"
            };
            if (!string.IsNullOrEmpty(OutputFolder) && Directory.Exists(OutputFolder))
                dlg.InitialDirectory = OutputFolder;

            if (dlg.ShowDialog() == true)
                OutputFolder = Path.GetDirectoryName(dlg.FileName) ?? OutputFolder;
        }

        [RelayCommand(CanExecute = nameof(CanRun))]
        private void Run()
        {
            SaveSettings();
            LogSection("Export");
            AddLog(LogLevel.Info, $"Output folder: {OutputFolder}");
            AddLog(LogLevel.Info, BuildOptionsSummary());

            _handler.Settings = BuildSettings();
            _handler.OutputFolder = OutputFolder;
            Raise(DwgExportRequest.Export, "Exporting DWGs…");
        }

        [RelayCommand]
        private void CopyLog()
        {
            try
            {
                System.Windows.Clipboard.SetText(
                    string.Join(Environment.NewLine, LogEntries.Select(l => l.ToString())));
                StatusMessage = $"Copied {LogEntries.Count} log line(s).";
            }
            catch (Exception ex)
            {
                AddLog(LogLevel.Warning, $"Clipboard error: {ex.Message}");
            }
        }

        // ── Property-change callbacks ────────────────────────────────────────

        partial void OnOutputFolderChanged(string value)
        {
            RunCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsBusyChanged(bool value)
        {
            RunCommand.NotifyCanExecuteChanged();
            RescanCommand.NotifyCanExecuteChanged();
        }

        // ── Request completion ────────────────────────────────────────────────

        private void OnProgressChanged(int current, int total)
        {
            _dispatcher.Invoke(() =>
            {
                Progress = total > 0 ? (double)current / total * 100 : 0;
                StatusMessage = $"Exporting {current} of {total}…";
            });
        }

        private void OnRequestCompleted()
        {
            _dispatcher.Invoke(() =>
            {
                IsBusy = false;
                Progress = 0;

                if (!_handler.LastRunSucceeded)
                {
                    AddLog(LogLevel.Error, _handler.ErrorMessage);
                    StatusMessage = "Operation failed — see log.";
                    return;
                }

                switch (_handler.Request)
                {
                    case DwgExportRequest.Scan:
                        CompleteScan();
                        break;
                    case DwgExportRequest.Export:
                        CompleteExport();
                        break;
                }
            });
        }

        private void CompleteScan()
        {
            var items = _handler.ScannedInstances;
            int linked = items.Count(i => i.SourceType == DwgSourceType.Link);
            int imported = items.Count(i => i.SourceType == DwgSourceType.Import);
            int withViews = items.Count(i => i.ViewCategory != DwgViewCategory.NoViews);
            int noViews = items.Count(i => i.ViewCategory == DwgViewCategory.NoViews);

            UpdateMetrics(items.Count, linked, imported, withViews, noViews);
            AddLog(LogLevel.Info, $"Found {items.Count} DWG instance(s) — {linked} linked, {imported} imported.");
            StatusMessage = items.Count > 0
                ? $"{items.Count} DWG(s) ready. Select an output folder and click Run."
                : "No DWG instances found in this document.";
        }

        private void CompleteExport()
        {
            int exported = _handler.ExportedCount;
            int skipped = _handler.SkippedCount;
            int failed = _handler.FailedCount;

            var items = _handler.ScannedInstances;
            var errors = _handler.ExportErrors;

            for (int i = 0; i < items.Count && i < errors.Count; i++)
            {
                string err = errors[i];
                if (err == null)
                    AddLog(LogLevel.Success, $"✔  {SubfolderPrefix(items[i])}{items[i].OutputFileName}");
                else if (err == "SKIP" || err == "SKIP_FILTER")
                    AddLog(LogLevel.Debug, $"◌  Skipped: {items[i].OutputFileName}");
                else
                    AddLog(LogLevel.Error, $"✘  {items[i].OutputFileName} — {err}");
            }

            var resultLevel = failed > 0 ? LogLevel.Warning : LogLevel.Success;
            AddLog(resultLevel, $"Done — {exported} exported | {skipped} skipped | {failed} failed");
            StatusMessage = $"{exported} exported | {skipped} skipped | {failed} failed";
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private bool NotBusy() => !IsBusy;

        private bool CanRun()
            => !IsBusy
            && !string.IsNullOrWhiteSpace(OutputFolder)
            && _handler.ScannedInstances?.Count > 0;

        private void Raise(DwgExportRequest request, string status)
        {
            IsBusy = true;
            StatusMessage = status;
            _handler.Request = request;
            _externalEvent.Raise();
        }

        private void UpdateMetrics(int total, int linked, int imported, int withViews, int noViews)
        {
            SummaryCards.Clear();
            SummaryCards.Add(new SummaryCard("Total DWGs", total, CardTone.Accent));
            SummaryCards.Add(new SummaryCard("Linked", linked));
            SummaryCards.Add(new SummaryCard("Imported", imported));
            SummaryCards.Add(new SummaryCard("With Views", withViews, CardTone.Success));
            SummaryCards.Add(new SummaryCard("No Views", noViews, noViews > 0 ? CardTone.Warning : CardTone.Neutral));
        }

        private ExportDwgSettings BuildSettings() => new ExportDwgSettings
        {
            LastOutputFolder = OutputFolder,
            ExportLinked = ExportLinked,
            ExportImported = ExportImported,
            SubfoldersByViewType = SubfoldersByViewType,
            IncludePlan = IncludePlan,
            IncludeSection = IncludeSection,
            IncludeDrafting = IncludeDrafting,
            IncludeNoViews = IncludeNoViews,
            GenerateSidecar = GenerateSidecar,
            Overwrite = Overwrite
        };

        private void SaveSettings()
        {
            var s = BuildSettings();
            s.LastOutputFolder = OutputFolder;
            try { SettingsService<ExportDwgSettings>.Save(ToolName, s); }
            catch (Exception ex) { AddLog(LogLevel.Warning, $"Could not save settings: {ex.Message}"); }
        }

        private static string SubfolderPrefix(DwgInstanceInfo info)
        {
            string src = info.SourceType == DwgSourceType.Link ? "link" : "import";
            string view = info.ViewCategory switch
            {
                DwgViewCategory.Plan => "plan",
                DwgViewCategory.Section => "section",
                DwgViewCategory.Drafting => "drafting",
                _ => "no_views"
            };
            return $"{src}\\{view}\\";
        }

        private string BuildOptionsSummary()
        {
            var parts = new List<string>();
            if (ExportLinked) parts.Add("linked");
            if (ExportImported) parts.Add("imported");
            string sources = parts.Count > 0 ? string.Join(", ", parts) : "none";

            var viewParts = new List<string>();
            if (IncludePlan) viewParts.Add("plan");
            if (IncludeSection) viewParts.Add("section");
            if (IncludeDrafting) viewParts.Add("drafting");
            if (IncludeNoViews) viewParts.Add("no_views");
            string views = viewParts.Count > 0 ? string.Join(", ", viewParts) : "none";

            string overwriteLabel = Overwrite ? "overwrite ON" : "overwrite OFF";
            string sidecarLabel = GenerateSidecar ? "sidecar ON" : "sidecar OFF";
            return $"Sources: {sources} | Views: {views} | {overwriteLabel} | {sidecarLabel}";
        }

        private void LogSection(string title)
            => AddLog(LogLevel.Debug, $"──────── {title} ────────");

        private void AddLog(LogLevel level, string message)
        {
            var entry = new LogEntry(level, message);
            if (_dispatcher.CheckAccess()) LogEntries.Add(entry);
            else _dispatcher.Invoke(() => LogEntries.Add(entry));
        }
    }
}
