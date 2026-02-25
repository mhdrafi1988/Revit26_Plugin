using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26.RoofTagV42.Commands;
using Revit26.RoofTagV42.Models;
using Revit26.RoofTagV42.Services;
using Revit26.RoofTagV42.Utilities;
using Revit26.RoofTagV42.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace Revit26.RoofTagV42.ViewModels
{
    public partial class RoofTagViewModel : ObservableObject
    {
        private readonly UIApplication _uiApplication;
        private readonly Document _document;
        private readonly ILiveLogger _logger;
        private readonly RoofBase _selectedRoof;
        private TaggingExternalEventHandler _taggingHandler;
        private ExternalEvent _externalEvent;

        // Observable Properties
        private bool _useManualMode = false;
        public bool UseManualMode
        {
            get => _useManualMode;
            set
            {
                if (SetProperty(ref _useManualMode, value))
                {
                    LogInfo($"Mode changed to: {(value ? "Manual" : "Automatic")}");
                    OnPropertyChanged(nameof(ManualModeButtonText));
                    OnPropertyChanged(nameof(ManualModeStatusText));
                }
            }
        }

        public string ManualModeButtonText => UseManualMode ? "? Manual Mode" : "Select Points";
        public string ManualModeStatusText => UseManualMode ?
            $"Manual: {SelectedPointsCount} point(s) selected" :
            "Automatic: Using roof vertices";

        private List<XYZ> _manualPoints = new List<XYZ>();
        public List<XYZ> ManualPoints
        {
            get => _manualPoints;
            private set
            {
                if (SetProperty(ref _manualPoints, value))
                {
                    SelectedPointsCount = value?.Count ?? 0;
                }
            }
        }

        private int _selectedPointsCount = 0;
        public int SelectedPointsCount
        {
            get => _selectedPointsCount;
            private set => SetProperty(ref _selectedPointsCount, value);
        }

        private double _bendOffsetMillimeters = 1000.0;
        public double BendOffsetMillimeters
        {
            get => _bendOffsetMillimeters;
            set
            {
                if (value >= 0 && value <= 10000 && SetProperty(ref _bendOffsetMillimeters, value))
                {
                    OnPropertyChanged(nameof(BendOffsetFeet));
                    LogInfo($"Bend offset: {value:F0} mm ({BendOffsetFeet:F2} ft)");
                }
            }
        }

        private double _endOffsetMillimeters = 2000.0;
        public double EndOffsetMillimeters
        {
            get => _endOffsetMillimeters;
            set
            {
                if (value >= 0 && value <= 20000 && SetProperty(ref _endOffsetMillimeters, value))
                {
                    OnPropertyChanged(nameof(EndOffsetFeet));
                    LogInfo($"End offset: {value:F0} mm ({EndOffsetFeet:F2} ft)");
                }
            }
        }

        private double _selectedAngle = 45.0;
        public double SelectedAngle
        {
            get => _selectedAngle;
            set
            {
                if (value >= 0 && value <= 180 && SetProperty(ref _selectedAngle, value))
                {
                    LogInfo($"Angle set to: {value}°");
                }
            }
        }

        private bool _bendInward = true;
        public bool BendInward
        {
            get => _bendInward;
            set
            {
                if (SetProperty(ref _bendInward, value))
                {
                    LogInfo($"Bend direction: {(value ? "Inward" : "Outward")}");
                }
            }
        }

        private bool _useLeader = true;
        public bool UseLeader
        {
            get => _useLeader;
            set
            {
                if (SetProperty(ref _useLeader, value))
                {
                    LogInfo($"Use leader: {value}");
                }
            }
        }

        private string _logText = "";
        public string LogText
        {
            get => _logText;
            set => SetProperty(ref _logText, value);
        }

        private bool _autoScroll = true;
        public bool AutoScroll
        {
            get => _autoScroll;
            set
            {
                if (SetProperty(ref _autoScroll, value))
                {
                    LogInfo($"Auto-scroll: {(value ? "Enabled" : "Disabled")}");
                }
            }
        }

        private bool _isProcessing = false;
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (SetProperty(ref _isProcessing, value))
                {
                    OnPropertyChanged(nameof(IsNotProcessing));
                    ExecuteTaggingCommand.NotifyCanExecuteChanged();
                    SelectManualPointsCommand.NotifyCanExecuteChanged();
                    ClearManualPointsCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool IsNotProcessing => !_isProcessing;

        public double BendOffsetFeet => BendOffsetMillimeters.ToFeet();
        public double EndOffsetFeet => EndOffsetMillimeters.ToFeet();

        public ObservableCollection<SpotTagTypeWrapper> SpotTagTypes { get; private set; }

        private SpotTagTypeWrapper _selectedSpotTagType;
        public SpotTagTypeWrapper SelectedSpotTagType
        {
            get => _selectedSpotTagType;
            set
            {
                if (SetProperty(ref _selectedSpotTagType, value) && value != null)
                {
                    LogInfo($"Tag type selected: {value.Name}");
                }
            }
        }

        // Commands
        public IRelayCommand ClearLogCommand { get; private set; }
        public IRelayCommand TestSettingsCommand { get; private set; }
        public IRelayCommand ExecuteTaggingCommand { get; private set; }
        public IRelayCommand SelectManualPointsCommand { get; private set; }
        public IRelayCommand ClearManualPointsCommand { get; private set; }

        public RoofTagViewModel(UIApplication uiApplication, ILiveLogger logger, RoofBase selectedRoof)
        {
            _uiApplication = uiApplication ?? throw new ArgumentNullException(nameof(uiApplication));
            _document = uiApplication.ActiveUIDocument?.Document;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _selectedRoof = selectedRoof;

            _logger.LogMessageReceived += OnLogMessageReceived;

            // Initialize External Event
            _taggingHandler = new TaggingExternalEventHandler(uiApplication, selectedRoof);
            _externalEvent = ExternalEvent.Create(_taggingHandler);

            InitializeCommands();
            LoadTagTypes();
            LogInitialization();
        }

        private void InitializeCommands()
        {
            ClearLogCommand = new RelayCommand(ClearLog, () => !string.IsNullOrEmpty(LogText));
            TestSettingsCommand = new RelayCommand(LogCurrentSettings);
            ExecuteTaggingCommand = new RelayCommand(ExecuteTagging, CanExecuteTagging);
            SelectManualPointsCommand = new RelayCommand(SelectManualPoints, CanSelectManualPoints);
            ClearManualPointsCommand = new RelayCommand(ClearManualPoints, () => ManualPoints.Count > 0 && IsNotProcessing);
        }

        private void LoadTagTypes()
        {
            if (_document == null)
            {
                LogError("No active document found");
                return;
            }

            try
            {
                SpotTagTypes = new ObservableCollection<SpotTagTypeWrapper>(
                    new FilteredElementCollector(_document)
                        .OfClass(typeof(SpotDimensionType))
                        .Cast<SpotDimensionType>()
                        .Select(t => new SpotTagTypeWrapper(t))
                        .OrderBy(t => t.Name)
                );

                SelectedSpotTagType = SpotTagTypes.FirstOrDefault();

                if (SpotTagTypes.Count > 0)
                {
                    LogInfo($"Loaded {SpotTagTypes.Count} tag type(s)");
                }
                else
                {
                    LogWarning("No spot dimension types found in document");
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to load tag types: {ex.Message}");
            }
        }

        private void LogInitialization()
        {
            LogInfo("========================================");
            LogInfo("ROOF TAG V42 - INITIALIZED");
            LogInfo($"Document: {_document?.Title ?? "None"}");

            if (_selectedRoof != null)
            {
                LogInfo($"Selected roof: Element ID {_selectedRoof.Id}");
                LogInfo($"Roof type: {_selectedRoof.Name}");

                var editor = _selectedRoof.GetSlabShapeEditor();
                if (editor != null)
                {
                    LogInfo($"Slab shape editor: {(editor.IsEnabled ? "Enabled" : "Disabled")}");
                }
            }

            LogInfo($"Current view: {_document?.ActiveView?.Name ?? "Unknown"} ({_document?.ActiveView?.ViewType})");
            LogInfo("========================================");
            LogInfo("Configure settings and click 'Run Tagging'");
        }

        private void OnLogMessageReceived(object sender, string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                LogText += $"{DateTime.Now:HH:mm:ss} - {message}{Environment.NewLine}";

                if (AutoScroll)
                {
                    OnPropertyChanged(nameof(LogText));
                }
            });
        }

        private void LogCurrentSettings()
        {
            LogInfo("=== CURRENT SETTINGS ===");
            LogInfo($"Mode: {(UseManualMode ? "Manual" : "Automatic")}");
            LogInfo($"Selected Points: {SelectedPointsCount}");
            LogInfo($"Angle: {SelectedAngle}°");
            LogInfo($"Bend inward: {BendInward}");
            LogInfo($"Bend offset: {BendOffsetMillimeters:F0} mm ({BendOffsetFeet:F2} ft)");
            LogInfo($"End offset: {EndOffsetMillimeters:F0} mm ({EndOffsetFeet:F2} ft)");
            LogInfo($"Use leader: {UseLeader}");
            LogInfo($"Tag type: {SelectedSpotTagType?.Name ?? "None"}");
            LogInfo("=====================");
        }

        private void ClearLog()
        {
            LogText = string.Empty;
            LogInfo("Log cleared");
        }

        private bool CanExecuteTagging()
        {
            return IsNotProcessing && _selectedRoof != null && _document != null;
        }

        private bool CanSelectManualPoints()
        {
            return IsNotProcessing && _selectedRoof != null && _document != null;
        }

        private void SelectManualPoints()
        {
            try
            {
                LogInfo("=== STARTING MANUAL POINT SELECTION ===");
                LogInfo("Click on the roof surface to select points");
                LogInfo("Press ESC key when finished");
                LogInfo("----------------------------------------");

                // Store current window reference
                var window = Application.Current.Windows.OfType<RoofTagWindow>().FirstOrDefault();

                if (window != null)
                {
                    window.Hide();
                }

                // Select points
                var points = ManualSelectionService.SelectManualPoints(_uiApplication.ActiveUIDocument, _selectedRoof);

                if (window != null)
                {
                    window.Show();
                    window.Activate();
                }

                if (points.Count > 0)
                {
                    ManualPoints = points;
                    UseManualMode = true;
                    LogSuccess($"? Selected {points.Count} point(s) on roof surface");

                    // Log point coordinates
                    for (int i = 0; i < points.Count; i++)
                    {
                        LogInfo($"Point {i + 1}: X={points[i].X:F2}', Y={points[i].Y:F2}', Z={points[i].Z:F2}'");
                    }

                    LogInfo("? Manual mode activated");
                    LogInfo("Click 'Run Tagging' to place tags at selected points");
                }
                else
                {
                    LogWarning("No points selected. Manual selection cancelled.");
                    UseManualMode = false;
                }

                LogInfo("=== MANUAL SELECTION COMPLETE ===");
            }
            catch (Exception ex)
            {
                LogError($"Manual selection failed: {ex.Message}");
                LogError($"Stack trace: {ex.StackTrace}");
            }
        }

        private void ClearManualPoints()
        {
            ManualPoints = new List<XYZ>();
            UseManualMode = false;
            LogInfo("Manual points cleared. Switching to automatic mode.");
        }

        private void ExecuteTagging()
        {
            IsProcessing = true;
            LogInfo("========================================");
            LogInfo("STARTING TAG OPERATION");

            try
            {
                if (_selectedRoof == null)
                {
                    LogError("No roof selected for tagging");
                    IsProcessing = false;
                    return;
                }

                List<XYZ> pointsToTag;

                if (UseManualMode && ManualPoints.Count > 0)
                {
                    // Use manually selected points
                    pointsToTag = ManualPoints;
                    LogInfo($"Using {pointsToTag.Count} manually selected points");
                }
                else
                {
                    // Use automatic roof vertices
                    pointsToTag = GeometryService.GetRoofVertices(_selectedRoof);

                    if (pointsToTag.Count == 0)
                    {
                        pointsToTag = GeometryService.GetRoofBoundaryXY(_selectedRoof);
                        LogInfo($"No vertices found. Using {pointsToTag.Count} boundary points");
                    }
                    else
                    {
                        LogInfo($"Using {pointsToTag.Count} automatic roof vertices");
                    }
                }

                if (pointsToTag.Count == 0)
                {
                    LogError("No points available for tagging!");
                    IsProcessing = false;
                    return;
                }

                // Prepare for external event
                _taggingHandler.SetPoints(pointsToTag);
                _taggingHandler.SetViewModel(this);
                var result = _externalEvent.Raise();

                if (result == ExternalEventRequest.Accepted)
                {
                    LogSuccess("? Tagging operation queued successfully");
                    LogInfo("Waiting for Revit to process the request...");
                }
                else
                {
                    IsProcessing = false;
                    LogError("? Failed to queue tagging operation");
                    LogError($"External event result: {result}");
                }
            }
            catch (Exception ex)
            {
                IsProcessing = false;
                LogError($"Tagging failed: {ex.Message}");
                LogError($"Stack trace: {ex.StackTrace}");
            }
        }

        // Call this method from the ExternalEventHandler when tagging is complete
        public void TaggingCompleted(int tagsPlaced, int tagsFailed, int totalPoints)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsProcessing = false;

                LogInfo("========================================");
                LogInfo("TAG OPERATION COMPLETE");
                LogInfo($"Successfully placed: {tagsPlaced} tags");
                LogInfo($"Failed: {tagsFailed} tags");
                LogInfo($"Total points processed: {totalPoints}");

                if (tagsPlaced > 0 && tagsFailed == 0)
                {
                    LogSuccess("? All tags placed successfully!");
                }
                else if (tagsPlaced > 0)
                {
                    LogWarning($"? {tagsPlaced} tags placed, {tagsFailed} failed");
                }
                else
                {
                    LogError("? No tags were placed");
                }

                // Clear manual points after successful tagging
                if (UseManualMode && tagsPlaced > 0)
                {
                    ManualPoints = new List<XYZ>();
                    UseManualMode = false;
                    LogInfo("Manual points cleared. Ready for new selection.");
                }

                LogInfo("========================================");
            });
        }

        public void LogInfo(string message) => _logger.LogInfo(message);
        public void LogWarning(string message) => _logger.LogWarning(message);
        public void LogError(string message) => _logger.LogError(message);
        public void LogSuccess(string message) => _logger.LogSuccess(message);
        public void Log(string message) => _logger.Log(message);
    }
}