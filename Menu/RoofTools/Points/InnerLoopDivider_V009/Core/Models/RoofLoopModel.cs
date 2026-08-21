// =======================================================
// File: RoofLoopModel.cs
// Location: Core/Models/
// Changes vs V007:
//   Converted from hand-rolled INotifyPropertyChanged to
//   CommunityToolkit.Mvvm's ObservableObject + [ObservableProperty],
//   matching this suite's stack convention (see AutoSlopeByDrain's
//   DrainItem for the same pattern). Behavior identical — IsSelected
//   and RecommendedPoints still raise change notifications on every
//   set so the grid checkbox/inputs and the ViewModel's counts stay
//   in sync.
// =======================================================

using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.InnerLoopDivider.V009.Core.Models
{
    /// <summary>
    /// Represents a single boundary loop extracted from a roof, including its
    /// shape classification, perimeter, and the user's division choices.
    /// </summary>
    public partial class RoofLoopModel : ObservableObject
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

        /// <summary>Whether this loop is included when division points are applied.</summary>
        [ObservableProperty]
        private bool isSelected = true;

        /// <summary>Number of division points to add along this loop.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DividedLengthMeters))]
        private int recommendedPoints = 6;

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
    }
}
