using Autodesk.Revit.DB;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Revit26_Plugin.PDCV2.Models
{
    public class RoofLoopModel : INotifyPropertyChanged
    {
        public int Index { get; set; }
        public double PerimeterMm { get; set; }
        public string LoopType { get; set; }       // Outer / Inner
        public bool IsCircular { get; set; }
        public string LoopShapeType { get; set; }  // Circular / Rectangle / Other

        // Circle geometry — populated only for Circular loops
        public XYZ Center { get; set; }
        public double Radius { get; set; }

        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _recommendedPoints = 8;
        public int RecommendedPoints
        {
            get => _recommendedPoints;
            set
            {
                if (_recommendedPoints != value)
                {
                    _recommendedPoints = value;
                    OnPropertyChanged();
                }
            }
        }

        public CurveLoop Geometry { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
