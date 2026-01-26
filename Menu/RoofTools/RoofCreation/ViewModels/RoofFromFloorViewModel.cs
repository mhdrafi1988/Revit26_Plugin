using System;
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
        private const bool DebugMode = true;

        private readonly UIApplication _uiApp;
        private readonly Window _window;

        private readonly ExternalEvent _roofSelectEvent;
        private readonly RoofSelectionHandler _roofSelectHandler;

        private readonly ExternalEvent _linkSelectEvent;
        private readonly LinkSelectionHandler _linkSelectHandler;

        private RelayCommand _startCommand;

        private FootPrintRoof _selectedRoof;
        private RevitLinkInstance _selectedLink;

        private RoofMemoryContext _roofContext;
        private List<ProfileLoop> _floorProfiles = new();
        private List<CurveLoop> _cleanLoops = new();

        public RoofFromFloorViewModel(UIApplication uiApp, Window window)
        {
            _uiApp = uiApp;
            _window = window;

            _roofSelectHandler = new RoofSelectionHandler { ViewModel = this };
            _roofSelectEvent = ExternalEvent.Create(_roofSelectHandler);

            _linkSelectHandler = new LinkSelectionHandler { ViewModel = this };
            _linkSelectEvent = ExternalEvent.Create(_linkSelectHandler);

            UpdateActiveViewStatus();
            Log("UI loaded. Switch to a Plan View, then select a roof.");
        }

        // ================= UI PROPERTIES =================

        [ObservableProperty] private string activeViewName = "Unknown";
        [ObservableProperty] private string selectedRoofName = "No roof selected";
        [ObservableProperty] private string selectedLinkName = "No link selected";
        [ObservableProperty] private string logText = "";

        [ObservableProperty] private Brush viewStatusColor = Brushes.Orange;
        [ObservableProperty] private Brush roofStatusColor = Brushes.Orange;

        [ObservableProperty] private bool isPlanViewValid;
        [ObservableProperty] private bool isRoofSelected;
        [ObservableProperty] private bool canStart;

        // ================= COMMANDS =================

        public ICommand SelectRoofCommand => new RelayCommand(() =>
        {
            Log("Launching roof selection...");
            _window.Hide();
            _roofSelectEvent.Raise();
        });

        public ICommand SelectLinkCommand => new RelayCommand(() =>
        {
            Log("Launching link selection...");
            _window.Hide();
            _linkSelectEvent.Raise();
        });

        public ICommand StartCommand =>
            _startCommand ??= new RelayCommand(OnStart, () => CanStart);

        public ICommand CloseCommand =>
            new RelayCommand(() => _window.Close());

        // ================= CALLBACKS =================

        public void SetSelectedRoof(RoofBase roof)
        {
            ShowWindow();

            if (roof is not FootPrintRoof fpRoof)
            {
                Log("? Only FootPrintRoof is supported.");
                return;
            }

            _selectedRoof = fpRoof;

            Document doc = _uiApp.ActiveUIDocument.Document;
            _roofContext = ProfileExtractor.ExtractRoofContext(doc, fpRoof);

            SelectedRoofName = fpRoof.Name;
            RoofStatusColor = Brushes.Green;
            IsRoofSelected = true;

            Log("Roof selected successfully.");
            Log($"Roof footprint curves: {_roofContext.RoofFootprintCurves.Count}");

            UpdateCanStart();
        }

        public void SetSelectedLink(RevitLinkInstance link)
        {
            ShowWindow();

            _selectedLink = link;
            SelectedLinkName = link.Name;

            Log($"Link selected: {link.Name}");

            UpdateCanStart();
        }

        public void ShowWindow()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _window.Show();
                _window.Activate();
            });
        }

        public void LogFromExternal(string message)
        {
            Application.Current.Dispatcher.Invoke(() => Log(message));
        }

        // ================= MAIN =================

        private void OnStart()
        {
            if (_roofContext == null || _selectedLink == null)
            {
                Log("? Missing roof or link context.");
                return;
            }

            Document doc = _uiApp.ActiveUIDocument.Document;

            Log("Extracting floor profiles...");
            _floorProfiles = FloorProfileService.ExtractFloorProfilesFromLink(
                doc,
                _selectedLink,
                _roofContext.BoundingBox,
                _roofContext.RoofLevel.Elevation + _roofContext.RoofBaseElevation);

            Log($"Floor profiles: {_floorProfiles.Count}");

            Log("Cleaning geometry...");
            _cleanLoops = ProfileCleaner.CleanAndBuildLoops(
                _roofContext.RoofFootprintCurves,
                _floorProfiles);

            Log($"Closed loops: {_cleanLoops.Count}");

            if (_cleanLoops.Count == 0)
            {
                Log("? No valid closed loops.");
                return;
            }

            RoofType roofType =
                doc.GetElement(_selectedRoof.GetTypeId()) as RoofType;

            bool success = RoofCreationService.TryCreateFootprintRoof(
                doc,
                _cleanLoops,
                roofType,
                _roofContext.RoofLevel,
                Log);

            if (!success && DebugMode)
            {
                Log("?? Dumping debug geometry...");
                View view = _uiApp.ActiveUIDocument.ActiveView;

                CurveDumpService.DumpCurves(doc, view,
                    _roofContext.RoofFootprintCurves, "DEBUG_Roof");

                CurveDumpService.DumpCurves(doc, view,
                    _floorProfiles.SelectMany(p => p.Curves), "DEBUG_Floors");

                CurveDumpService.DumpCurves(doc, view,
                    _cleanLoops.SelectMany(l => l), "DEBUG_Cleaned");
            }
        }

        // ================= HELPERS =================

        private void UpdateActiveViewStatus()
        {
            View view = _uiApp.ActiveUIDocument?.ActiveView;
            ActiveViewName = view?.Name ?? "No Active View";

            IsPlanViewValid = view is ViewPlan;
            ViewStatusColor = IsPlanViewValid ? Brushes.Green : Brushes.Red;
        }

        private void UpdateCanStart()
        {
            CanStart = IsPlanViewValid && IsRoofSelected && _selectedLink != null;
            _startCommand?.NotifyCanExecuteChanged(); // ?? FIX
        }

        private void Log(string message)
        {
            LogText += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
        }
    }
}
