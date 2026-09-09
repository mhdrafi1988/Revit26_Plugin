using Autodesk.Revit.DB;

namespace Revit26_Plugin.RoofPointComparison.V001.Core.Models
{
    /// <summary>One shape-editing point read from a roof's SlabShapeEditor.</summary>
    public class RoofPointSample
    {
        public XYZ Position { get; set; }

        /// <summary>Position.Z converted to millimeters — the point's elevation value.</summary>
        public double ElevationMm { get; set; }
    }
}
