using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Revit26_Plugin.Shared.Models;
using Revit26_Plugin.Shared.Services;

namespace Revit26_Plugin.RoofFromLinkedRooms.V001
{
    /// <summary>View model for the Roof From Linked Rooms window.</summary>
    public partial class MainViewModel : ObservableObject
    {
        private readonly Document _doc;
        private readonly RunCreateRoofsExternalEventHandler _handler;
        private readonly ExternalEvent _externalEvent;
        private readonly ToolSettings _settings;

        /// <summary>Linked documents that contain rooms.</summary>
        public ObservableCollection<LinkedDocumentOption> LinkedDocuments { get; } = new();

        /// <summary>Placed instances of the selected link.</summary>
        public ObservableCollection<LinkInstanceOption> AvailableInstances { get; } = new();

        /// <summary>Host roof types.</summary>
        public ObservableCollection<RoofType> RoofTypes { get; } = new();

        /// <summary>Host levels used for elevation matching.</summary>
        public List<HostLevelOption> HostLevels { get; private set; } = new();

        /// <summary>All rooms of the selected link instance.</summary>
        public ObservableCollection<RoomCandidate> Rooms { get; } = new();

        /// <summary>Rooms after the text filter.</summary>
        public ObservableCollection<RoomCandidate> FilteredRooms { get; } = new();

        /// <summary>Activity log.</summary>
        public ObservableCollection<LogEntry> Logs { get; } = new();

