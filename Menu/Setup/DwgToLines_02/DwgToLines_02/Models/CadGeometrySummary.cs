namespace Revit26_Plugin.DwgSymbolicConverter_V02.Models
{
    /// <summary>
    /// Represents a grouped summary row of CAD geometry.
    /// One row = one GeometryType + one LayerName.
    /// </summary>
    public class CadGeometrySummary
    {
        /// <summary>
        /// Geometry classification (Line, Arc, Polyline, etc.)
        /// </summary>
        public string GeometryType { get; set; }

        /// <summary>
        /// CAD layer name.
        /// </summary>
        public string LayerName { get; set; }

        /// <summary>
        /// Number of curves in this group.
        /// </summary>
        public int Count { get; set; }
    }
}
