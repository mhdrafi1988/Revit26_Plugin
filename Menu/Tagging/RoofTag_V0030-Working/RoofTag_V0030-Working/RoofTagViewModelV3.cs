using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Data;

namespace Revit22_Plugin.RoofTagV3
{
    public class RoofTagViewModelV3 : INotifyPropertyChanged
    {
        private readonly UIApplication _uiApp;
        private readonly Document _doc;

        // ------------------------------
        // MODE: AUTO / MANUAL
        // ------------------------------
        private bool _useManualMode = false;
        public bool UseManualMode
        {
            get => _useManualMode;
            set { _useManualMode = value; OnPropertyChanged(); }
        }

        // ------------------------------
        // ANGLE: 30° / 45°
        // ------------------------------
        private bool _isAngle45 = true; // default
        public bool IsAngle45
        {
            get => _isAngle45;
            set
            {
                _isAngle45 = value;
                if (value) IsAngle30 = false;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedAngle));
            }
        }

        private bool _isAngle30 = false;
        public bool IsAngle30
        {
            get => _isAngle30;
            set
            {
                _isAngle30 = value;
                if (value) IsAngle45 = false;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedAngle));
            }
        }

        public double SelectedAngle => IsAngle45 ? 45.0 : 30.0;

        // ------------------------------
        // BEND DIRECTION: IN / OUT
        // ------------------------------
        private bool _bendInward = true;
        public bool BendInward
        {
            get => _bendInward;
            set
            {
                _bendInward = value;
                if (value) BendOutward = false;
                OnPropertyChanged();
            }
        }

        private bool _bendOutward = false;
        public bool BendOutward
        {
            get => _bendOutward;
            set
            {
                _bendOutward = value;
                if (value) BendInward = false;
                OnPropertyChanged();
            }
        }

        // ------------------------------
        // OFFSETS (mm)
        // ------------------------------
        private double _bendOffset = 1000.0; // mm default
        public double BendOffset
        {
            get => _bendOffset;
            set { _bendOffset = value; OnPropertyChanged(); OnPropertyChanged(nameof(BendOffsetFt)); }
        }

        private double _endOffset = 2000.0; // mm default
        public double EndOffset
        {
            get => _endOffset;
            set { _endOffset = value; OnPropertyChanged(); OnPropertyChanged(nameof(EndOffsetFt)); }
        }

        public double BendOffsetFt => UnitUtils.ConvertToInternalUnits(BendOffset, UnitTypeId.Millimeters);
        public double EndOffsetFt => UnitUtils.ConvertToInternalUnits(EndOffset, UnitTypeId.Millimeters);

        // ------------------------------
        // LEADER
        // ------------------------------
        private bool _useLeader = true;
        public bool UseLeader
        {
            get => _useLeader;
            set { _useLeader = value; OnPropertyChanged(); }
        }

        // ------------------------------
        // TAG TYPES
        // ------------------------------
        public ObservableCollection<SpotTagTypeWrapperV3> SpotTagTypes { get; set; }
        private SpotTagTypeWrapperV3 _selectedSpotTagType;
        public SpotTagTypeWrapperV3 SelectedSpotTagType
        {
            get => _selectedSpotTagType;
            set { _selectedSpotTagType = value; OnPropertyChanged(); }
        }

        // ------------------------------
        // CONSTRUCTOR
        // ------------------------------
        public RoofTagViewModelV3(UIApplication uiApp)
        {
            _uiApp = uiApp;
            _doc = uiApp.ActiveUIDocument.Document;

            LoadTagTypes();
        }

        private void LoadTagTypes()
        {
            SpotTagTypes = new ObservableCollection<SpotTagTypeWrapperV3>(
                new FilteredElementCollector(_doc)
                    .OfClass(typeof(SpotDimensionType))
                    .Cast<SpotDimensionType>()
                    .Select(t => new SpotTagTypeWrapperV3(t))
            );

            SelectedSpotTagType = SpotTagTypes.FirstOrDefault();
        }

        // ------------------------------
        // INotifyPropertyChanged
        // ------------------------------
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    // ========================================================
    // TAG TYPE WRAPPER
    // ========================================================
    public class SpotTagTypeWrapperV3
    {
        public SpotDimensionType TagType { get; }
        public string Name => TagType.Name;

        public SpotTagTypeWrapperV3(SpotDimensionType type)
        {
            TagType = type;
        }

        public override string ToString() => Name;
    }

    // ========================================================
    // INVERSE BOOLEAN CONVERTER (Self-contained)
    // ========================================================
    public class InverseBoolConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return !(bool)value;
        }
    }
}
