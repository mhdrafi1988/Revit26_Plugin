using Autodesk.Revit.DB;
using System.ComponentModel;

namespace Revit22_Plugin.PDCV1.Models
{
    public class RoofLoopModel : INotifyPropertyChanged
    {
        public int Index { get; set; }
        public double PerimeterMm { get; set; }
        public string LoopType { get; set; } // Outer / Inner
        public bool IsCircular { get; set; }
        public string LoopShapeType { get; set; } // Circular / Rectangle / Other

        private int _recommendedPoints = 2;
        public int RecommendedPoints
        {
            get => _recommendedPoints;
            set
            {
                if (_recommendedPoints != value)
                {
                    _recommendedPoints = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RecommendedPoints)));
                }
            }
        }

        public CurveLoop Geometry { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
