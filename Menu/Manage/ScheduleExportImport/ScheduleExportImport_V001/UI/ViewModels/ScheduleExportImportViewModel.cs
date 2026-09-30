using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.IO;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Revit26_Plugin.ScheduleExportImport.V001.Core.Models;
using Revit26_Plugin.ScheduleExportImport.V001.Core.Services;
using Revit26_Plugin.ScheduleExportImport.V001.Infrastructure.ExternalEvents;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.ScheduleExportImport.V001.UI.ViewModels
{
    public partial class ScheduleExportImportViewModel : ObservableObject
    {
        private readonly Document _doc;
        private readonly ScheduleImportEventHandler _handler;
        private readonly ExternalEvent _externalEvent;

        public ObservableCollection<ScheduleViewInfo> Schedules { get; } = new ObservableCollection<ScheduleViewInfo>();
        public ObservableCollection<LogEntry> LogEntries { get; } = new ObservableCollection<LogEntry>();
        public ICollectionView SchedulesView { get; }

        [ObservableProperty]
        private string searchText = string.Empty;

        partial void OnSearchTextChanged(string value) => SchedulesView.Refresh();

        [RelayCommand]
        private void ClearSearch() => SearchText = string.Empty;

        private bool MatchesSearch(object item)
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return true;
            var s = (ScheduleViewInfo)item;
            var t = SearchText.Trim();
            return (s.Name?.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0)
                || (s.CategoryName?.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        [ObservableProperty]
        private ScheduleViewInfo selectedSchedule;

        [ObservableProperty]
        private string statusMessage = "Select a schedule from the list, then Export or Import.";

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string lastExportedFile = string.Empty;

        public ScheduleExportImportViewModel(Document doc, ScheduleImportEventHandler handler, ExternalEvent externalEvent)
        {
            _doc = doc;
            _handler = handler;
            _externalEvent = externalEvent;
            SchedulesView = CollectionViewSource.GetDefaultView(Schedules);
            SchedulesView.Filter = MatchesSearch;
            _handler.RequestCompleted += OnRequestCompleted;

            LoadSchedules();
        }

        // ── Load schedule list ─────────────────────────────────────────────────
        private void LoadSchedules()
        {
            IsBusy = true;
            StatusMessage = "Loading schedules...";
            _handler.Request = ScheduleImportRequest.LoadSchedules;
            _externalEvent.Raise();
        }

        [RelayCommand]
        private void RefreshSchedules()
        {
            Schedules.Clear();
            LogEntries.Clear();
            LastExportedFile = string.Empty;
            LoadSchedules();
        }

        // ── Export ─────────────────────────────────────────────────────────────
        [RelayCommand(CanExecute = nameof(CanExportOrImport))]
        private void ExportSchedule()
        {
            if (SelectedSchedule == null) return;

            var dlg = new SaveFileDialog
            {
                Title = "Export Schedule to Excel",
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                FileName = SanitizeFileName(SelectedSchedule.Name) + "_Export.xlsx"
            };
            if (dlg.ShowDialog() != true) return;

            var targetPath = dlg.FileName;

            IsBusy = true;
            StatusMessage = $"Exporting \"{SelectedSchedule.Name}\"...";
            AddLog(LogLevel.Info, $"Exporting schedule \"{SelectedSchedule.Name}\"...");

            _handler.Request = ScheduleImportRequest.ExportSchedule;
            _handler.TargetScheduleId = SelectedSchedule.ViewId;

            void Callback()
            {
                _handler.RequestCompleted -= Callback;
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    IsBusy = false;
                    if (!_handler.LastRunSucceeded)
                    {
                        AddLog(LogLevel.Error, $"Export failed: {_handler.ErrorMessage}");
                        StatusMessage = "Export failed.";
                        return;
                    }

                    try
                    {
                        ScheduleExcelService.Export(targetPath, SelectedSchedule.Name, _handler.ExportedHeaders, _handler.ExportedRows);
                        LastExportedFile = targetPath;
                        AddLog(LogLevel.Success, $"Exported {_handler.ExportedRows.Count} rows, {_handler.ExportedHeaders.Count} fields → {Path.GetFileName(targetPath)}");
                        StatusMessage = $"Exported {_handler.ExportedRows.Count} rows to {Path.GetFileName(targetPath)}";

                        var td = new TaskDialog("Export Complete");
                        td.MainContent = $"Exported {_handler.ExportedRows.Count} row(s) to:\n{targetPath}\n\nOpen the file in Excel?";
                        td.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
                        if (td.Show() == TaskDialogResult.Yes)
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(targetPath) { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        AddLog(LogLevel.Error, $"Failed to write Excel: {ex.Message}");
                        StatusMessage = "Excel write failed.";
                    }
                });
            }

            _handler.RequestCompleted += Callback;
            _externalEvent.Raise();
        }

        // ── Import ─────────────────────────────────────────────────────────────
        [RelayCommand(CanExecute = nameof(CanExportOrImport))]
        private void ImportSchedule()
        {
            if (SelectedSchedule == null) return;

            var dlg = new OpenFileDialog
            {
                Title = "Import Schedule from Excel",
                Filter = "Excel Files (*.xlsx)|*.xlsx"
            };
            if (!string.IsNullOrEmpty(LastExportedFile))
                dlg.InitialDirectory = Path.GetDirectoryName(LastExportedFile);

            if (dlg.ShowDialog() != true) return;

            (System.Collections.Generic.List<string> headers, System.Collections.Generic.List<ScheduleRow> rows) parsed;
            try
            {
                parsed = ScheduleExcelService.Import(dlg.FileName);
            }
            catch (Exception ex)
            {
                AddLog(LogLevel.Error, $"Could not read Excel file: {ex.Message}");
                TaskDialog.Show("Import Error", $"Could not read Excel file:\n{ex.Message}");
                return;
            }

            if (parsed.rows.Count == 0)
            {
                AddLog(LogLevel.Warning, "No data rows found in the Excel file.");
                TaskDialog.Show("Import", "No data rows found in the Excel file.");
                return;
            }

            // Determine writable headers — those that exist and are not read-only
            // Load field info first; if we haven't exported this schedule yet, do it now inline
            var writableHeaders = parsed.headers; // ViewModel passes all; handler skips read-only params

            AddLog(LogLevel.Info, $"Importing {parsed.rows.Count} rows from {Path.GetFileName(dlg.FileName)}...");
            IsBusy = true;
            StatusMessage = "Importing...";

            _handler.Request = ScheduleImportRequest.ImportSchedule;
            _handler.RowsToImport = parsed.rows;
            _handler.WritableHeaders = writableHeaders;

            void Callback()
            {
                _handler.RequestCompleted -= Callback;
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    IsBusy = false;
                    if (!_handler.LastRunSucceeded)
                    {
                        AddLog(LogLevel.Error, $"Import failed: {_handler.ErrorMessage}");
                        StatusMessage = "Import failed.";
                        return;
                    }

                    var r = _handler.LastImportResult;
                    AddLog(LogLevel.Success, $"Import done — {r.ElementsUpdated} elements updated, {r.ParametersWritten} params written, {r.ParametersSkipped} skipped, {r.ElementsNotFound} not found, {r.Errors} errors.");
                    if (!string.IsNullOrEmpty(r.ErrorDetail))
                        AddLog(LogLevel.Warning, r.ErrorDetail);

                    StatusMessage = $"Import complete — {r.ElementsUpdated} elements updated, {r.Errors} errors.";
                });
            }

            _handler.RequestCompleted += Callback;
            _externalEvent.Raise();
        }

        private bool CanExportOrImport() => SelectedSchedule != null && !IsBusy;

        partial void OnSelectedScheduleChanged(ScheduleViewInfo value)
        {
            ExportScheduleCommand.NotifyCanExecuteChanged();
            ImportScheduleCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsBusyChanged(bool value)
        {
            ExportScheduleCommand.NotifyCanExecuteChanged();
            ImportScheduleCommand.NotifyCanExecuteChanged();
        }

        // ── Handler callback (LoadSchedules + catch-all) ───────────────────────
        private void OnRequestCompleted()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                IsBusy = false;
                if (_handler.Request != ScheduleImportRequest.LoadSchedules) return;

                if (!_handler.LastRunSucceeded)
                {
                    AddLog(LogLevel.Error, $"Failed to load schedules: {_handler.ErrorMessage}");
                    StatusMessage = "Failed to load schedules.";
                    return;
                }

                Schedules.Clear();
                foreach (var s in _handler.LoadedSchedules)
                    Schedules.Add(s);

                AddLog(LogLevel.Info, $"Found {Schedules.Count} schedule(s) in document.");
                StatusMessage = Schedules.Count > 0
                    ? $"Found {Schedules.Count} schedule(s). Select one to export or import."
                    : "No schedules found in the document.";
            });
        }

        private void AddLog(LogLevel level, string message)
        {
            var entry = new LogEntry(level, message);
            Application.Current?.Dispatcher.Invoke(() => LogEntries.Add(entry));
        }

        private static string SanitizeFileName(string name)
        {
            foreach (var ch in Path.GetInvalidFileNameChars())
                name = name.Replace(ch, '_');
            return name;
        }
    }
}
