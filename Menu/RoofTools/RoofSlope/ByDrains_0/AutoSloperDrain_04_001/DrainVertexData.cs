// File: DrainVertexData.cs
// Location: Revit26_Plugin.Asd_19.Models
// THIS IS THE ONLY DEFINITION - DELETE ANY OTHER COPIES

using Autodesk.Revit.DB;

namespace Revit26_Plugin.Asd_19.Models
{
    /// <summary>
    /// Data class for vertex information used in CSV export and parameter updates
    /// </summary>
    public class DrainVertexData
    {
        /// <summary>Index of the vertex in the roof's vertex list</summary>
        public int VertexIndex { get; set; }

        /// <summary>3D position of the vertex in Revit internal units (feet)</summary>
        public XYZ Position { get; set; }

        /// <summary>Shortest path distance to nearest drain in meters</summary>
        public double PathLengthMeters { get; set; }

        /// <summary>Calculated elevation offset in millimeters</summary>
        public double ElevationOffsetMm { get; set; }

        /// <summary>ID of the nearest drain (1-based for readability)</summary>
        public int NearestDrainId { get; set; }

        /// <summary>Direction vector from vertex to drain (normalized)</summary>
        public XYZ DirectionVector { get; set; }

        /// <summary>Whether this vertex was processed (had a valid path)</summary>
        public bool WasProcessed { get; set; }

        /// <summary>Size category of the nearest drain (e.g., "150 x 150 mm")</summary>
        public string DrainSize { get; set; }

        /// <summary>Shape type of the nearest drain (Circle, Square, Rectangle, etc.)</summary>
        public string DrainShape { get; set; }

        /// <summary>Formatted direction vector for CSV export</summary>
        public string Direction =>
            this.DirectionVector != null ?
            $"{this.DirectionVector.X:F3},{this.DirectionVector.Y:F3},{this.DirectionVector.Z:F3}" :
            "0,0,0";

        /// <summary>Returns a string representation of the vertex data</summary>
        public override string ToString()
        {
            return $"Vertex {VertexIndex}: Path={PathLengthMeters:F2}m, Offset={ElevationOffsetMm:F0}mm, " +
                   $"Drain={NearestDrainId}, Processed={WasProcessed}";
        }
    }

    /// <summary>
    /// Metrics class for export summary and parameter updates
    /// </summary>
    public class DrainExportMetrics
    {
        /// <summary>Number of vertices successfully processed</summary>
        public int ProcessedVertices { get; set; }

        /// <summary>Number of vertices skipped (no path to drain)</summary>
        public int SkippedVertices { get; set; }

        /// <summary>Number of selected drains used in calculation</summary>
        public int DrainCount { get; set; }

        /// <summary>Maximum elevation offset achieved in millimeters</summary>
        public double HighestElevationMm { get; set; }

        /// <summary>Longest drainage path found in meters</summary>
        public double LongestPathM { get; set; }

        /// <summary>Slope percentage used for calculation</summary>
        public double SlopePercent { get; set; }

        /// <summary>Duration of the calculation in seconds</summary>
        public int RunDurationSec { get; set; }

        /// <summary>Date and time when the calculation was performed</summary>
        public string RunDate { get; set; }

        /// <summary>Revit ElementId of the roof</summary>
        public string RoofId { get; set; }

        /// <summary>Name of the roof element</summary>
        public string RoofName { get; set; }

        /// <summary>Returns a formatted summary string</summary>
        public string GetSummaryString()
        {
            return $"Processed: {ProcessedVertices}, Skipped: {SkippedVertices}, " +
                   $"Max Elev: {HighestElevationMm:F0}mm, Longest Path: {LongestPathM:F2}m, " +
                   $"Duration: {RunDurationSec}s";
        }
    }
}