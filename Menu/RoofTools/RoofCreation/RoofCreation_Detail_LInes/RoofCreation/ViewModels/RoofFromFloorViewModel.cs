using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.RoofFromFloor.ExternalEvents;
using Revit26_Plugin.RoofFromFloor.Geometry;
using Revit26_Plugin.RoofFromFloor.Models;
using Revit26_Plugin.RoofFromFloor.Services;

namespace Revit26_Plugin.RoofFromFloor.ViewModels
{
    public partial class RoofFromFloorViewModel : ObservableObject
    {
        public bool DebugMode => true;

        private readonly UIApplication _uiApp;
        private readonly Window _window;
        private readonly UiLogService _log;

        private RelayCommand _startCommand;

        private readonly ExternalEvent _roofSelectEvent;
        private readonly ExternalEvent _linkSelectEvent;
        private readonly ExternalEvent _roofCreateEvent;

        public RoofMemoryContext RoofContext => _roofContext;
        public FootPrintRoof SelectedRoof => _selectedRoof;
        public List<ProfileLoop> FloorProfiles => _floorProfiles;
        public List<CurveLoop> CleanLoops => _cleanLoops;

        private FootPrintRoof _selectedRoof;
        private RevitLinkInstance _selectedLink;
        private RoofMemoryContext _roofContext;
        private List<ProfileLoop> _floorProfiles = new();
        private List<CurveLoop> _cleanLoops = new();

        public RoofFromFloorViewModel(UIApplication app, Window window)
        {
            _uiApp = app;
            _window = window;

            _log = new UiLogService(
                Application.Current.Dispatcher,
                msg => LogText += msg + "\n");

            _roofSelectEvent = ExternalEvent.Create(
                new RoofSelectionHandler { ViewModel = this });

            _linkSelectEvent = ExternalEvent.Create(
                new LinkSelectionHandler { ViewModel = this });

            _roofCreateEvent = ExternalEvent.Create(
                new RoofCreationHandler { ViewModel = this });

            UpdateActiveViewStatus();
            _log.Info("UI loaded. Switch to a Plan View, then select a roof.");
        }

        // ---------- UI PROPERTIES ----------

        [ObservableProperty] private string activeViewName;
        [ObservableProperty] private string selectedRoofName = "No roof selected";
        [ObservableProperty] private string selectedLinkName = "No link selected";
        [ObservableProperty] private string logText = "";

        [ObservableProperty] private Brush viewStatusColor;
        [ObservableProperty] private Brush roofStatusColor = Brushes.Orange;

        [ObservableProperty] private bool isPlanViewValid;
        [ObservableProperty] private bool isRoofSelected;
        [ObservableProperty] private bool canStart;

        // ---------- COMMANDS ----------

        public ICommand SelectRoofCommand => new RelayCommand(() =>
        {
            _log.Info("Launching roof selection...");
            _window.Hide();
            _roofSelectEvent.Raise();
        });

        public ICommand SelectLinkCommand => new RelayCommand(() =>
        {
            _log.Info("Launching link selection...");
            _window.Hide();
            _linkSelectEvent.Raise();
        });

        public ICommand StartCommand =>
            _startCommand ??= new RelayCommand(OnStart, () => CanStart);

        public ICommand CloseCommand =>
            new RelayCommand(() => _window.Close());

        // ---------- CALLBACKS ----------

        public void SetSelectedRoof(FootPrintRoof roof)
        {
            ShowWindow();
            _selectedRoof = roof;
            _roofContext = ProfileExtractor.ExtractRoofContext(
                _uiApp.ActiveUIDocument.Document, roof);

            SelectedRoofName = roof.Name;
            RoofStatusColor = Brushes.Green;
            IsRoofSelected = true;

            _log.Info($"Roof selected. Footprint curves: {_roofContext.RoofFootprintCurves.Count}");
            UpdateCanStart();
        }

        public void SetSelectedLink(RevitLinkInstance link)
        {
            ShowWindow();
            _selectedLink = link;
            SelectedLinkName = link.Name;

            _log.Info($"Link selected: {link.Name}");
            UpdateCanStart();
        }

        public void LogFromExternal(string msg) => _log.Info(msg);

        public void ShowWindow()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _window.Show();
                _window.Activate();
            });
        }

        // ---------- START ----------

        private void OnStart()
        {
            _log.Info("Extracting floor profiles...");

            _floorProfiles = FloorProfileService.ExtractFloorProfilesFromLink(
                _uiApp.ActiveUIDocument.Document,
                _selectedLink,
                _roofContext.BoundingBox,
                _roofContext.RoofLevel.Elevation + _roofContext.RoofBaseElevation);

            _log.Info($"Floor profiles: {_floorProfiles.Count}");

            _log.Info("Cleaning geometry...");
            _cleanLoops = ProfileCleaner.CleanAndBuildLoops(
                _roofContext.RoofFootprintCurves,
                _floorProfiles);

            _log.Info($"Closed loops: {_cleanLoops.Count}");

            _window.Hide();
            _roofCreateEvent.Raise();
        }

        // ---------- HELPERS ----------

        private void UpdateActiveViewStatus()
        {
            var view = _uiApp.ActiveUIDocument.ActiveView;
            ActiveViewName = view.Name;

            IsPlanViewValid = view is ViewPlan;
            ViewStatusColor = IsPlanViewValid ? Brushes.Green : Brushes.Red;
        }

        private void UpdateCanStart()
        {
            CanStart = IsPlanViewValid && IsRoofSelected && _selectedLink != null;
            _startCommand?.NotifyCanExecuteChanged();
        }
    }
}
