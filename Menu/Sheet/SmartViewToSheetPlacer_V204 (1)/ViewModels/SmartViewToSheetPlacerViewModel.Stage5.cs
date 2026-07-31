using System;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.Shared.Models;
using Revit26_Plugin.SmartViewToSheetPlacer.V204.Infrastructure.ExternalEvents;

namespace Revit26_Plugin.SmartViewToSheetPlacer.V204.ViewModels
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
        /// V204 fix: previously always returned "Not Started" unless
        /// Stage4Complete was true, at which point it jumped straight to
        /// "In Progress" and never showed "Complete" — Stage 5 has no
        /// further user action after landing here, so "Complete" now shows
        /// once Stage 4 finishes (matches the other stages' pattern).
        /// </summary>
        public string Stage5StatusLabel => Stage4Complete ? "Complete" : "Not Started";

        private string? _lastExportFolder;

        [RelayCommand]
        private void SelectAllSheetsToOpen()
        {
            foreach (var s in SuggestedSheets)
                s.OpenAfterPlacement = true;
        }

        [RelayCommand]
        private void ClearSheetsToOpen()
        {
            foreach (var s in SuggestedSheets)
                s.OpenAfterPlacement = false;
        }

        [RelayCommand]
        private void OpenSelectedAndClose()
        {
            var idsToOpen = SuggestedSheets
                .Where(s => s.OpenAfterPlacement && s.CreatedSheetId != null)
                .Select(s => s.CreatedSheetId!)
                .ToList();

            if (idsToOpen.Count == 0)
            {
                CloseRequested?.Invoke();
                return;
            }

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
        /// Logs button), per convention. Prompts for a folder on first save
        /// this session, then reuses the last-used folder silently.
        /// </summary>
        private void AutoSaveLogs()
        {
            try
            {
                string folder = _lastExportFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (_lastExportFolder == null)
                {
                    var selected = Services.DialogService.SelectFolder(folder);
                    if (!string.IsNullOrEmpty(selected))
                        folder = selected;
                    _lastExportFolder = folder;
                }

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
