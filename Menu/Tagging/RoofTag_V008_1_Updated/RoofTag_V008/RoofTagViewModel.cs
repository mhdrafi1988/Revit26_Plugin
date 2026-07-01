using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.Shared.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Revit26_Plugin.RoofTag_V008
{
    public class RoofTagViewModel : ObservableObject, INotifyPropertyChanged
    {
        private readonly UIApplication _uiApp;
        private readonly Document _doc;

        private bool _useManualMode = false;
        private bool _isAngle45 = true;
        private bool _isAngle30 = false;
        private bool _bendInward = true;
        private bool _bendOutward = false;
        private double _bendOffset = 800.0;  // ← UPDATED: 1000.0 → 800.0 (V007 values)
        private double _endOffset = 1500.0;  // ← UPDATED: 2000.0 → 1500.0 (V007 values)
        private bool _useLeader = true;
        private double _clusteringTolerance = 10.0;  // ← User-configurable, default 10mm
        private bool _enableMinimumPointDistance = true;  // ← NEW: Toggle filter on/off
        private double _minimumPointDistance = 500.0;  // ← NEW: Default 500mm, filters input points before processing
        private SpotTagTypeWrapper _selectedSpotTagType;
        private int _totalCount;
        private int _placedCount;
        private int _failedCount;
        private int _removedCount;  // ← Track removed/clustered points

        public bool UseManualMode
        {
            get => _useManualMode;
            set => SetProperty(ref _useManualMode, value);
        }

        public bool IsAngle45
        {
            get => _isAngle45;
            set
            {
                if (SetProperty(ref _isAngle45, value))
                {
                    if (value) IsAngle30 = false;
                    OnPropertyChanged(nameof(SelectedAngle));
                }
            }
        }

        public bool IsAngle30
        {
            get => _isAngle30;
            set
            {
                if (SetProperty(ref _isAngle30, value))
                {
                    if (value) IsAngle45 = false;
                    OnPropertyChanged(nameof(SelectedAngle));
                }
            }
        }

        public double SelectedAngle => IsAngle45 ? 45.0 : 30.0;

        public bool BendInward
        {
            get => _bendInward;
            set
            {
                if (SetProperty(ref _bendInward, value))
                {
                    if (value) BendOutward = false;
                }
            }
        }

        public bool BendOutward
        {
            get => _bendOutward;
            set
            {
                if (SetProperty(ref _bendOutward, value))
                {
                    if (value) BendInward = false;
                }
            }
        }

        public double BendOffset
        {
            get => _bendOffset;
            set => SetProperty(ref _bendOffset, value);
        }

        public double EndOffset
        {
            get => _endOffset;
            set => SetProperty(ref _endOffset, value);
        }

        public bool UseLeader
        {
            get => _useLeader;
            set => SetProperty(ref _useLeader, value);
        }

        /// <summary>
        /// User-configurable clustering tolerance in millimeters.
        /// Points within this distance are considered duplicates and skipped.
        /// </summary>
        public double ClusteringTolerance
        {
            get => _clusteringTolerance;
            set => SetProperty(ref _clusteringTolerance, value);
        }

        /// <summary>
        /// Enable/disable the minimum point distance filter.
        /// When enabled, input points closer than MinimumPointDistance are filtered before processing.
        /// </summary>
        public bool EnableMinimumPointDistance
        {
            get => _enableMinimumPointDistance;
            set => SetProperty(ref _enableMinimumPointDistance, value);
        }

        /// <summary>
        /// Minimum distance (mm) between input points.
        /// Points closer than this are removed from input before any tag placement.
        /// Default: 500 mm. Only active if EnableMinimumPointDistance is true.
        /// </summary>
        public double MinimumPointDistance
        {
            get => _minimumPointDistance;
            set => SetProperty(ref _minimumPointDistance, value);
        }

        public ObservableCollection<SpotTagTypeWrapper> SpotTagTypes { get; } = new();

        public SpotTagTypeWrapper SelectedSpotTagType
        {
            get => _selectedSpotTagType;
            set => SetProperty(ref _selectedSpotTagType, value);
        }

        public ObservableCollection<LogEntry> LogEntries { get; } = new();

        /// <summary>Total points collected (raw input before deduplication).</summary>
        public int TotalCount
        {
            get => _totalCount;
            set => SetProperty(ref _totalCount, value);
        }

        /// <summary>Points successfully placed.</summary>
        public int PlacedCount
        {
            get => _placedCount;
            set => SetProperty(ref _placedCount, value);
        }

        /// <summary>Points that failed to place (transaction errors).</summary>
        public int FailedCount
        {
            get => _failedCount;
            set => SetProperty(ref _failedCount, value);
        }

        /// <summary>Points removed due to clustering/deduplication.</summary>
        public int RemovedCount
        {
            get => _removedCount;
            set => SetProperty(ref _removedCount, value);
        }

        public RoofTagViewModel(UIApplication uiApp)
        {
            _uiApp = uiApp;
            _doc   = uiApp.ActiveUIDocument.Document;
            LoadTagTypes();
        }

        private RelayCommand _copyLogCommand;
        public RelayCommand CopyLogCommand => _copyLogCommand ??= new RelayCommand(CopyLog);

        private void CopyLog()
        {
            if (LogEntries.Count == 0) return;
            var text = string.Join(System.Environment.NewLine,
                LogEntries.Select(e => e.ToString()));
            Clipboard.SetText(text);
        }

        private void LoadTagTypes()
        {
            var types = new FilteredElementCollector(_doc)
                .OfClass(typeof(SpotDimensionType))
                .Cast<SpotDimensionType>()
                .Select(t => new SpotTagTypeWrapper(t));

            foreach (var t in types)
                SpotTagTypes.Add(t);

            SelectedSpotTagType = SpotTagTypes.FirstOrDefault();
        }

        public void AddLog(LogEntry entry)
        {
            LogEntries.Add(entry);

            if (entry.Level == LogLevel.Success) PlacedCount++;
            else if (entry.Level == LogLevel.Error) FailedCount++;
            else if (entry.Level == LogLevel.Warning) RemovedCount++;
        }

        public void ResetCounters()
        {
            TotalCount = 0;
            PlacedCount = 0;
            FailedCount = 0;
            RemovedCount = 0;
            LogEntries.Clear();
        }
    }

    public class SpotTagTypeWrapper
    {
        public SpotDimensionType TagType { get; }
        public string Name => TagType.Name;

        public SpotTagTypeWrapper(SpotDimensionType type)
        {
            TagType = type;
        }

        public override string ToString() => Name;
    }
}
