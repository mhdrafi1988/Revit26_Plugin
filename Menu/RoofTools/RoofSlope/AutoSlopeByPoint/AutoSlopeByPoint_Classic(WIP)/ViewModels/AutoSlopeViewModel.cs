using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlopeByPoint_WIP2.ExternalEvents;
using Revit26_Plugin.AutoSlopeByPoint_WIP2.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Revit26_Plugin.AutoSlopeByPoint_WIP2.ViewModels
{
    public class AutoSlopeViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // =====================================================
        // PROPERTIES
        // =====================================================

        // Slope Options
        public List<double> SlopeOptions { get; } = new List<double> { 0.5, 1.0, 1.5, 2.0, 2.5 };

        // Input Properties
        private double _slopePercent = 1.5;
        public double SlopePercent
        {
            get => _slopePercent;
            set
            {
                if (_slopePercent != value)
                {
                    _slopePercent = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _thresholdMeters = 50;
        public int ThresholdMeters
        {
            get => _thresholdMeters;
            set
            {
                if (_thresholdMeters != value)
                {
                    _thresholdMeters = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _exportFolderPath;
        public string ExportFolderPath
        {
            get => _exportFolderPath;
            set
            {
                if (_exportFolderPath != value)
                {
                    _exportFolderPath = value;
                    OnPropertyChanged();
                }
            }
        }

        // Live Log Properties
        private ObservableCollection<string> _logMessages = new ObservableCollection<string>();
        public ObservableCollection<string> LogMessages
        {
            get => _logMessages;
            set
            {
                _logMessages = value;
                OnPropertyChanged();
            }
        }

        private string _logText;
        public string LogText
        {
            get => _logText;
            set
            {
                if (_logText != value)
                {
                    _logText = value;
                    OnPropertyChanged();
                }
            }
        }

        // Result Properties
        private int _verticesProcessed;
        public int VerticesProcessed
        {
            get => _verticesProcessed;
            set
            {
                if (_verticesProcessed != value)
                {
                    _verticesProcessed = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SummaryText));
                }
            }
        }

        private int _verticesSkipped;
        public int VerticesSkipped
        {
            get => _verticesSkipped;
            set
            {
                if (_verticesSkipped != value)
                {
                    _verticesSkipped = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SummaryText));
                }
            }
        }

        private int _drainCount;
        public int DrainCount
        {
            get => _drainCount;
            set
            {
                if (_drainCount != value)
                {
                    _drainCount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SummaryText));
                }
            }
        }

        private double _highestElevationMm;
        public double HighestElevationMm
        {
            get => _highestElevationMm;
            set
            {
                if (_highestElevationMm != value)
                {
                    _highestElevationMm = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HighestElevationDisplay));
                    OnPropertyChanged(nameof(SummaryText));
                }
            }
        }

        public string HighestElevationDisplay => $"{HighestElevationMm:0} mm";

        private double _longestPathM;
        public double LongestPathM
        {
            get => _longestPathM;
            set
            {
                if (_longestPathM != value)
                {
                    _longestPathM = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(LongestPathDisplay));
                    OnPropertyChanged(nameof(SummaryText));
                }
            }
        }

        public string LongestPathDisplay => $"{LongestPathM:0.00} m";

        private int _runDurationSec;
        public int RunDurationSec
        {
            get => _runDurationSec;
            set
            {
                if (_runDurationSec != value)
                {
                    _runDurationSec = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RunDurationDisplay));
                    OnPropertyChanged(nameof(SummaryText));
                }
            }
        }

        public string RunDurationDisplay => $"{RunDurationSec} sec";

        private string _runDate;
        public string RunDate
        {
            get => _runDate;
            set
            {
                if (_runDate != value)
                {
                    _runDate = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SummaryText));
                }
            }
        }

        // Summary Text
        public string SummaryText =>
$@"Vertices Processed : {VerticesProcessed}
Vertices Skipped   : {VerticesSkipped}
Drain Count        : {DrainCount}
Highest Elevation  : {HighestElevationDisplay}
Longest Path       : {LongestPathDisplay}
Run Duration       : {RunDurationDisplay}
Run Date           : {RunDate}";

        // Command State
        private bool _hasRun;
        public bool HasRun
        {
            get => _hasRun;
            set
            {
                if (_hasRun != value)
                {
                    _hasRun = value;
                    OnPropertyChanged();
                }
            }
        }

        // =====================================================
        // COMMANDS
        // =====================================================
        public ICommand RunCommand { get; }
        public ICommand BrowseFolderCommand { get; }

        // =====================================================
        // SERVICES & CONTEXT
        // =====================================================
        private readonly UIDocument _uidoc;
        private readonly UIApplication _app;
        private readonly ElementId _roofId;
        private readonly List<XYZ> _drainPoints;
        private readonly ILogService _logService;

        // =====================================================
        // CONSTRUCTOR
        // =====================================================
        public AutoSlopeViewModel(
            UIDocument uidoc,
            UIApplication app,
            ElementId roofId,
            List<XYZ> drainPoints,
            ILogService logService)
        {
            _uidoc = uidoc;
            _app = app;
            _roofId = roofId;
            _drainPoints = drainPoints;
            _logService = logService;

            // Initialize log service with callback
            _logService.Initialize(AddLogMessage);

            // Initialize export folder
            ExportFolderPath = Services.CsvExportService.GetDefaultExportFolder();

            // Initialize commands
            RunCommand = new RelayCommand(
                _ => RunAutoSlope(),
                _ => !HasRun);

            BrowseFolderCommand = new RelayCommand(_ => BrowseExportFolder());

            // Initialize Event Manager
            AutoSlopeEventManager.Initialize();
        }

        // =====================================================
        // METHODS
        // =====================================================
        private void AddLogMessage(string message)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                LogMessages.Add(message);
                LogText = string.Join(Environment.NewLine, LogMessages);

                // Auto-scroll to bottom
                OnPropertyChanged(nameof(LogText));
            });
        }

        private void BrowseExportFolder()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select CSV Export Folder",
                SelectedPath = ExportFolderPath,
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ExportFolderPath = dialog.SelectedPath;
            }
        }

        private void RunAutoSlope()
        {
            if (HasRun)
                return;

            HasRun = true;

            AddLogMessage("?? Starting AutoSlope Engine...");

            AutoSlopeHandler.Payload = new AutoSlopePayload
            {
                RoofId = _roofId,
                DrainPoints = _drainPoints,
                SlopePercent = SlopePercent,
                ThresholdMeters = ThresholdMeters,
                ExportFolderPath = ExportFolderPath,
                ViewModel = this,
                LogCallback = AddLogMessage
            };

            AutoSlopeEventManager.RaiseEvent();
        }
    }
}