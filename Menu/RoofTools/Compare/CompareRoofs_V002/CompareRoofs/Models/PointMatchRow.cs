using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.RoofPointElevationSync.V002
{
    /// <summary>
    /// One row of the point mapping grid: a pair of shape-edit points on Roof A / Roof B
    /// that coincide in XY within the user tolerance.
    /// </summary>
    public partial class PointMatchRow : ObservableObject
    {
        [ObservableProperty]
        private bool isIncluded;

        public string PointId { get; set; }

        public double X { get; set; }
        public double Y { get; set; }

        public double ElevationA { get; set; }
        public double ElevationB { get; set; }

        /// <summary>Reference to the actual Revit shape-edit point on Roof A.</summary>
        public object RevitPointA { get; set; }

        /// <summary>Reference to the actual Revit shape-edit point on Roof B.</summary>
        public object RevitPointB { get; set; }

        public double HigherElevation => ElevationA >= ElevationB ? ElevationA : ElevationB;

        public bool IsEqual => System.Math.Abs(ElevationA - ElevationB) < 1e-6;

        /// <summary>"Roof A", "Roof B", or "Equal — no change".</summary>
        public string TargetLabel
        {
            get
            {
                if (IsEqual) return "Equal — no change";
                return ElevationA < ElevationB ? "Roof A" : "Roof B";
            }
        }

        public double? NewElevation => IsEqual ? (double?)null : HigherElevation;
    }
}
