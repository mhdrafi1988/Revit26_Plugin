using Autodesk.Revit.DB;

namespace Revit26_Plugin.RoofPointComparison.V001.Core.Models
{
    public enum PointComparisonStatus
    {
        /// <summary>Same position (within tolerance) and same elevation (within tolerance) on both roofs.</summary>
        Matched,

        /// <summary>Same position (within tolerance) but elevation differs by more than the tolerance.</summary>
        MismatchedElevation,

        /// <summary>Point exists on Roof A but has no matching position on Roof B.</summary>
        MissingInB,

        /// <summary>Point exists on Roof B but has no matching position on Roof A.</summary>
        MissingInA
    }

    /// <summary>One row of the roof-to-roof point comparison, used for markers and metrics.</summary>
    public class PointComparisonEntry
    {
        public PointComparisonStatus Status { get; set; }

        /// <summary>World position used to place the marker for this entry.</summary>
        public XYZ Position { get; set; }

        public double ElevationA_mm { get; set; }
        public double ElevationB_mm { get; set; }

        /// <summary>ElevationB_mm - ElevationA_mm. Only meaningful for Matched/MismatchedElevation.</summary>
        public double ElevationDiffMm => ElevationB_mm - ElevationA_mm;
    }
}
