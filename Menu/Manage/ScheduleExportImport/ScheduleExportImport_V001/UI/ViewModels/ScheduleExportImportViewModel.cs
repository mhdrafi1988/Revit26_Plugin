using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Revit26_Plugin.ScheduleExportImport.V001.Core.Models;
using Revit26_Plugin.ScheduleExportImport.V001.Core.Services;
using Revit26_Plugin.ScheduleExportImport.V001.Infrastructure.ExternalEvents;
using Revit26_Plugin.Shared.Models;
using Revit26_Plugin.Shared.Services;

namespace Revit26_Plugin.ScheduleExportImport.V001.UI.ViewModels
{
    public enum PreviewFilter { All, Changes, NotFound, Duplicate, MissingFromFile, Skipped, Applied, Failed }

    public partial class ScheduleExportImportViewModel : ObservableObject
    {
        private const string ToolName = "ScheduleExportImport";
        private const int MaxDetailLogLines = 300;

        private readonly ScheduleImportEventHandler _handler;
        private readonly ExternalEvent _externalEvent;
        // Application.Current is null inside Revit, so capture the window's dispatcher instead.
        private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

        private string _pendingExportPath;
        private ExcelImportFile _importFile;
        private ImportAnalysis _analysis;
        private string _previewScheduleName;

        /// <summary>Raised when an analysis is ready; the main window opens the preview window.</summary>
        public event Action PreviewRequested;

        // ══ Schedule list ════════════════════════════════════════════════
        public ObservableCollection<ScheduleViewInfo> Schedules { get; } = new ObservableCollection<ScheduleViewInfo>();
        public ICollectionView SchedulesView { get; }

        [ObservableProperty] private string searchText = string.Empty;
        [ObservableProperty] private ScheduleViewInfo selectedSchedule;
        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private string statusMessage = "Loading schedules…";
        [ObservableProperty] private string lastExportedFile = string.Empty;

        // ══ Summary cards (main window) ══════════════════════════════════
        public ObservableCollection<SummaryCard> SummaryCards { get; } = new ObservableCollection<SummaryCard>();
        [ObservableProperty] private string summaryTitle = "Document";

        // ══ Import preview ═══════════════════════════════════════════════
        public ObservableCollection<ImportChangeRowViewModel> PreviewRows { get; } = new ObservableCollection<ImportChangeRowViewModel>();
        public ICollectionView PreviewView { get; }
        public ObservableCollection<SummaryCard> PreviewCards { get; } = new ObservableCollection<SummaryCard>();

        [ObservableProperty] private PreviewFilter activePreviewFilter = PreviewFilter.All;
        [ObservableProperty] private string previewSearchText = string.Empty;
        [ObservableProperty] private string previewFileName = string.Empty;
        [ObservableProperty] private string previewWarning = string.Empty;
        [ObservableProperty] private int selectedChangeCount;
        [ObservableProperty] private bool hasApplied;
        [ObservableProperty] private string lastReportPath = string.Empty;

        // Chip counts
        [ObservableProperty] private int countAll;
        [ObservableProperty] private int countChanges;
        [ObservableProperty] private int countNotFound;
        [ObservableProperty] private int countDuplicate;
        [ObservableProperty] private int countMissing;
        [ObservableProperty] private int countSkipped;
        [ObservableProperty] private int countApplied;
        [ObservableProperty] private int countFailed;

        // ══ Log ══════════════════════════════════════════════════════════
        public ObservableCollection<LogEntry> LogEntries { get; } = new ObservableCollection<LogEntry>();

        public ScheduleExportImportViewModel(ScheduleImportEventHandler handler, ExternalEvent externalEvent)
        {
            _handler = handler;
            _externalEvent = externalEvent;

            SchedulesView = CollectionViewSource.GetDefaultView(Schedules);
            SchedulesView.Filter = MatchesScheduleSearch;
            PreviewView = new ListCollectionView(PreviewRows) { Filter = MatchesPreviewFilter };

            _handler.RequestCompleted += OnRequestCompleted;
            Raise(ScheduleImportRequest.LoadSchedules);
        }