        [ObservableProperty] private LinkedDocumentOption selectedLinkedDocument;
        [ObservableProperty] private LinkInstanceOption selectedInstance;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RunCommand))]
        private RoofType selectedRoofType;

        [ObservableProperty] private string toleranceMmText = "1";
        [ObservableProperty] private string filterText = "";
        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private double progress;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RunCommand))]
        private bool hasRunSinceLastPreview;

        [ObservableProperty] private string summaryText = "";
        [ObservableProperty] private string allCreatedRoofsText = "(populated after Run)";
        [ObservableProperty] private string roofsWithOpeningsText = "(populated after Run)";
        [ObservableProperty] private string toastMessage = "";

        /// <summary>Total rooms loaded.</summary>
        public int MetricTotal => Rooms.Count;

        /// <summary>Rooms ticked.</summary>
        public int MetricSelected => Rooms.Count(r => r.IsSelected);

        /// <summary>Ticked rooms that will produce a roof.</summary>
        public int MetricRoofsToCreate => Rooms.Count(r => r.IsSelected && r.IsMapped);

        /// <summary>Rooms with no matching host level.</summary>
        public int MetricUnmapped => Rooms.Count(r => !r.IsMapped);

        /// <summary>Roofs created in the last run.</summary>
        [ObservableProperty] private int metricCreated;

        /// <summary>Failures in the last run.</summary>
        [ObservableProperty] private int metricFailed;

        /// <summary>Openings cut in the last run.</summary>
        [ObservableProperty] private int metricOpenings;

        /// <summary>Creates the view model; must be called in a Revit API context.</summary>
        public MainViewModel(Document doc, RunCreateRoofsExternalEventHandler handler, ExternalEvent externalEvent)
        {
            _doc = doc;
            _handler = handler;
            _externalEvent = externalEvent;

            _settings = SettingsService.Load(out string msg);
            AddLog(LogLevel.Info, msg);
            toleranceMmText = _settings.ToleranceMm.ToString("0.###");

            HostLevels = LevelMatchingService.GetHostLevels(_doc);
            if (HostLevels.Count == 0) AddLog(LogLevel.Warning, "No levels found in the host model.");

            LoadRoofTypes();
            LoadLinkedDocuments();
        }

        private void LoadRoofTypes()
        {
            foreach (var t in new FilteredElementCollector(_doc).OfClass(typeof(RoofType)).Cast<RoofType>().OrderBy(t => t.Name))
                RoofTypes.Add(t);

            SelectedRoofType = RoofTypes.FirstOrDefault(t => t.Name == _settings.LastRoofTypeName) ?? RoofTypes.FirstOrDefault();
            if (RoofTypes.Count == 0)
                AddLog(LogLevel.Warning, "No RoofType elements found — Run will be unavailable.");
        }

        private void LoadLinkedDocuments()
        {
            var docs = LinkedRoomService.GetLinkedDocumentsWithRooms(_doc);
            foreach (var d in docs) LinkedDocuments.Add(d);

            if (docs.Count == 0)
            {
                AddLog(LogLevel.Warning, "No loaded linked model with rooms was found. Load a link that contains placed rooms.");
                return;
            }

            SelectedLinkedDocument = docs.FirstOrDefault(d => d.DisplayName == _settings.LastLinkName) ?? docs[0];
        }

        private double ToleranceFeet()
        {
            if (!double.TryParse(ToleranceMmText, out double mm) || mm < 0) return 0.0;
            return UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
        }

        partial void OnSelectedLinkedDocumentChanged(LinkedDocumentOption value)
        {
            AvailableInstances.Clear();
            ClearRooms();
            if (value == null) return;
            foreach (var inst in value.Instances) AvailableInstances.Add(inst);
            SelectedInstance = AvailableInstances.FirstOrDefault();
        }

        partial void OnSelectedInstanceChanged(LinkInstanceOption value)
        {
            ClearRooms();
            if (value == null || SelectedLinkedDocument == null) return;

            var found = LinkedRoomService.GetAllRooms(SelectedLinkedDocument.LinkDocument);
            double tol = ToleranceFeet();
            foreach (var r in found)
            {
                r.SelectedHostLevel = LevelMatchingService.FindMatch(r.LinkedLevelElevation, value.Transform, HostLevels, tol);
                r.ResetStatus();
                r.PropertyChanged += Room_PropertyChanged;
                Rooms.Add(r);
            }

            ApplyFilter();
            NotifyMetrics();
            HasRunSinceLastPreview = false;

            AddLog(found.Count == 0 ? LogLevel.Warning : LogLevel.Info,
                found.Count == 0
                    ? "No placed rooms were found in this link."
                    : $"Loaded {found.Count} room(s) from '{value.DisplayName}'; {found.Count(r => !r.IsMapped)} without a host level.");
        }

        partial void OnToleranceMmTextChanged(string value) => RematchAll();
        partial void OnFilterTextChanged(string value) => ApplyFilter();

        private void RematchAll()
        {
            if (Rooms.Count == 0 || SelectedInstance == null) return;
            double tol = ToleranceFeet();
            foreach (var r in Rooms)
                r.SelectedHostLevel = LevelMatchingService.FindMatch(r.LinkedLevelElevation, SelectedInstance.Transform, HostLevels, tol);
            NotifyMetrics();
        }

        private void ClearRooms()
        {
            foreach (var r in Rooms) r.PropertyChanged -= Room_PropertyChanged;
            Rooms.Clear();
            FilteredRooms.Clear();
            NotifyMetrics();
        }

        private void Room_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RoomCandidate.IsSelected) || e.PropertyName == nameof(RoomCandidate.IsMapped))
            {
                NotifyMetrics();
                RunCommand.NotifyCanExecuteChanged();
            }
        }

        private void ApplyFilter()
        {
            FilteredRooms.Clear();
            string f = (FilterText ?? "").Trim();
            foreach (var r in Rooms)
            {
                if (f.Length == 0
                    || r.DisplayName.Contains(f, StringComparison.OrdinalIgnoreCase)
                    || r.LinkedLevelName.Contains(f, StringComparison.OrdinalIgnoreCase))
                    FilteredRooms.Add(r);
            }
        }

        private void NotifyMetrics()
        {
            OnPropertyChanged(nameof(MetricTotal));
            OnPropertyChanged(nameof(MetricSelected));
            OnPropertyChanged(nameof(MetricRoofsToCreate));
            OnPropertyChanged(nameof(MetricUnmapped));
        }

        [RelayCommand]
        private void SelectAllVisible()
        {
            foreach (var r in FilteredRooms) r.IsSelected = true;
        }

        [RelayCommand]
        private void ClearSelection()
        {
            foreach (var r in Rooms) r.IsSelected = false;
        }

        /// <summary>Re-matches levels, resets row statuses and re-enables Run.</summary>
        [RelayCommand]
        private void RefreshPreview()
        {
            RematchAll();
            foreach (var r in Rooms) r.ResetStatus();
            HasRunSinceLastPreview = false;
            SummaryText = "";
            NotifyMetrics();
        }

        private bool CanRun() => !IsBusy && !HasRunSinceLastPreview && SelectedRoofType != null && MetricRoofsToCreate > 0;

        [RelayCommand(CanExecute = nameof(CanRun))]
        private void Run()
        {
            IsBusy = true;
            RunCommand.NotifyCanExecuteChanged();

            _handler.PendingRequest = new CreateRoofsRunRequest
            {
                Rooms = Rooms.Where(r => r.IsSelected && r.IsMapped).ToList(),
                LinkTransform = SelectedInstance.Transform,
                RoofTypeId = SelectedRoofType.Id
            };
            _externalEvent.Raise();
        }

        /// <summary>Called by the handler on the Revit API thread after the run completes.</summary>
        public void OnRunComplete(RunSummary summary, List<ElementId> createdIds, List<ElementId> withOpeningsIds)
        {
            IsBusy = false;
            HasRunSinceLastPreview = true;

            MetricCreated = summary.RoofsCreatedCount;
            MetricFailed = summary.FailedCount;
            MetricOpenings = summary.OpeningsCutCount;

            AllCreatedRoofsText = createdIds.Count > 0 ? string.Join(", ", createdIds.Select(i => i.Value)) : "(none)";
            RoofsWithOpeningsText = withOpeningsIds.Count > 0 ? string.Join(", ", withOpeningsIds.Select(i => i.Value)) : "(none)";

            SummaryText = $"{summary.RoofsCreatedCount} roofs created | {summary.OpeningsCutCount} openings | " +
                          $"{summary.TrimmedFixedCount} trimmed | {summary.FailedCount} failed | {summary.UnmappedSkippedCount} unmapped";
            AddLog(LogLevel.Info, $"Run completed — {SummaryText}");

            SaveSettings("after run");
            RunCommand.NotifyCanExecuteChanged();
        }

        /// <summary>Persists the last link, roof type and tolerance.</summary>
        public void SaveSettings(string reason)
        {
            _settings.LastLinkName = SelectedLinkedDocument?.DisplayName;
            _settings.LastRoofTypeName = SelectedRoofType?.Name;
            if (double.TryParse(ToleranceMmText, out double mm) && mm >= 0) _settings.ToleranceMm = mm;

            bool ok = SettingsService.Save(_settings, out string msg);
            AddLog(ok ? LogLevel.Info : LogLevel.Warning, $"{msg} ({reason}).");
        }

        /// <summary>Appends a line to the activity log.</summary>
        public void AddLog(LogLevel level, string message) => Logs.Add(new LogEntry(level, message));

        /// <summary>Shows a blocking error dialog.</summary>
        public void ShowCriticalError(string reason)
            => TaskDialog.Show(ToolCatalog.RoofFromLinkedRooms.Name, $"The operation could not complete cleanly.\n\n{reason}");

        [RelayCommand]
        private void CopyAllCreatedRoofs() { System.Windows.Clipboard.SetText(AllCreatedRoofsText); ShowToast("Roof IDs copied"); }

        [RelayCommand]
        private void CopyRoofsWithOpenings() { System.Windows.Clipboard.SetText(RoofsWithOpeningsText); ShowToast("Roof-with-openings IDs copied"); }

        [RelayCommand]
        private void CopyAllLogs()
        {
            System.Windows.Clipboard.SetText(string.Join(Environment.NewLine, Logs.Select(l => l.ToString())));
            ShowToast("Logs copied to clipboard");
        }

        /// <summary>Shows a transient message for two seconds.</summary>
        public void ShowToast(string message)
        {
            ToastMessage = message;
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (s, e) => { ToastMessage = ""; timer.Stop(); };
            timer.Start();
        }

        [RelayCommand]
        private void ExportLogs()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Text file (*.txt)|*.txt",
                FileName = $"RoofFromLinkedRooms_Logs_{DateTime.Now:yyyy-MM-dd_HH-mm}.txt"
            };
            if (dialog.ShowDialog() != true) return;

            var sb = new StringBuilder();
            foreach (var l in Logs) sb.AppendLine(l.ToString());
            if (!string.IsNullOrEmpty(SummaryText)) sb.AppendLine().AppendLine("Run summary: " + SummaryText);
            sb.AppendLine("All Created Roofs: " + AllCreatedRoofsText);
            sb.AppendLine("Roofs With Openings: " + RoofsWithOpeningsText);
            File.WriteAllText(dialog.FileName, sb.ToString());
        }
    }
}
