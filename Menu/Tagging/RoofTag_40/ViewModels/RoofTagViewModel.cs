using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RoofTagV3.Models;
using RoofTagV3.Utilities;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace RoofTagV3.ViewModels
{
    public class RoofTagViewModel : INotifyPropertyChanged
    {
        private readonly UIApplication _uiApplication;
        private readonly Document _document;
        private readonly LiveLogger _logger;

        private bool _useManualMode = false;
        public bool UseManualMode
        {
            get => _useManualMode;
            set { _useManualMode = value; OnPropertyChanged(); }
        }

        private double _selectedAngle = 45.0;
        public double SelectedAngle
        {
            get => _selectedAngle;
            set { _selectedAngle = value; OnPropertyChanged(); }
        }

        private bool _bendInward = true;
        public bool BendInward
        {
            get => _bendInward;
            set { _bendInward = value; OnPropertyChanged(); }
        }

        private double _bendOffsetMillimeters = 1000.0;
        public double BendOffsetMillimeters
        {
            get => _bendOffsetMillimeters;
            set
            {
                if (value >= 0)
                {
                    _bendOffsetMillimeters = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(BendOffsetFeet));
                }
            }
        }

        private double _endOffsetMillimeters = 2000.0;
        public double EndOffsetMillimeters
        {
            get => _endOffsetMillimeters;
            set
            {
                if (value >= 0)
                {
                    _endOffsetMillimeters = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(EndOffsetFeet));
                }
            }
        }

        public double BendOffsetFeet => UnitUtils.ConvertFromInternalUnits(
            BendOffsetMillimeters / 304.8, UnitTypeId.Feet);

        public double EndOffsetFeet => UnitUtils.ConvertFromInternalUnits(
            EndOffsetMillimeters / 304.8, UnitTypeId.Feet);

        private bool _useLeader = true;
        public bool UseLeader
        {
            get => _useLeader;
            set { _useLeader = value; OnPropertyChanged(); }
        }

        public ObservableCollection<SpotTagTypeWrapper> SpotTagTypes { get; private set; }

        private SpotTagTypeWrapper _selectedSpotTagType;
        public SpotTagTypeWrapper SelectedSpotTagType
        {
            get => _selectedSpotTagType;
            set { _selectedSpotTagType = value; OnPropertyChanged(); }
        }

        private string _logText = "";
        public string LogText
        {
            get => _logText;
            set { _logText = value; OnPropertyChanged(); }
        }

        public RoofTagViewModel(UIApplication uiApplication, LiveLogger logger)
        {
            _uiApplication = uiApplication ?? throw new ArgumentNullException(nameof(uiApplication));
            _document = uiApplication.ActiveUIDocument?.Document;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _logger.LogMessageReceived += OnLogMessageReceived;
            LoadTagTypes();
        }

        private void OnLogMessageReceived(object sender, string message)
        {
            LogText += $"{DateTime.Now:HH:mm:ss} - {message}{Environment.NewLine}";
        }

        private void LoadTagTypes()
        {
            if (_document == null) return;

            SpotTagTypes = new ObservableCollection<SpotTagTypeWrapper>(
                new FilteredElementCollector(_document)
                    .OfClass(typeof(SpotDimensionType))
                    .Cast<SpotDimensionType>()
                    .Select(t => new SpotTagTypeWrapper(t))
                    .OrderBy(t => t.Name)
            );

            SelectedSpotTagType = SpotTagTypes.FirstOrDefault();
        }

        public void ClearLog()
        {
            LogText = string.Empty;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}