        // ══════════════════════════════════════════════════════════════════
        // Schedule list
        // ══════════════════════════════════════════════════════════════════

        partial void OnSearchTextChanged(string value) => SchedulesView.Refresh();

        [RelayCommand]
        private void ClearSearch() => SearchText = string.Empty;

        private bool MatchesScheduleSearch(object item)
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return true;
            var s = (ScheduleViewInfo)item;
            var t = SearchText.Trim();
            return (s.Name?.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0)
                || (s.CategoryName?.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        [RelayCommand(CanExecute = nameof(NotBusy))]
        private void RefreshSchedules()
        {
            LogSection("Refresh schedule list");
            Raise(ScheduleImportRequest.LoadSchedules);
        }

        partial void OnSelectedScheduleChanged(ScheduleViewInfo value)
        {
            RefreshCommandStates();
            if (value == null) return;
            SummaryTitle = value.Name;
            SetCards(SummaryCards,
                new SummaryCard("Elements", value.RowCount, CardTone.Accent),
                new SummaryCard("Visible fields", value.FieldCount),
                new SummaryCard("Schedules", Schedules.Count));
        }

        partial void OnIsBusyChanged(bool value) => RefreshCommandStates();

        // ══════════════════════════════════════════════════════════════════
        // Export
        // ══════════════════════════════════════════════════════════════════

        [RelayCommand(CanExecute = nameof(HasScheduleAndNotBusy))]
        private void ExportSchedule()
        {
            var dlg = new SaveFileDialog
            {
                Title = "Export Schedule to Excel",
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = SanitizeFileName(SelectedSchedule.Name) + "_Export.xlsx"
            };
            if (dlg.ShowDialog() != true) return;

            _pendingExportPath = dlg.FileName;
            LogSection($"Export — {SelectedSchedule.Name}");
            AddLog(LogLevel.Info, $"Reading {SelectedSchedule.RowCount} element(s) from the schedule…");
            _handler.TargetScheduleId = SelectedSchedule.ViewId;
            Raise(ScheduleImportRequest.ExportSchedule, $"Exporting \"{SelectedSchedule.Name}\"…");
        }

        private void CompleteExport()
        {
            var headers = _handler.ExportedHeaders;
            var rows = _handler.ExportedRows;
            var editable = _handler.EditableHeaders;
            try
            {
                ScheduleExcelService.Export(_pendingExportPath, SelectedSchedule.Name, headers, rows, editable);
            }
            catch (IOException ex)
            {
                Fail($"Could not write the Excel file. Close it in Excel if it is open, then try again. ({ex.Message})");
                return;
            }
            catch (Exception ex)
            {
                Fail($"Could not write the Excel file: {ex.Message}");
                return;
            }

            LastExportedFile = _pendingExportPath;
            int readOnly = headers.Count - editable.Count;
            AddLog(LogLevel.Info, $"Editable columns ({editable.Count}): {Preview(editable)}");
            if (readOnly > 0)
                AddLog(LogLevel.Info, $"Read-only columns ({readOnly}): {Preview(headers.Where(h => !editable.Contains(h)))}");
            var exportSize = FileSizeLabel(_pendingExportPath);
            AddLog(LogLevel.Success, $"Exported {rows.Count} row(s) × {headers.Count} field(s) → {Path.GetFileName(_pendingExportPath)}  ({exportSize})");
            AddLog(LogLevel.Info, $"Folder: {Path.GetDirectoryName(_pendingExportPath)}");

            SummaryTitle = $"Last export — {SelectedSchedule.Name}";
            SetCards(SummaryCards,
                new SummaryCard("Rows exported", rows.Count, CardTone.Accent),
                new SummaryCard("Editable fields", editable.Count, CardTone.Success),
                new SummaryCard("Read-only fields", readOnly));
            StatusMessage = $"Exported {rows.Count} row(s) to {Path.GetFileName(_pendingExportPath)}";

            var td = new TaskDialog("Export Complete")
            {
                MainInstruction = $"Exported {rows.Count} row(s)",
                MainContent = $"{_pendingExportPath}\n\nGreen columns are editable; grey columns are locked and ignored on import.\n\nOpen the file in Excel now?",
                CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No
            };
            if (td.Show() == TaskDialogResult.Yes)
                OpenFile(_pendingExportPath);
        }

        // ══════════════════════════════════════════════════════════════════
        // Import step 1 — read file + analyze (no changes made)
        // ══════════════════════════════════════════════════════════════════

        [RelayCommand(CanExecute = nameof(HasScheduleAndNotBusy))]
        private void ImportSchedule()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Import Schedule from Excel",
                Filter = "Excel Workbook (*.xlsx)|*.xlsx"
            };
            if (!string.IsNullOrEmpty(LastExportedFile))
                dlg.InitialDirectory = Path.GetDirectoryName(LastExportedFile);
            if (dlg.ShowDialog() != true) return;

