namespace Revit22_Plugin.V4_02.Domain.Models
{
    public class SlopeResult
    {
        public int VerticesModified { get; set; }
        public int VerticesSkipped { get; set; }
        public double MaxElevationMm { get; set; }
        public double LongestPathMeters { get; set; }

        public override string ToString()
        {
            return
                $"Vertices modified: {VerticesModified}, " +
                $"Max offset: {MaxElevationMm:0.0} mm, " +
                $"Longest path: {LongestPathMeters:0.00} m";
        }
    }
}
