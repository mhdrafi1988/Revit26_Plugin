using System;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.Shared.Models;
using Revit26_Plugin.SmartViewToSheetPlacer.V212.Infrastructure.ExternalEvents;
using Revit26_Plugin.SmartViewToSheetPlacer.V212.Models;

namespace Revit26_Plugin.SmartViewToSheetPlacer.V212.ViewModels
{
    /// <summary>Stage 5: Placement Complete — results summary, open-sheets
    /// selection, and log export (manual + auto-save on completion).</summary>
    public partial class SmartViewToSheetPlacerViewModel
    {
        // ---- Stage 5 state ----
        [ObservableProperty] private int _placedSheetCount;
        [ObservableProperty] private int _placedViewCount;
        [ObservableProperty] private int _failedCount;

        /// <summary>
        /// V212 fix: previously always returned "Not Started" unless
        /// Stage4Complete was true, at which point it jumped straight to
        /// "In Progress" and never showed "Complete" — Stage 5 has no
        /// further user action after landing here, so "Complete" now shows
        /// once Stage 4 finishes (matches the other stages' pattern).
        /// </summary>
        public string Stage5StatusLabel => Stage4Complete ? "Complete" : "Not Started";

        private string? _lastExportFolder;

        /// <summary>
        /// V212: controls whether AutoSaveLogs() shows the folder-picker
        /// popup on its first save each session. OFF (default, per Rafi):
        /// auto-save silently to the last-used folder, or My Documents if
        /// none yet — no popup, ever. ON: restores the original behavior —
        /// prompt once per session, then reuse silently for the rest of it.
        /// Does not affect the manual "Export Logs" button, which always
        /// shows the native Save dialog regardless of this setting.
        /// </summary>
        [ObservableProperty] private bool _promptForFolderOnAutoSave = false;

        /// <summary>
        /// Controls whether Stage 5's Activity Log card is expanded or
        /// collapsed. Confirmed with Rafi: persisted across sessions (not
        /// just this session) — restored from settings.json on load via
        /// LoadSettings(), saved via SaveSettings() same as other persisted
        /// fields (Margins, Gap settings, etc.). V212 UPDATE: default changed
        /// to collapsed (false) — user expands manually if/when needed;
        /// LoadSettings() overwrites this with whatever was last saved on
        /// every launch after the first.
        /// </summary>
        [ObservableProperty] private bool _isActivityLogExpanded = false;

        /// <summary>
        /// V212 UPDATE: only affects sheets in currently-EXPANDED SheetTypeGroups
        /// — confirmed with Rafi: collapsed groups are left untouched, so a user
        /// reviewing one expanded group can Select All without accidentally
        /// checking sheets in groups they haven't looked at yet.
        /// </summary>
        [RelayCommand]
        private void SelectAllSheetsToOpen()
        {
            foreach (var s in SheetTypeGroups.Where(g => g.IsExpanded).SelectMany(g => g.Sheets))
                s.OpenAfterPlacement = true;
        }

        /// <summary>See SelectAllSheetsToOpen — same expanded-groups-only restriction.</summary>
        [RelayCommand]
        private void ClearSheetsToOpen()
        {
            foreach (var s in SheetTypeGroups.Where(g => g.IsExpanded).SelectMany(g => g.Sheets))
                s.OpenAfterPlacement = false;
        }

        /// <summary>
        /// V212 UPDATE: renamed from OpenSelectedAndClose — "Open Selected" and
        /// "Close" are now two separate buttons (confirmed with Rafi). This no
        /// longer closes the window in any case: if nothing is checked to open,
        /// it silently does nothing (no log entry, per Rafi); otherwise it opens
        /// the selected sheets and the window stays open afterward — see
        /// OnHandlerRequestCompleted's OpenSheets case, which used to call
        /// CloseRequested here but now just logs completion instead.
        /// </summary>
        [RelayCommand]
        private void OpenSelected()
        {
            var idsToOpen = SuggestedSheets
                .Where(s => s.OpenAfterPlacement && s.CreatedSheetId != null)
                .Select(s => s.CreatedSheetId!)
                .ToList();

            if (idsToOpen.Count == 0)
                return;

            IsBusy = true;
            BusyMessage = "Opening selected sheets...";
            _handler.SheetIdsToOpen = idsToOpen;
            _handler.Request = SmartViewToSheetPlacerRequest.OpenSheets;
            _event.Raise();
        }

        [RelayCommand]
        private void ExportLogs()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                    FileName = $"{ToolName}_Logs_{DateTime.Now:yyyy-MM-dd_HH-mm}.txt",
                    InitialDirectory = _lastExportFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                };

                if (dialog.ShowDialog() == true)
                {
                    var text = string.Join(Environment.NewLine, Logs.Select(l => l.ToString()));
                    File.WriteAllText(dialog.FileName, text);
                    _lastExportFolder = Path.GetDirectoryName(dialog.FileName);
                    Logs.Add(new LogEntry(LogLevel.Success, $"Logs exported to: {dialog.FileName}"));
                }
            }
            catch (Exception ex)
            {
                Logs.Add(new LogEntry(LogLevel.Error, $"Failed to export logs: {ex.Message}"));
            }
        }

        /// <summary>
        /// Auto-saves logs on completion (in addition to the manual Export
        /// Logs button), per convention. V212: only shows the folder-picker
        /// popup if PromptForFolderOnAutoSave is checked (default OFF) —
        /// otherwise silently uses the last-used folder or My Documents,
        /// with no popup at all.
        /// </summary>
        private void AutoSaveLogs()
        {
            try
            {
                string folder = _lastExportFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (_lastExportFolder == null && PromptForFolderOnAutoSave)
                {
                    var selected = Services.DialogService.SelectFolder(folder);
                    if (!string.IsNullOrEmpty(selected))
                        folder = selected;
                }
                _lastExportFolder = folder;

                string fileName = $"{ToolName}_Logs_{DateTime.Now:yyyy-MM-dd_HH-mm}.txt";
                string fullPath = Path.Combine(folder, fileName);
                var text = string.Join(Environment.NewLine, Logs.Select(l => l.ToString()));
                File.WriteAllText(fullPath, text);
                Logs.Add(new LogEntry(LogLevel.Info, $"Logs auto-saved to: {fullPath}"));
            }
            catch (Exception ex)
            {
                Logs.Add(new LogEntry(LogLevel.Warning, $"Auto-save of logs failed: {ex.Message}"));
            }
        }
    }
}
