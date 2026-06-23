using Autodesk.Revit.DB;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Revit26_Plugin.Tools.DivideInnerLoops.V002.Models
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
        public string LoopType { get; set; } // Outer / Inner

        /// <summary>True when the raw geometry was classified as circular.</summary>
        public bool IsCircular { get; set; }

        /// <summary>Raw shape classification from the geometry service: Circular / Rectangle / Other.</summary>
        public string LoopShapeType { get; set; } // Circular / Rectangle / Other

        /// <summary>
        /// User-facing primary group for the loop, used for hierarchical grouping
        /// in the UI: <c>Circular</c>, <c>Rectangular</c>, or <c>Other</c>.
        /// </summary>
        public string ShapeCategory => LoopShapeType switch
        {
            "Circular"  => "Circular",
            "Rectangle" => "Rectangular",
            _           => "Other"
        };

        /// <summary>
        /// Sort rank that fixes group display order to Circular (0),
        /// Rectangular (1), then Other (2), independent of alphabetical order.
        /// </summary>
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

        private int _recommendedPoints = 3;

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
        /// Computed divided length of each segment in metres, rounded to 2 decimal places.
        /// Calculated as <c>Perimeter / RecommendedPoints</c>. Returns "—" if no points are queued.
        /// </summary>
        public string DividedLengthMeters
        {
            get
            {
                if (RecommendedPoints <= 0)
                    return "—";

                double lengthMm = PerimeterMm / RecommendedPoints;
                double lengthM = lengthMm / 1000.0;
                return lengthM.ToString("0.00");
            }
        }

        /// <summary>Raised when a bindable property value changes.</summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Raises <see cref="PropertyChanged"/> for the given member.</summary>
        /// <param name="propertyName">Name of the changed property; supplied automatically.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
