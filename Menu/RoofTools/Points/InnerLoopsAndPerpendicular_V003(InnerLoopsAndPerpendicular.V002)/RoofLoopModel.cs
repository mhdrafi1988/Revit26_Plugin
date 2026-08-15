using Autodesk.Revit.DB;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Revit26_Plugin.InnerLoopsAndPerpendicular.V003.Models
{
    /// <summary>
    /// Represents a single boundary loop extracted from a roof, including its
    /// shape classification and perimeter. Used purely as the selectable shape
    /// source for the Perpendicular Border Point tool.
    /// </summary>
    public class RoofLoopModel : INotifyPropertyChanged
    {
        /// <summary>Sequential index of the loop as discovered on the roof.</summary>
        public int Index { get; set; }

        /// <summary>Perimeter of the loop in millimetres.</summary>
        public double PerimeterMm { get; set; }

        /// <summary>Boundary role of the loop: <c>Outer</c> or <c>Inner</c>.</summary>
        public string LoopType { get; set; }

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

        /// <summary>Whether this loop is included when perpendicular points are generated.</summary>
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

        /// <summary>Underlying Revit curve loop geometry for the boundary.</summary>
        public CurveLoop Geometry { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
