using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V004
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly Document _hostDoc;
        private readonly RunCreateElementsExternalEventHandler _handler;
        private readonly ExternalEvent _externalEvent;
        private readonly CancelFlag _cancelFlag = new();

        public ObservableCollection<LinkedDocumentOption> LinkedDocuments { get; } = new();
        public ObservableCollection<LinkInstanceOption> AvailableInstances { get; } = new();
        public ObservableCollection<RoomCandidate> Rooms { get; } = new();
        public ObservableCollection<RoomCandidate> FilteredRooms { get; } = new();
        public ObservableCollection<HostLevelOption> HostLevels { get; } = new();
        public ObservableCollection<FloorType> FloorTypes { get; } = new();
        public ObservableCollection<RoofType> RoofTypes { get; } = new();
        public ObservableCollection<LogEntry> Logs { get; } = new();

        [ObservableProperty] private LinkedDocumentOption selectedLinkedDocument;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RunFloorCommand))]
        [NotifyCanExecuteChangedFor(nameof(RunRoofCommand))]
        private LinkInstanceOption selectedInstance;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RunFloorCommand))]
        private FloorType selectedFloorType;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RunRoofCommand))]
        private RoofType selectedRoofType;

        [ObservableProperty] private RoomCandidate selectedRoom;

        [ObservableProperty] private string filterText = "";

        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private bool isLoading;
        [ObservableProperty] private string floorSummaryText = "";
        [ObservableProperty] private string roofSummaryText = "";
        [ObservableProperty] private string toastMessage = "";
        public bool HasFloorSummary => !string.IsNullOrEmpty(FloorSummaryText);
        public bool HasRoofSummary => !string.IsNullOrEmpty(RoofSummaryText);
        public bool HasToast => !string.IsNullOrEmpty(ToastMessage);

        partial void OnFloorSummaryTextChanged(string value) => OnPropertyChanged(nameof(HasFloorSummary));
        partial void OnRoofSummaryTextChanged(string value) => OnPropertyChanged(nameof(HasRoofSummary));
        partial void OnToastMessageChanged(string value) => OnPropertyChanged(nameof(HasToast));

        partial void OnFilterTextChanged(string value) => ApplyFilter();

        [ObservableProperty] private int totalCount;
        [ObservableProperty] private int processedCount;
        [ObservableProperty] private string progressText = "";

        public MainViewModel(Document hostDoc, RunCreateElementsExternalEventHandler handler, ExternalEvent externalEvent)
        {
            _hostDoc = hostDoc;
            _handler = handler;
            _externalEvent = externalEvent;

            // NOTE (fix #10, carried from V003): Revit's API is not thread-safe outside the
            // API execution context, so this can't be pushed onto a background Task — genuine
            // async loading isn't available here. IsLoading at least gives the user a visible
            // "working" state instead of the window silently freezing during the scan.
            IsLoading = true;
            LoadHostLevels();
            LoadLinkedDocuments();
            LoadFloorTypes();
            LoadRoofTypes();
            IsLoading = false;
        }

        private void LoadHostLevels()
        {
            HostLevels.Clear();
            foreach (var l in LevelMatchingService.GetHostLevels(_hostDoc))
                HostLevels.Add(l);

            if (HostLevels.Count == 0)
                AddLog(LogLevel.Warning, "No levels found in the host model — New Level mapping will be unavailable.");
        }

        private void LoadLinkedDocuments()
        {
            LinkedDocuments.Clear();
            foreach (var d in LinkedRoomService.GetLinkedDocumentsWithRooms(_hostDoc))
                LinkedDocuments.Add(d);

            if (LinkedDocuments.Count == 0)
                AddLog(LogLevel.Warning, "No linked files with rooms were found in this model.");
        }

        private void LoadFloorTypes()
        {
            var types = new FilteredElementCollector(_hostDoc)
                .OfClass(typeof(FloorType))
                .Cast<FloorType>()
                .OrderBy(t => t.Name);

            foreach (var t in types) FloorTypes.Add(t);
            SelectedFloorType = FloorTypes.FirstOrDefault();
        }

        private void LoadRoofTypes()
        {
            var types = new FilteredElementCollector(_hostDoc)
                .OfClass(typeof(RoofType))
                .Cast<RoofType>()
                .OrderBy(t => t.Name);

            foreach (var t in types) RoofTypes.Add(t);
            SelectedRoofType = RoofTypes.FirstOrDefault();
        }

        partial void OnSelectedLinkedDocumentChanged(LinkedDocumentOption value)
        {
            AvailableInstances.Clear();
            ClearRooms();
            if (value == null) return;

            foreach (var inst in value.Instances)
                AvailableInstances.Add(inst);

            SelectedInstance = AvailableInstances.Count == 1 ? AvailableInstances[0] : null;
        }

        partial void OnSelectedInstanceChanged(LinkInstanceOption value)
        {
            ClearRooms();
            if (value == null || SelectedLinkedDocument == null) return;

            var found = LinkedRoomService.GetAllRooms(SelectedLinkedDocument.LinkDocument, value);
            foreach (var r in found)
            {
                // Auto-match Linked File Level -> New Level by name (case-insensitive,
                // trimmed). Left null ("— unmapped —" in the grid) when no match exists —
                // never defaulted to any level, per confirmed spec.
                r.SelectedHostLevel = LevelMatchingService.FindMatch(r.LinkedLevelName, HostLevels);

                r.PropertyChanged += RoomCandidate_PropertyChanged;
                Rooms.Add(r);
            }

            ApplyFilter();

            // State persistence: auto-select the first item so the user has immediate
            // context on load, per confirmed spec.
            SelectedRoom = FilteredRooms.FirstOrDefault();

            int unmatched = found.Count(r => r.SelectedHostLevel == null);
            if (found.Count == 0)
                AddLog(LogLevel.Warning, "No rooms were found in this link.");
            else
                AddLog(LogLevel.Info, $"Loaded {found.Count} room(s) across {found.Select(r => r.LinkedLevelName).Distinct().Count()} linked level(s).");

            if (unmatched > 0)
                AddLog(LogLevel.Warning, $"{unmatched} room(s) had no matching host level — mapped manually via New Level.");
        }

        private void ClearRooms()
        {
            foreach (var r in Rooms) r.PropertyChanged -= RoomCandidate_PropertyChanged;
            Rooms.Clear();
            FilteredRooms.Clear();
            SelectedRoom = null;
        }

        private void RoomCandidate_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RoomCandidate.IsSelected))
            {
                RunFloorCommand.NotifyCanExecuteChanged();
                RunRoofCommand.NotifyCanExecuteChanged();
            }
        }

        private void ApplyFilter()
        {
            FilteredRooms.Clear();
            IEnumerable<RoomCandidate> source = Rooms;

            if (!string.IsNullOrWhiteSpace(FilterText))
            {
                string f = FilterText.Trim();
                source = source.Where(r =>
                    (r.RoomName?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (r.RoomNumber?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (r.LinkedLevelName?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            foreach (var r in source) FilteredRooms.Add(r);
        }

        /// <summary>Select All / Clear are scoped to the currently visible/filtered rows,
        /// per DataGrid spec item 5.</summary>
        [RelayCommand]
        private void SelectAll() { foreach (var r in FilteredRooms) r.IsSelected = true; }

        [RelayCommand]
        private void DeselectAll() { foreach (var r in FilteredRooms) r.IsSelected = false; }

        [RelayCommand]
        private void Refresh()
        {
            if (SelectedInstance != null) OnSelectedInstanceChanged(SelectedInstance);
        }

        private bool CanRunFloor() => !IsBusy && SelectedInstance != null && SelectedFloorType != null
            && Rooms.Any(r => r.IsSelected);

        private bool CanRunRoof() => !IsBusy && SelectedInstance != null && SelectedRoofType != null
            && Rooms.Any(r => r.IsSelected);

        [RelayCommand(CanExecute = nameof(CanRunFloor))]
        private void RunFloor() => StartRun(CreationMode.Floor, SelectedFloorType.Id);

        [RelayCommand(CanExecute = nameof(CanRunRoof))]
        private void RunRoof() => StartRun(CreationMode.Roof, SelectedRoofType.Id);

        private void StartRun(CreationMode mode, ElementId typeId)
        {
            var allSelected = Rooms.Where(r => r.IsSelected).ToList();

            // Unmapped rows are skipped (logged as Warning) and the rest of the run
            // proceeds, per confirmed spec.
            var runnable = allSelected.Where(r => r.IsMapped).ToList();
            var unmapped = allSelected.Where(r => !r.IsMapped).ToList();

            foreach (var r in unmapped)
                AddLog(LogLevel.Warning, $"{r.DisplayName} — skipped: no New Level mapped.");

            if (runnable.Count == 0)
            {
                AddLog(LogLevel.Warning, "No mapped rooms to process — run cancelled.");
                return;
            }

            IsBusy = true;
            _cancelFlag.IsCancelled = false;

            TotalCount = runnable.Count;
            ProcessedCount = 0;
            ProgressText = $"Processing 0 of {TotalCount}";

            _handler.PendingRequest = new CreateRunRequest
            {
                Mode = mode,
                Rooms = runnable,
                LinkTransform = SelectedInstance.Transform,
                TypeId = typeId,
                Cancel = _cancelFlag
            };

            // Unmapped count is known before the handler even runs (computed above from
            // the checked-but-unmapped rows), so it's passed through rather than recomputed
            // on the Revit API thread.
            _handler.UnmappedSkippedCount = unmapped.Count;

            _externalEvent.Raise();
        }

        private bool CanCancel() => IsBusy;

        [RelayCommand(CanExecute = nameof(CanCancel))]
        private void Cancel() => _cancelFlag.IsCancelled = true;

        /// <summary>Called by the handler on the Revit API thread; ObservableCollection
        /// updates are safe here since the handler runs on Revit's idling/API context,
        /// same thread that owns the modeless window's dispatcher in this pattern.</summary>
        public void AddLog(LogLevel level, string message) => Logs.Add(new LogEntry(level, message));

        public void ReportProgress(int processed)
        {
            ProcessedCount = processed;
            ProgressText = $"Processing {processed} of {TotalCount}";
        }

        public void OnRunComplete(CreationMode mode, RunSummary summary, bool wasCancelled)
        {
            IsBusy = false;
            ProgressText = wasCancelled ? "Cancelled" : "Completed";

            string text = $"success {summary.SuccessCount} | trimmed/fixed {summary.TrimmedFixedCount} | "
                + $"failed {summary.FailedCount} | inner loops skipped {summary.InnerLoopsSkippedCount} | "
                + $"unmapped skipped {summary.UnmappedSkippedCount}";

            if (mode == CreationMode.Floor) FloorSummaryText = text;
            else RoofSummaryText = text;

            RunFloorCommand.NotifyCanExecuteChanged();
            RunRoofCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
        }

        /// <summary>Shows a Task Dialog for a transaction-level failure (spec: critical
        /// errors must surface a dialog, not just a log line). Safe to call from the
        /// handler since it runs on the same thread as the Revit UI.</summary>
        public void ShowCriticalError(string reason)
        {
            TaskDialog.Show("Floors and Roofs From Linked Rooms",
                $"The operation could not complete cleanly.\n\n{reason}");
        }

        [RelayCommand]
        private void CopyFloorSummary()
        {
            if (!HasFloorSummary) return;
            Clipboard.SetText(FloorSummaryText);
            ShowToast("Floor summary copied to clipboard");
        }

        [RelayCommand]
        private void CopyRoofSummary()
        {
            if (!HasRoofSummary) return;
            Clipboard.SetText(RoofSummaryText);
            ShowToast("Roof summary copied to clipboard");
        }

        [RelayCommand]
        private void CopyAllLogs()
        {
            var sb = new StringBuilder();
            foreach (var l in Logs) sb.AppendLine(l.ToString());
            Clipboard.SetText(sb.ToString());
            ShowToast("Logs copied to clipboard");
        }

        /// <summary>Called from code-behind after Copy Selected too, so both copy paths
        /// give the same feedback.</summary>
        public void ShowToast(string message)
        {
            ToastMessage = message;
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (s, e) => { ToastMessage = ""; timer.Stop(); };
            timer.Start();
        }

        [RelayCommand]
        private void ClearLogs()
        {
            if (Logs.Count == 0) return;
            var result = MessageBox.Show("Clear all logs?", "Floors and Roofs From Linked Rooms",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes) Logs.Clear();
        }

        [RelayCommand]
        private void ExportLogs()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Text file (*.txt)|*.txt",
                FileName = $"FloorsAndRoofFromLinkedRooms_Logs_{DateTime.Now:yyyy-MM-dd_HH-mm}.txt"
            };
            if (dialog.ShowDialog() != true) return;

            var sb = new StringBuilder();
            foreach (var l in Logs) sb.AppendLine(l.ToString());
            sb.AppendLine();
            if (HasFloorSummary) sb.AppendLine("Floor run: " + FloorSummaryText);
            if (HasRoofSummary) sb.AppendLine("Roof run: " + RoofSummaryText);

            File.WriteAllText(dialog.FileName, sb.ToString());
        }

        // Copy Selected is handled in code-behind (DataGrid/list selection is a UI concern),
        // per the suite convention — see FloorsFromLinkedRoomsWindow.xaml.cs.
    }
}
