using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.RoofFromFloor.V010.ExternalEvents;
using Revit26_Plugin.RoofFromFloor.V010.Geometry;
using Revit26_Plugin.RoofFromFloor.V010.Models;
using Revit26_Plugin.RoofFromFloor.V010.Services;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoofFromFloor.V010.ViewModels
{
    public partial class RoofFromFloorViewModel : ObservableObject
    {
        // ─── Private state ────────────────────────────────────────────────
        private readonly UIApplication _uiApp;
        private readonly Window        _window;

        private readonly ExternalEvent _roofSelectEvent;
        private readonly ExternalEvent _linkSelectEvent;
        private readonly ExternalEvent _roofCreateEvent;

        private RelayCommand _startCommand;

        private FootPrintRoof       _selectedRoof;
        private RevitLinkInstance   _selectedLink;
        private RoofMemoryContext   _roofContext;
        private List<ProfileLoop>   _floorProfiles = new();
        private List<CurveLoop>     _cleanLoops    = new();

        // ─── Public log collection (replaces plain string LogText) ────────
        /// <summary>
        /// Bound to the log ItemsControl. Each item is a typed LogEntry with Level-aware colour.
        /// </summary>
        public ObservableCollection<LogEntry> LogEntries { get; } = new();

        // ─── Accessors for external event handlers ────────────────────────
        public RoofMemoryContext  RoofContext    => _roofContext;
        public FootPrintRoof      SelectedRoof   => _selectedRoof;
        public List<ProfileLoop>  FloorProfiles  => _floorProfiles;
        public List<CurveLoop>    CleanLoops     => _cleanLoops;

        // ─── Observable properties ────────────────────────────────────────
        [ObservableProperty] private string activeViewName;
        [ObservableProperty] private string selectedRoofName = "No roof selected";
        [ObservableProperty] private string selectedLinkName = "No link selected";
        [ObservableProperty] private Brush  viewStatusColor;
        [ObservableProperty] private Brush  roofStatusColor  = Brushes.Orange;
        [ObservableProperty] private bool   isPlanViewValid;
        [ObservableProperty] private bool   isRoofSelected;
        [ObservableProperty] private bool   canStart;

        /// <summary>Summary badge after a run ("✅ 12 curves created").</summary>
        [ObservableProperty] private string summaryText;
        [ObservableProperty] private bool   hasSummary;

        // ─── Constructor ──────────────────────────────────────────────────
        public RoofFromFloorViewModel(UIApplication app, Window window)
        {
            _uiApp  = app;
            _window = window;

            _roofSelectEvent = ExternalEvent.Create(new RoofSelectionHandler  { ViewModel = this });
            _linkSelectEvent = ExternalEvent.Create(new LinkSelectionHandler  { ViewModel = this });
            _roofCreateEvent = ExternalEvent.Create(new RoofCreationHandler   { ViewModel = this });

            UpdateActiveViewStatus();
            Log(LogLevel.Info, "UI loaded. Switch to a Plan View, then select a roof.");
        }

        // ─── Commands ─────────────────────────────────────────────────────
        public ICommand SelectRoofCommand => new RelayCommand(() =>
        {
            Log(LogLevel.Info, "Launching roof selection…");
            _window.Hide();
            _roofSelectEvent.Raise();
        });

        public ICommand SelectLinkCommand => new RelayCommand(() =>
        {
            Log(LogLevel.Info, "Launching link selection…");
            _window.Hide();
            _linkSelectEvent.Raise();
        });

        public ICommand StartCommand =>
            _startCommand ??= new RelayCommand(OnStart, () => CanStart);

        public ICommand CloseCommand =>
            new RelayCommand(() => _window.Close());

        public ICommand CopyLogCommand => new RelayCommand(() =>
        {
            string text = string.Join("\n", LogEntries.Select(e => e.ToString()));
            Clipboard.SetText(text);
            Log(LogLevel.Info, "Log copied to clipboard.");
        });

        public ICommand ClearLogCommand => new RelayCommand(() =>
        {
            LogEntries.Clear();
            HasSummary = false;
        });

        // ─── Public callbacks (called from ExternalEventHandlers) ─────────
        public void SetSelectedRoof(FootPrintRoof roof)
        {
            ShowWindow();
            _selectedRoof  = roof;
            _roofContext   = ProfileExtractor.ExtractRoofContext(
                _uiApp.ActiveUIDocument.Document, roof);

            SelectedRoofName = roof.Name;
            RoofStatusColor  = Brushes.Green;
            IsRoofSelected   = true;

            Log(LogLevel.Success, $"Roof selected: {roof.Name}  |  Footprint curves: {_roofContext.RoofFootprintCurves.Count}");
            UpdateCanStart();
        }

        public void SetSelectedLink(RevitLinkInstance link)
        {
            ShowWindow();
            _selectedLink    = link;
            SelectedLinkName = link.Name;

            Log(LogLevel.Success, $"Link selected: {link.Name}");
            UpdateCanStart();
        }

        /// <summary>Thread-safe log from ExternalEventHandlers.</summary>
        public void LogFromExternal(string msg, LogLevel level = LogLevel.Info)
            => Log(level, msg);

        public void SetSummary(int curveCount)
        {
            SummaryText = $"✅  {curveCount} detail curves created";
            HasSummary  = true;
        }

        public void ShowWindow()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _window.Show();
                _window.Activate();
            });
        }

        // ─── Private helpers ──────────────────────────────────────────────
        private void OnStart()
        {
            Log(LogLevel.Info, "Extracting floor profiles from linked model…");

            _floorProfiles = FloorProfileService.ExtractFloorProfilesFromLink(
                _uiApp.ActiveUIDocument.Document,
                _selectedLink,
                _roofContext.BoundingBox,
                _roofContext.RoofLevel.Elevation + _roofContext.RoofBaseElevation);

            Log(LogLevel.Info, $"Floor profiles found: {_floorProfiles.Count}");
            Log(LogLevel.Info, "Cleaning and building closed loops…");

            _cleanLoops = ProfileCleaner.CleanAndBuildLoops(
                _roofContext.RoofFootprintCurves,
                _floorProfiles);

            Log(LogLevel.Info, $"Closed loops ready: {_cleanLoops.Count}");

            _window.Hide();
            _roofCreateEvent.Raise();
        }

        private void UpdateActiveViewStatus()
        {
            var view         = _uiApp.ActiveUIDocument.ActiveView;
            ActiveViewName   = view.Name;
            IsPlanViewValid  = view is ViewPlan;
            ViewStatusColor  = IsPlanViewValid ? Brushes.Green : Brushes.Red;
        }

        private void UpdateCanStart()
        {
            CanStart = IsPlanViewValid && IsRoofSelected && _selectedLink != null;
            _startCommand?.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Central log writer. Always marshals to UI thread.
        /// </summary>
        private void Log(LogLevel level, string msg)
        {
            var entry = new LogEntry(level, msg);

            if (Application.Current.Dispatcher.CheckAccess())
                LogEntries.Add(entry);
            else
                Application.Current.Dispatcher.Invoke(() => LogEntries.Add(entry));
        }
    }
}