            LogSection($"Import — {Path.GetFileName(dlg.FileName)}");
            try
            {
                _importFile = ScheduleExcelService.Import(dlg.FileName);
            }
            catch (IOException ex)
            {
                Fail($"Could not open the file. Close it in Excel if it is open, then try again. ({ex.Message})");
                return;
            }
            catch (Exception ex)
            {
                Fail($"Could not read the file: {ex.Message}");
                return;
            }

            if (_importFile.Rows.Count == 0)
            {
                Fail("No data rows with a valid Element ID were found in the file.");
                return;
            }

            AddLog(LogLevel.Info, $"Read {_importFile.Rows.Count} row(s), {_importFile.Headers.Count} column(s).");
            _previewScheduleName = SelectedSchedule.Name;
            PreviewWarning = string.Empty;
            if (!string.IsNullOrEmpty(_importFile.ScheduleName)
                && !_importFile.ScheduleName.Equals(SelectedSchedule.Name, StringComparison.OrdinalIgnoreCase))
            {
                PreviewWarning = $"This file was exported from \"{_importFile.ScheduleName}\", but \"{SelectedSchedule.Name}\" is selected. " +
                                 "Element IDs still match the model, but 'Missing from file' will be compared against the selected schedule.";
                AddLog(LogLevel.Warning, PreviewWarning);
            }

            _handler.ImportFile = _importFile;
            _handler.TargetScheduleId = SelectedSchedule.ViewId;
            Raise(ScheduleImportRequest.AnalyzeImport, "Comparing file with model…");
        }

        private void CompleteAnalysis()
        {
            _analysis = _handler.Analysis;
            HasApplied = false;
            LastReportPath = string.Empty;
            PreviewFileName = Path.GetFileName(_importFile.FilePath);
            ActivePreviewFilter = PreviewFilter.All;
            PreviewSearchText = string.Empty;

            foreach (var row in PreviewRows) row.PropertyChanged -= PreviewRow_PropertyChanged;
            PreviewRows.Clear();
            foreach (var item in _analysis.Items
                         .OrderBy(i => i.Status)
                         .ThenBy(i => i.ElementId)
                         .ThenBy(i => i.ParameterName))
            {
                var row = new ImportChangeRowViewModel(item);
                row.PropertyChanged += PreviewRow_PropertyChanged;
                PreviewRows.Add(row);
            }

            RecalculatePreview();
            LogAnalysis();
            StatusMessage = CountChanges == 0
                ? "No differences found — nothing to apply."
                : $"{CountChanges} change(s) ready to review.";

            PreviewRequested?.Invoke();
        }

