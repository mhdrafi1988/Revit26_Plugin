using System;
using System.Collections.ObjectModel;
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

namespace Revit26_Plugin.FloorsFromLinkedRoomsViaPlanView.V001
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly Document _hostDoc;
        private readonly ViewPlan _planView;
        private readonly RunFloorsExternalEventHandler _handler;
        private readonly ExternalEvent _externalEvent;
        private readonly Level _activeLevel;
        private readonly CancelFlag _cancelFlag = new();

        public ObservableCollection<LinkedDocumentOption> LinkedDocuments { get; } = new();
        public ObservableCollection<LinkInstanceOption> AvailableInstances { get; } = new();
        public ObservableCollection<RoomCandidate> Rooms { get; } = new();
        public ObservableCollection<FloorType> FloorTypes { get; } = new();
        public ObservableCollection<LogEntry> Logs { get; } = new();

        [ObservableProperty] private LinkedDocumentOption selectedLinkedDocument;
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RunCommand))]
        private LinkInstanceOption selectedInstance;
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RunCommand))]
        private FloorType selectedFloorType;
        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private bool isLoading;
        [ObservableProperty] private string summaryText = "";
        [ObservableProperty] private string toastMessage = "";
        public bool HasSummary => !string.IsNullOrEmpty(SummaryText);
        public bool HasToast => !string.IsNullOrEmpty(ToastMessage);

        partial void OnSummaryTextChanged(string value) => OnPropertyChanged(nameof(HasSummary));
        partial void OnToastMessageChanged(string value) => OnPropertyChanged(nameof(HasToast));
        [ObservableProperty] private int totalCount;
        [ObservableProperty] private int processedCount;
        [ObservableProperty] private string progressText = "";

        public MainViewModel(Document hostDoc, ViewPlan planView, RunFloorsExternalEventHandler handler, ExternalEvent externalEvent)
        {
            _hostDoc = hostDoc;
            _planView = planView;
            _handler = handler;
            _externalEvent = externalEvent;
            _activeLevel = planView.GenLevel;

            // NOTE (fix #10): Revit's API is not thread-safe outside the API execution
            // context, so this can't be pushed onto a background Task — genuine async
            // loading isn't available here. IsLoading at least gives the user a visible
            // "working" state instead of the window silently freezing during the scan.
            IsLoading = true;
            LoadLinkedDocuments();
            LoadFloorTypes();
            IsLoading = false;
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

        partial void OnSelectedLinkedDocumentChanged(LinkedDocumentOption value)
        {
            AvailableInstances.Clear();
            foreach (var r in Rooms) r.PropertyChanged -= RoomCandidate_PropertyChanged;
            Rooms.Clear();
            if (value == null) return;

            foreach (var inst in value.Instances)
                AvailableInstances.Add(inst);

            SelectedInstance = AvailableInstances.Count == 1 ? AvailableInstances[0] : null;
        }

        partial void OnSelectedInstanceChanged(LinkInstanceOption value)
        {
            foreach (var r in Rooms) r.PropertyChanged -= RoomCandidate_PropertyChanged;
            Rooms.Clear();
            if (value == null || SelectedLinkedDocument == null) return;

            var found = LinkedRoomService.GetRoomsAtLevel(SelectedLinkedDocument.LinkDocument, value, _activeLevel);
            foreach (var r in found)
            {
                r.PropertyChanged += RoomCandidate_PropertyChanged;
                Rooms.Add(r);
            }

            if (found.Count == 0)
                AddLog(LogLevel.Warning, "No rooms in this link intersect the active view's level.");
        }

        private void RoomCandidate_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RoomCandidate.IsSelected))
                RunCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private void SelectAll() { foreach (var r in Rooms) r.IsSelected = true; }

        [RelayCommand]
        private void DeselectAll() { foreach (var r in Rooms) r.IsSelected = false; }

        private bool CanRun() => !IsBusy && SelectedInstance != null && SelectedFloorType != null
            && Rooms.Any(r => r.IsSelected);

        [RelayCommand(CanExecute = nameof(CanRun))]
        private void Run()
        {
            IsBusy = true;
            _cancelFlag.IsCancelled = false;

            var selected = Rooms.Where(r => r.IsSelected).ToList();
            TotalCount = selected.Count;
            ProcessedCount = 0;
            ProgressText = $"Processing 0 of {TotalCount}";

            _handler.PendingRequest = new FloorRunRequest
            {
                Rooms = selected,
                LinkTransform = SelectedInstance.Transform,
                FloorTypeId = SelectedFloorType.Id,
                TargetLevel = _activeLevel,
                Cancel = _cancelFlag
            };
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

        public void OnRunComplete(RunSummary summary, bool wasCancelled)
        {
            IsBusy = false;
            ProgressText = wasCancelled ? "Cancelled" : "Completed";
            SummaryText = $"success {summary.SuccessCount} | trimmed/fixed {summary.TrimmedFixedCount} | "
                + $"failed {summary.FailedCount} | inner loops skipped {summary.InnerLoopsSkippedCount}";
            RunCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
        }

        /// <summary>Shows a Task Dialog for a transaction-level failure (spec: critical
        /// errors must surface a dialog, not just a log line). Safe to call from the
        /// handler since it runs on the same thread as the Revit UI.</summary>
        public void ShowCriticalError(string reason)
        {
            TaskDialog.Show("Floors From Linked Rooms",
                $"The operation could not complete cleanly.\n\n{reason}");
        }

        [RelayCommand]
        private void CopySummary()
        {
            if (!HasSummary) return;
            Clipboard.SetText(SummaryText);
            ShowToast("Summary copied to clipboard");
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
        /// give the same feedback (fix #19).</summary>
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
            var result = MessageBox.Show("Clear all logs?", "Floors From Linked Rooms",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes) Logs.Clear();
        }

        [RelayCommand]
        private void ExportLogs()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Text file (*.txt)|*.txt",
                FileName = $"FloorsFromLinkedRoomsViaPlanView_Logs_{DateTime.Now:yyyy-MM-dd_HH-mm}.txt"
            };
            if (dialog.ShowDialog() != true) return;

            var sb = new StringBuilder();
            foreach (var l in Logs) sb.AppendLine(l.ToString());
            sb.AppendLine();
            sb.AppendLine(SummaryText);

            File.WriteAllText(dialog.FileName, sb.ToString());
        }

        // Copy Selected is handled in code-behind (DataGrid/list selection is a UI concern),
        // per the suite convention — see FloorsFromLinkedRoomsWindow.xaml.cs.
    }
}
