// File: AutoSlopeDrainViewModel.cs
// Location: UI/ViewModels/
// Base: AutoSlopeByDrain V004's AutoSlopeDrainViewModel (Select All/None/
// Invert, size filter + DataGrid sorting via ICollectionView, Run wiring
// through AutoSlopeDrainEventManager).
//
// CHANGES (V005), per Rafi's confirmed decisions on 2026-07-21:
//   REWRITTEN using CommunityToolkit.Mvvm's ObservableObject +
//     [ObservableProperty] + [RelayCommand] source-generator attributes,
//     instead of the hand-rolled INotifyPropertyChanged + plain
//     RelayCommand pattern both V004 and ByPoint V018 actually used. This
//     is an intentional deviation from both prior tools' pattern, flagged
//     explicitly per Rafi's request — requires a confirmed
//     CommunityToolkit.Mvvm PackageReference in the .csproj (Rafi is
//     wiring the .csproj himself; not included here).
//   ADDED   ThresholdMeters (Max Path Distance) input, distinct from
//     ConnectionThresholdMeters (Max Edge Distance) — ported from ByPoint.
//   ADDED   InsertCurveIntersectionPoints toggle (off by default).
//   ADDED   VerifyElevationsAfterCommit toggle (OFF by default).
//   CHANGED export: Excel-only (ExportToExcel replaces ExportToCsv),
//     single exported file path instead of Detailed+Summary pair.
//   ADDED   settings persistence via SettingsService — loads on
//     construction, saves after a successful Run and can be called on
//     window close from the code-behind.
//   ADDED   CanRun / RunDisabledReason surfaced so the View can show an
//     inline hint ("Select at least one drain to run") when the Run
//     button is disabled specifically because 0 drains are selected —
//     confirmed UX decision, not a silent disable with no explanation.
//   ADDED   TotalDetectedCount / SelectedCount / ArcsCalculated metric
//     bindings for the new UI metric cards.

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.AutoSlopeByDrain.V007.Core.Models;
using Revit26_Plugin.AutoSlopeByDrain.V007.Core.Services;
using Revit26_Plugin.AutoSlopeByDrain.V007.Infrastructure.ExternalEvents;
using Revit26_Plugin.AutoSlopeByDrain.V007.Infrastructure.Helpers;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace Revit26_Plugin.AutoSlopeByDrain.V007.UI.ViewModels
{
    public partial class AutoSlopeDrainViewModel : ObservableObject
    {
        // Must be public (not private) — [ObservableProperty] generates a public
        // State property, and a public property cannot expose a less-accessible
        // type. The enum is still only meant for internal use within this
        // ViewModel; nothing outside binds to it directly.
        public enum RunState { Ready, Running, Done }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusMessage))]
        [NotifyPropertyChangedFor(nameof(LongestPathDisplay))]
        [NotifyPropertyChangedFor(nameof(HighestElevationDisplay))]
        [NotifyCanExecuteChangedFor(nameof(RunAutoSlopeCommand))]
        private RunState state = RunState.Ready;

        private bool IsRunning => State == RunState.Running;
        private bool IsComplete => State == RunState.Done;

        public string StatusMessage => State switch
        {
            RunState.Running => "Processing...",
            RunState.Done => "Completed",
            _ => "Ready"
        };

        // ── Roof / drains ─────────────────────────────────────────────────────
        public UIDocument UIDoc { get; }
        public UIApplication App { get; }
        private readonly RoofData _roofData;
        public ElementId RoofId => _roofData.Roof.Id;

        public ObservableCollection<DrainItem> AllDrains { get; } = new ObservableCollection<DrainItem>();
        public ObservableCollection<string> SizeFilters { get; } = new ObservableCollection<string>();
        public ICollectionView FilteredDrainsView { get; }

        [ObservableProperty]
        private string roofSubtitle;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RunAutoSlopeCommand))]
        private string selectedSizeFilter = "All";

        partial void OnSelectedSizeFilterChanged(string value)
        {
            FilteredDrainsView.Refresh();
            UpdateSelectedCount();
        }

        [ObservableProperty]
        private int selectedDrainsCount;

        /// <summary>
        /// NEW (V005). True only when the Run button is disabled specifically
        /// because 0 drains are selected — the View shows an inline hint in
        /// this case rather than a silent disable with no explanation, per
        /// Rafi's confirmed UX decision.
        /// </summary>
        public bool ShowNoDrainsSelectedHint => !IsRunning && !IsComplete && SelectedDrainsCount == 0;

        // ── Slope inputs ──────────────────────────────────────────────────────
        public List<string> SlopeOptions { get; } = new List<string> { "1.0", "1.5", "2.0", "2.5", "3.0" };

        [ObservableProperty]
        private string slopeInput = AppConstants.DefaultSlopePercent.ToString("0.0");

        [ObservableProperty]
        private string connectionThresholdInput = AppConstants.DefaultConnectionThresholdMeters.ToString();

        /// <summary>NEW (V005): Max Path Distance input, ported from ByPoint. Distinct from ConnectionThresholdInput.</summary>
        [ObservableProperty]
        private string thresholdInput = AppConstants.DefaultThresholdMeters.ToString();

        [ObservableProperty]
        private string pathSampleCountInput = AppConstants.DefaultPathSampleCount.ToString();

        /// <summary>NEW (V005), ported from ByPoint. Opt-in, off by default.</summary>
        [ObservableProperty]
        private bool insertCurveIntersectionPoints = false;

        /// <summary>NEW (V005). Opt-in, OFF by default per Rafi's confirmed decision.</summary>
        [ObservableProperty]
        private bool verifyElevationsAfterCommit = false;

        // ── Export ────────────────────────────────────────────────────────────
        [ObservableProperty]
        private string exportFolderPath;

        // ── Log ───────────────────────────────────────────────────────────────
        public ObservableCollection<LogEntry> LogEntries { get; } = new ObservableCollection<LogEntry>();

        // ── Results / metrics ─────────────────────────────────────────────────
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LongestPathDisplay))]
        private double longestPath_m;
        public string LongestPathDisplay => IsComplete ? LongestPath_m.ToString("F2") : "N/A";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HighestElevationDisplay))]
        private double highestElevation_mm;
        public string HighestElevationDisplay => IsComplete ? HighestElevation_mm.ToString("F0") : "N/A";

        [ObservableProperty]
        private int runDuration_sec;

        [ObservableProperty]
        private int finalDrainCount;

        /// <summary>NEW (V005): drains DrainDetectionService found, before selection.</summary>
        [ObservableProperty]
        private int totalDetectedCount;

        /// <summary>NEW (V005): drains checked in the grid at Run time (== FinalDrainCount, kept as a separate binding for clarity in the UI).</summary>
        [ObservableProperty]
        private int selectedCount;

        /// <summary>NEW (V005): boundary/opening arcs found. 0 unless InsertCurveIntersectionPoints was on.</summary>
        [ObservableProperty]
        private int arcsCalculated;

        private AutoSlopeDrainResult _lastResult;

        // ── Constructor ───────────────────────────────────────────────────────
        public AutoSlopeDrainViewModel(UIDocument uidoc, UIApplication app, RoofData roofData)
        {
            UIDoc = uidoc;
            App = app;
            _roofData = roofData;

            RoofSubtitle = $"Revit 2026 · Roof: {roofData.Roof.Name} (Id {roofData.Roof.Id.Value})";

            var detectionService = new DrainDetectionService();

            foreach (var drain in roofData.DetectedDrains)
            {
                AllDrains.Add(drain);
                drain.PropertyChanged += OnDrainPropertyChanged;
            }

            foreach (var category in detectionService.GenerateSizeCategories(roofData.DetectedDrains))
                SizeFilters.Add(category);

            FilteredDrainsView = CollectionViewSource.GetDefaultView(AllDrains);
            FilteredDrainsView.Filter = FilterDrainItem;

            TotalDetectedCount = roofData.DetectedDrains.Count;

            // ── NEW (V005): load persisted settings, falling back to AppConstants
            // defaults for anything not yet saved (first run, or a fresh machine).
            var settings = SettingsService.Load(msg => AddLog(new LogEntry(LogLevel.Warning, msg)));

            SlopeInput = settings.SlopePercent.ToString("0.0");
            ConnectionThresholdInput = settings.ConnectionThresholdMeters.ToString("0");
            ThresholdInput = settings.ThresholdMeters.ToString("0");
            PathSampleCountInput = settings.PathSampleCount.ToString();
            InsertCurveIntersectionPoints = settings.InsertCurveIntersectionPoints;
            VerifyElevationsAfterCommit = settings.VerifyElevationsAfterCommit;
            SelectedSizeFilter = string.IsNullOrEmpty(settings.SelectedSizeFilter) ? "All" : settings.SelectedSizeFilter;

            ExportFolderPath = !string.IsNullOrWhiteSpace(settings.ExportFolderPath) && Directory.Exists(settings.ExportFolderPath)
                ? settings.ExportFolderPath
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    AppConstants.DefaultExportFolder);

            if (!Directory.Exists(ExportFolderPath))
                Directory.CreateDirectory(ExportFolderPath);

            AddLog(new LogEntry(LogLevel.Info, $"Detected {roofData.DetectedDrains.Count} drain opening(s)."));
            AddLog(new LogEntry(LogLevel.Info, $"Default export folder: {ExportFolderPath}"));

            UpdateSelectedCount();

            AutoSlopeDrainEventManager.Init();
        }

        // ── Filtering ───────────────────────────────────────────────────────────
        private void OnDrainPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(DrainItem.IsSelected)) return;
            UpdateSelectedCount();
            RunAutoSlopeCommand.NotifyCanExecuteChanged();
        }

        private bool FilterDrainItem(object obj)
        {
            if (!(obj is DrainItem drain)) return false;
            if (SelectedSizeFilter == "All") return true;

            var svc = new DrainDetectionService();
            return svc.FilterDrainsBySize(new List<DrainItem> { drain }, SelectedSizeFilter).Any();
        }

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var d in FilteredDrainsView.Cast<DrainItem>()) d.IsSelected = true;
            FilteredDrainsView.Refresh();
            UpdateSelectedCount();
            AddLog(new LogEntry(LogLevel.Info, $"Selected all {FilteredDrainsView.Cast<DrainItem>().Count()} visible drain(s)."));
            RunAutoSlopeCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private void SelectNone()
        {
            foreach (var d in FilteredDrainsView.Cast<DrainItem>()) d.IsSelected = false;
            FilteredDrainsView.Refresh();
            UpdateSelectedCount();
            AddLog(new LogEntry(LogLevel.Info, "Deselected all visible drains."));
            RunAutoSlopeCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private void InvertSelection()
        {
            foreach (var d in FilteredDrainsView.Cast<DrainItem>()) d.IsSelected = !d.IsSelected;
            FilteredDrainsView.Refresh();
            UpdateSelectedCount();
            AddLog(new LogEntry(LogLevel.Info, $"Selection inverted: {AllDrains.Count(d => d.IsSelected)} selected."));
            RunAutoSlopeCommand.NotifyCanExecuteChanged();
        }

        private void UpdateSelectedCount()
        {
            SelectedDrainsCount = FilteredDrainsView.Cast<DrainItem>().Count(d => d.IsSelected);
            OnPropertyChanged(nameof(ShowNoDrainsSelectedHint));
        }

        // ── Run ───────────────────────────────────────────────────────────────
        private bool CanRunAutoSlope() => !IsRunning && !IsComplete && AllDrains.Any(d => d.IsSelected);

        [RelayCommand(CanExecute = nameof(CanRunAutoSlope))]
        private void RunAutoSlope()
        {
            if (IsRunning || IsComplete) return;

            if (!double.TryParse(SlopeInput, out double slopePercent) || slopePercent <= 0)
            {
                AddLog(new LogEntry(LogLevel.Error, "Please enter a valid positive slope percentage."));
                return;
            }
            if (!double.TryParse(ConnectionThresholdInput, out double connectionThresholdM) || connectionThresholdM <= 0)
            {
                AddLog(new LogEntry(LogLevel.Error, "Please enter a valid positive Max Edge Distance (m)."));
                return;
            }
            if (!double.TryParse(ThresholdInput, out double thresholdM) || thresholdM <= 0)
            {
                AddLog(new LogEntry(LogLevel.Error, "Please enter a valid positive Max Path Distance (m)."));
                return;
            }
            if (!int.TryParse(PathSampleCountInput, out int pathSamples) || pathSamples < 2)
            {
                AddLog(new LogEntry(LogLevel.Error, "Path Samples must be a whole number of 2 or more."));
                return;
            }

            // UPDATED (V005, second round): pass the checked DrainItem objects
            // directly, per Rafi's confirmed decision — no more lightweight
            // signatures. The Engine reuses these exact objects (including their
            // LoopCurves) rather than re-detecting and position-matching.
            var selectedDrainItems = AllDrains.Where(d => d.IsSelected).ToList();

            if (selectedDrainItems.Count == 0)
            {
                // Defensive backstop — CanRunAutoSlope already prevents this in
                // normal use via the disabled Run button + inline hint.
                AddLog(new LogEntry(LogLevel.Warning, "No drains selected for slope application."));
                return;
            }

            State = RunState.Running;
            LogEntries.Clear();
            AddLog(new LogEntry(LogLevel.Info, "Starting AutoSlope By Drain..."));

            if (!Directory.Exists(ExportFolderPath))
            {
                try { Directory.CreateDirectory(ExportFolderPath); }
                catch (Exception ex)
                {
                    AddLog(new LogEntry(LogLevel.Warning, $"Failed to create export directory: {ex.Message}"));
                }
            }

            AutoSlopeDrainHandler.Payload = new AutoSlopeDrainPayload
            {
                RoofId = RoofId,
                SelectedDrains = selectedDrainItems,
                TotalDetectedCount = TotalDetectedCount,
                SlopePercent = slopePercent,
                ConnectionThresholdMeters = connectionThresholdM,
                ThresholdMeters = thresholdM,
                PathSampleCount = pathSamples,
                InsertCurveIntersectionPoints = InsertCurveIntersectionPoints,
                VerifyElevationsAfterCommit = VerifyElevationsAfterCommit,
                ProjectTitle = UIDoc?.Document?.Title ?? "Unknown Project",
                Log = AddLog,
                ExportConfig = new ExportConfig
                {
                    ExportPath = ExportFolderPath,
                    ExportToExcel = true
                },
                OnCompleted = result =>
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (!result.Success)
                        {
                            AddLog(new LogEntry(LogLevel.Error, $"Run failed: {result.ErrorMessage}"));
                            State = RunState.Ready;
                            return;
                        }

                        _lastResult = result;
                        LongestPath_m = result.LongestPath_m;
                        HighestElevation_mm = result.HighestElevation_mm;
                        RunDuration_sec = result.RunDuration_sec;
                        FinalDrainCount = result.DrainCount;
                        TotalDetectedCount = result.TotalDetectedCount;
                        SelectedCount = result.SelectedCount;
                        ArcsCalculated = result.ArcsCalculated;

                        State = RunState.Done;

                        // NEW (V005): persist settings after a successful run.
                        SettingsService.Save(new AutoSlopeDrainSettings
                        {
                            SlopePercent = slopePercent,
                            ConnectionThresholdMeters = connectionThresholdM,
                            ThresholdMeters = thresholdM,
                            PathSampleCount = pathSamples,
                            InsertCurveIntersectionPoints = InsertCurveIntersectionPoints,
                            VerifyElevationsAfterCommit = VerifyElevationsAfterCommit,
                            ExportFolderPath = ExportFolderPath,
                            SelectedSizeFilter = SelectedSizeFilter
                        }, msg => AddLog(new LogEntry(LogLevel.Warning, msg)));

                        if (!string.IsNullOrEmpty(result.ExportedFilePath))
                        {
                            var answer = MessageBox.Show(
                                "Excel export completed. Open the export folder now?",
                                "Export Complete",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);

                            if (answer == MessageBoxResult.Yes)
                                System.Diagnostics.Process.Start("explorer.exe", ExportFolderPath);
                        }
                    }));
                }
            };

            AutoSlopeDrainEventManager.Event.Raise();
        }

        [RelayCommand]
        private void BrowseFolder()
        {
            var selected = DialogService.SelectFolder(ExportFolderPath);
            if (!string.IsNullOrEmpty(selected))
            {
                ExportFolderPath = selected;
                AddLog(new LogEntry(LogLevel.Info, $"Export folder set to: {ExportFolderPath}"));
            }
        }

        [RelayCommand]
        private void OpenExportFolder()
        {
            if (!string.IsNullOrEmpty(ExportFolderPath) && Directory.Exists(ExportFolderPath))
                System.Diagnostics.Process.Start("explorer.exe", ExportFolderPath);
        }

        [RelayCommand]
        private void ClearLog()
        {
            LogEntries.Clear();
            AddLog(new LogEntry(LogLevel.Info, "Log cleared."));
        }

        [RelayCommand]
        private void CopyAllLogs()
        {
            if (LogEntries.Count == 0) return;
            string text = string.Join(Environment.NewLine, LogEntries.Select(e => e.ToString()));
            Clipboard.SetText(text);
        }

        /// <summary>
        /// Called by the View's code-behind on window close, so settings capture
        /// the current UI state even if the user never clicked Run this session.
        /// </summary>
        public void SaveSettingsOnClose()
        {
            double.TryParse(SlopeInput, out double slope);
            double.TryParse(ConnectionThresholdInput, out double connThreshold);
            double.TryParse(ThresholdInput, out double threshold);
            int.TryParse(PathSampleCountInput, out int samples);

            SettingsService.Save(new AutoSlopeDrainSettings
            {
                SlopePercent = slope > 0 ? slope : AppConstants.DefaultSlopePercent,
                ConnectionThresholdMeters = connThreshold > 0 ? connThreshold : AppConstants.DefaultConnectionThresholdMeters,
                ThresholdMeters = threshold > 0 ? threshold : AppConstants.DefaultThresholdMeters,
                PathSampleCount = samples >= 2 ? samples : AppConstants.DefaultPathSampleCount,
                InsertCurveIntersectionPoints = InsertCurveIntersectionPoints,
                VerifyElevationsAfterCommit = VerifyElevationsAfterCommit,
                ExportFolderPath = ExportFolderPath,
                SelectedSizeFilter = SelectedSizeFilter
            });
        }

        // ── AddLog (thread-safe) ──────────────────────────────────────────────
        private void AddLog(LogEntry entry)
        {
            var dispatcher = Application.Current.Dispatcher;
            if (dispatcher.CheckAccess())
                LogEntries.Add(entry);
            else
                dispatcher.BeginInvoke(new Action(() => LogEntries.Add(entry)));
        }
    }
}