        private void LogAnalysis()
        {
            var a = _analysis;
            AddLog(LogLevel.Info, $"Matched {a.ElementsMatched} element(s); {a.UnchangedValues} value(s) unchanged.");
            if (a.DuplicateRowsMerged > 0)
                AddLog(LogLevel.Info, $"{a.DuplicateRowsMerged} repeated row(s) with identical values were merged.");

            LogGroup(ImportChangeStatus.Duplicate, LogLevel.Warning, "Duplicate Element ID(s) with conflicting values — held back");
            LogGroup(ImportChangeStatus.NotFound, LogLevel.Warning, "Element ID(s) not found in the model");
            LogGroup(ImportChangeStatus.MissingFromFile, LogLevel.Warning, "Schedule element(s) missing from the file — left unchanged");
            LogGroup(ImportChangeStatus.ReadOnly, LogLevel.Warning, "Edit(s) to read-only parameters — ignored");
            LogGroup(ImportChangeStatus.TypeParameter, LogLevel.Warning, "Edit(s) to type parameters — ignored");

            if (CountChanges > 0)
                AddLog(LogLevel.Success, $"{CountChanges} change(s) on {PreviewRows.Where(r => r.Status == ImportChangeStatus.Change).Select(r => r.ElementId).Distinct().Count()} element(s) ready — review them in the preview window.");
            else
                AddLog(LogLevel.Info, "No differences between the file and the model.");
        }

        private void LogGroup(ImportChangeStatus status, LogLevel level, string title)
        {
            var items = PreviewRows.Where(r => r.Status == status).ToList();
            if (items.Count == 0) return;
            var ids = items.Select(r => r.ElementId).Distinct().ToList();
            AddLog(level, $"{title}: {items.Count} — IDs {string.Join(", ", ids.Take(10))}{(ids.Count > 10 ? $" … (+{ids.Count - 10})" : "")}");
        }

        // ══════════════════════════════════════════════════════════════════
        // Import step 2 — apply ticked changes
        // ══════════════════════════════════════════════════════════════════

        [RelayCommand(CanExecute = nameof(CanApply))]
        private void ApplySelected()
        {
            var toApply = PreviewRows.Where(r => r.IsSelected && r.IsSelectable).Select(r => r.Model).ToList();
            AddLog(LogLevel.Info, $"Applying {toApply.Count} change(s) on {toApply.Select(c => c.ElementId).Distinct().Count()} element(s)…");
            _handler.ChangesToApply = toApply;
            Raise(ScheduleImportRequest.ApplyChanges, "Applying changes…");
        }

        private bool CanApply() => !IsBusy && !HasApplied && SelectedChangeCount > 0;

        private void CompleteApply()
        {
            HasApplied = true;
            foreach (var row in PreviewRows) row.Refresh();
            RecalculatePreview();

            var applied = PreviewRows.Where(r => r.Status == ImportChangeStatus.Applied).ToList();
            var failed = PreviewRows.Where(r => r.Status == ImportChangeStatus.Failed).ToList();

            foreach (var r in applied.Take(MaxDetailLogLines))
                AddLog(LogLevel.Success, $"#{r.ElementId}  {r.ParameterName}: \"{r.OldValue}\" → \"{r.NewValue}\"");
            if (applied.Count > MaxDetailLogLines)
                AddLog(LogLevel.Info, $"… {applied.Count - MaxDetailLogLines} more applied change(s) — see the import report.");
            foreach (var r in failed)
                AddLog(LogLevel.Error, $"#{r.ElementId}  {r.ParameterName}: \"{r.NewValue}\" failed — {r.Message}");

            try
            {
                LastReportPath = ImportReportService.Write(_importFile.FilePath, _previewScheduleName, _analysis, PreviewRows.Select(r => r.Model));
                var reportSize = FileSizeLabel(LastReportPath);
                AddLog(LogLevel.Info, $"Import report saved → {Path.GetFileName(LastReportPath)}  ({reportSize})");
                AddLog(LogLevel.Info, $"Folder: {Path.GetDirectoryName(LastReportPath)}");
            }
            catch (Exception ex)
            {
                AddLog(LogLevel.Warning, $"Could not save the import report: {ex.Message}");
            }

            var level = failed.Count == 0 ? LogLevel.Success : LogLevel.Warning;
            AddLog(level, $"Import finished — {applied.Count} value(s) applied on {_handler.AppliedElementCount} element(s), {failed.Count} failed. Undo with Ctrl+Z (one step).");

            SummaryTitle = $"Last import — {_previewScheduleName}";
            SetCards(SummaryCards,
                new SummaryCard("Values applied", applied.Count, CardTone.Success),
                new SummaryCard("Elements updated", _handler.AppliedElementCount, CardTone.Accent),
                new SummaryCard("Failed", failed.Count, failed.Count > 0 ? CardTone.Danger : CardTone.Neutral),
                new SummaryCard("Not found / dup.", CountNotFound + CountDuplicate, CountNotFound + CountDuplicate > 0 ? CardTone.Warning : CardTone.Neutral));
            StatusMessage = $"Import finished — {applied.Count} applied, {failed.Count} failed.";
        }

