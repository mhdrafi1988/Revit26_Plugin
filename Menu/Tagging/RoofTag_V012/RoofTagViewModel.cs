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

namespace Revit26_Plugin.RoofTag_V012
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
        private double _bendOffset = 1000.0;
        private double _endOffset = 2000.0;
        private bool _useLeader = true;
        private SpotTagTypeWrapper _selectedSpotTagType;
        private int _placedCount;
        private int _failedCount;

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

        /// <summary>Preset quick-pick values for Bend/End offset editable combo boxes.</summary>
        public double[] PresetOffsetValues { get; } = { 250, 500, 750, 1000 };

        private bool _clusterFilterEnabled = true;
        private double _clusterRadius = 600.0;

        /// <summary>Rule 1 — when true, only one tag is placed per cluster of same-elevation points.</summary>
        public bool ClusterFilterEnabled
        {
            get => _clusterFilterEnabled;
            set => SetProperty(ref _clusterFilterEnabled, value);
        }

        /// <summary>Rule 1 cluster radius in mm (horizontal). Elevation tolerance is fixed at 5 mm.</summary>
        public double ClusterRadius
        {
            get => _clusterRadius;
            set => SetProperty(ref _clusterRadius, value);
        }

        public bool UseLeader
        {
            get => _useLeader;
            set => SetProperty(ref _useLeader, value);
        }

        public ObservableCollection<SpotTagTypeWrapper> SpotTagTypes { get; } = new();

        public SpotTagTypeWrapper SelectedSpotTagType
        {
            get => _selectedSpotTagType;
            set => SetProperty(ref _selectedSpotTagType, value);
        }

        public ObservableCollection<LogEntry> LogEntries { get; } = new();

        public int PlacedCount
        {
            get => _placedCount;
            set => SetProperty(ref _placedCount, value);
        }

        public int FailedCount
        {
            get => _failedCount;
            set => SetProperty(ref _failedCount, value);
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
        }

        public void ResetCounters()
        {
            PlacedCount = 0;
            FailedCount = 0;
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
