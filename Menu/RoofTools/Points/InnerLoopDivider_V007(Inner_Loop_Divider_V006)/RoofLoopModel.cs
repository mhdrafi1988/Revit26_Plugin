using Autodesk.Revit.DB;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Revit26_Plugin.InnerLoopDivider.V007.Models
{
    /// <summary>
    /// Represents a single boundary loop extracted from a roof, including its
    /// shape classification, perimeter, and the user's division choices.
    /// </summary>
    public class RoofLoopModel : INotifyPropertyChanged
    {
        /// <summary>Sequential index of the loop as discovered on the roof.</summary>
        public int Index { get; set; }

        /// <summary>Perimeter of the loop in millimetres.</summary>
        public double PerimeterMm { get; set; }

        /// <summary>Boundary role of the loop: <c>Outer</c> or <c>Inner</c>.</summary>
        public string LoopType { get; set; }

        /// <summary>True when the raw geometry was classified as circular.</summary>
        public bool IsCircular { get; set; }

        /// <summary>Raw shape classification: Circular / Rectangle / Other.</summary>
        public string LoopShapeType { get; set; }

        /// <summary>User-facing group for hierarchical UI grouping.</summary>
        public string ShapeCategory => LoopShapeType switch
        {
            "Circular"  => "Circular",
            "Rectangle" => "Rectangular",
            _           => "Other"
        };

        /// <summary>Sort rank: Circular (0), Rectangular (1), Other (2).</summary>
        public int CategoryRank => LoopShapeType switch
        {
            "Circular"  => 0,
            "Rectangle" => 1,
            _           => 2
        };

        private bool _isSelected = true;

        /// <summary>Whether this loop is included when division points are applied.</summary>
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

        private int _recommendedPoints = 6;

        /// <summary>Number of division points to add along this loop.</summary>
        public int RecommendedPoints
        {
            get => _recommendedPoints;
            set
            {
                if (_recommendedPoints != value)
                {
                    _recommendedPoints = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DividedLengthMeters));
                }
            }
        }

        /// <summary>Underlying Revit curve loop geometry for the boundary.</summary>
        public CurveLoop Geometry { get; set; }

        /// <summary>
        /// Computed divided segment length in metres.
        /// Returns "—" if no points are queued.
        /// </summary>
        public string DividedLengthMeters
        {
            get
            {
                if (RecommendedPoints <= 0) return "—";
                double lengthM = (PerimeterMm / RecommendedPoints) / 1000.0;
                return lengthM.ToString("0.00");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