        // ══════════════════════════════════════════════════════════════════
        // Preview: filters, selection, counts
        // ══════════════════════════════════════════════════════════════════

        [RelayCommand]
        private void SetPreviewFilter(string filter)
        {
            if (Enum.TryParse(filter, out PreviewFilter f)) ActivePreviewFilter = f;
        }

        partial void OnActivePreviewFilterChanged(PreviewFilter value) => PreviewView.Refresh();
        partial void OnPreviewSearchTextChanged(string value) => PreviewView.Refresh();

        [RelayCommand]
        private void ClearPreviewSearch() => PreviewSearchText = string.Empty;

        private bool MatchesPreviewFilter(object item)
        {
            var r = (ImportChangeRowViewModel)item;
            bool statusOk = ActivePreviewFilter switch
            {
                PreviewFilter.All => true,
                PreviewFilter.Changes => r.Status == ImportChangeStatus.Change,
                PreviewFilter.NotFound => r.Status == ImportChangeStatus.NotFound,
                PreviewFilter.Duplicate => r.Status == ImportChangeStatus.Duplicate,
                PreviewFilter.MissingFromFile => r.Status == ImportChangeStatus.MissingFromFile,
                PreviewFilter.Skipped => r.Status == ImportChangeStatus.ReadOnly || r.Status == ImportChangeStatus.TypeParameter,
                PreviewFilter.Applied => r.Status == ImportChangeStatus.Applied,
                PreviewFilter.Failed => r.Status == ImportChangeStatus.Failed,
                _ => true
            };
            if (!statusOk) return false;
            if (string.IsNullOrWhiteSpace(PreviewSearchText)) return true;

            var t = PreviewSearchText.Trim();
            return r.ElementId.ToString().Contains(t)
                || Contains(r.ParameterName, t) || Contains(r.OldValue, t) || Contains(r.NewValue, t) || Contains(r.Message, t);
        }

        [RelayCommand]
        private void SelectAllVisible()
        {
            foreach (var r in PreviewView.Cast<ImportChangeRowViewModel>().Where(r => r.IsSelectable))
                r.IsSelected = true;
        }

        [RelayCommand]
        private void ClearSelection()
        {
            foreach (var r in PreviewRows) r.IsSelected = false;
        }

        private void PreviewRow_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ImportChangeRowViewModel.IsSelected))
                RecalculateSelection();
        }

        private void RecalculateSelection()
        {
            SelectedChangeCount = PreviewRows.Count(r => r.IsSelected && r.IsSelectable);
            ApplySelectedCommand.NotifyCanExecuteChanged();
        }

        private void RecalculatePreview()
        {
            CountAll = PreviewRows.Count;
            CountChanges = PreviewRows.Count(r => r.Status == ImportChangeStatus.Change);
            CountNotFound = PreviewRows.Count(r => r.Status == ImportChangeStatus.NotFound);
            CountDuplicate = PreviewRows.Count(r => r.Status == ImportChangeStatus.Duplicate);
            CountMissing = PreviewRows.Count(r => r.Status == ImportChangeStatus.MissingFromFile);
            CountSkipped = PreviewRows.Count(r => r.Status == ImportChangeStatus.ReadOnly || r.Status == ImportChangeStatus.TypeParameter);
            CountApplied = PreviewRows.Count(r => r.Status == ImportChangeStatus.Applied);
            CountFailed = PreviewRows.Count(r => r.Status == ImportChangeStatus.Failed);
            RecalculateSelection();

            var a = _analysis;
            if (!HasApplied)
            {
                SetCards(PreviewCards,
                    new SummaryCard("Rows in file", a.RowsInFile),
                    new SummaryCard("Changes", CountChanges, CardTone.Accent),
                    new SummaryCard("Unchanged values", a.UnchangedValues),
                    new SummaryCard("Not found", CountNotFound, CountNotFound > 0 ? CardTone.Warning : CardTone.Neutral),
                    new SummaryCard("Duplicate IDs", CountDuplicate, CountDuplicate > 0 ? CardTone.Danger : CardTone.Neutral),
                    new SummaryCard("Missing from file", CountMissing, CountMissing > 0 ? CardTone.Warning : CardTone.Neutral),
                    new SummaryCard("Ignored (read-only)", CountSkipped));
            }
            else
            {
                SetCards(PreviewCards,
                    new SummaryCard("Rows in file", a.RowsInFile),
                    new SummaryCard("Applied", CountApplied, CardTone.Success),
                    new SummaryCard("Failed", CountFailed, CountFailed > 0 ? CardTone.Danger : CardTone.Neutral),
                    new SummaryCard("Not applied", CountChanges),
                    new SummaryCard("Not found", CountNotFound, CountNotFound > 0 ? CardTone.Warning : CardTone.Neutral),
                    new SummaryCard("Duplicate IDs", CountDuplicate, CountDuplicate > 0 ? CardTone.Danger : CardTone.Neutral),
                    new SummaryCard("Missing from file", CountMissing, CountMissing > 0 ? CardTone.Warning : CardTone.Neutral));
            }
            PreviewView.Refresh();
        }

        [RelayCommand]
        private void CopyNotFoundIds()
        {
            var ids = PreviewRows
                .Where(r => r.Status == ImportChangeStatus.NotFound || r.Status == ImportChangeStatus.MissingFromFile || r.Status == ImportChangeStatus.Duplicate)
                .Select(r => $"{r.ElementId}\t{r.StatusLabel}")
                .Distinct()
                .ToList();
            if (ids.Count == 0) return;
            TrySetClipboard(string.Join(Environment.NewLine, ids));
            AddLog(LogLevel.Info, $"Copied {ids.Count} problem Element ID(s) to the clipboard.");
        }

        [RelayCommand]
        private void OpenReport()
        {
            if (!string.IsNullOrEmpty(LastReportPath) && File.Exists(LastReportPath))
                OpenFile(LastReportPath);
        }

        // ══════════════════════════════════════════════════════════════════
        // Log
        // ══════════════════════════════════════════════════════════════════

        [RelayCommand]
        private void CopyLog()
        {
            TrySetClipboard(string.Join(Environment.NewLine, LogEntries.Select(l => l.ToString())));
            StatusMessage = $"Copied {LogEntries.Count} log line(s) to the clipboard.";
        }

        [RelayCommand]
        private void SaveLog()
        {
            var folder = !string.IsNullOrEmpty(LastExportedFile)
                ? Path.GetDirectoryName(LastExportedFile)
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            try
            {
                var path = LogExportService.Export(ToolName, LogEntries, folder);
                AddLog(LogLevel.Info, $"Log saved → {path}");
                OpenFile(path);
            }
            catch (Exception ex)
            {
                AddLog(LogLevel.Error, $"Could not save the log: {ex.Message}");
            }
        }

        [RelayCommand]
        private void ClearLog() => LogEntries.Clear();

        private void LogSection(string title) => AddLog(LogLevel.Debug, $"──────── {title} ────────");

        private void AddLog(LogLevel level, string message)
        {
            var entry = new LogEntry(level, message);
            if (_dispatcher.CheckAccess()) LogEntries.Add(entry);
            else _dispatcher.Invoke(() => LogEntries.Add(entry));
        }

        // ══════════════════════════════════════════════════════════════════
        // External event plumbing
        // ══════════════════════════════════════════════════════════════════

        private void Raise(ScheduleImportRequest request, string status = null)
        {
            IsBusy = true;
            if (status != null) StatusMessage = status;
            _handler.Request = request;
            _externalEvent.Raise();
        }

        private void OnRequestCompleted()
        {
            _dispatcher.Invoke(() =>
            {
                IsBusy = false;
                if (!_handler.LastRunSucceeded)
                {
                    Fail($"{_handler.Request} failed: {_handler.ErrorMessage}");
                    return;
                }

                switch (_handler.Request)
                {
                    case ScheduleImportRequest.LoadSchedules: CompleteLoadSchedules(); break;
                    case ScheduleImportRequest.ExportSchedule: CompleteExport(); break;
                    case ScheduleImportRequest.AnalyzeImport: CompleteAnalysis(); break;
                    case ScheduleImportRequest.ApplyChanges: CompleteApply(); break;
                }
            });
        }

        private void CompleteLoadSchedules()
        {
            var previous = SelectedSchedule?.ViewId;
            Schedules.Clear();
            foreach (var s in _handler.LoadedSchedules) Schedules.Add(s);
            SelectedSchedule = Schedules.FirstOrDefault(s => s.ViewId == previous);

            AddLog(LogLevel.Info, $"Found {Schedules.Count} schedule(s) in the document.");
            SummaryTitle = "Document";
            SetCards(SummaryCards,
                new SummaryCard("Schedules", Schedules.Count, CardTone.Accent),
                new SummaryCard("Scheduled elements", Schedules.Sum(s => s.RowCount)));
            StatusMessage = Schedules.Count > 0
                ? "Select a schedule, then Export or Import."
                : "No schedules found in this document.";
        }

        // ══════════════════════════════════════════════════════════════════
        // Helpers
        // ══════════════════════════════════════════════════════════════════

        private bool NotBusy() => !IsBusy;
        private bool HasScheduleAndNotBusy() => SelectedSchedule != null && !IsBusy;

        private void RefreshCommandStates()
        {
            ExportScheduleCommand.NotifyCanExecuteChanged();
            ImportScheduleCommand.NotifyCanExecuteChanged();
            RefreshSchedulesCommand.NotifyCanExecuteChanged();
            ApplySelectedCommand.NotifyCanExecuteChanged();
        }

        partial void OnHasAppliedChanged(bool value) => ApplySelectedCommand.NotifyCanExecuteChanged();

        private void Fail(string message)
        {
            IsBusy = false;
            AddLog(LogLevel.Error, message);
            StatusMessage = message;
        }

        private static void SetCards(ObservableCollection<SummaryCard> target, params SummaryCard[] cards)
        {
            target.Clear();
            foreach (var c in cards) target.Add(c);
        }

        private static string Preview(IEnumerable<string> names)
        {
            var list = names.ToList();
            return string.Join(", ", list.Take(8)) + (list.Count > 8 ? $" … (+{list.Count - 8})" : "");
        }

        private static bool Contains(string source, string term)
            => source?.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;

        private void TrySetClipboard(string text)
        {
            try { Clipboard.SetText(text); }
            catch (Exception ex) { AddLog(LogLevel.Warning, $"Clipboard is busy, try again: {ex.Message}"); }
        }

        private static void OpenFile(string path)
            => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });

        private static string SanitizeFileName(string name)
        {
            foreach (var ch in Path.GetInvalidFileNameChars())
                name = name.Replace(ch, '_');
            return name;
        }

        private static string FileSizeLabel(string path)
        {
            try
            {
                long bytes = new FileInfo(path).Length;
                if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F1} MB";
                if (bytes >= 1_024) return $"{bytes / 1_024.0:F1} KB";
                return $"{bytes} B";
            }
            catch { return "?"; }
        }
    }
}
