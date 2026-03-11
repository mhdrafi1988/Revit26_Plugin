// File: DrainVertexData.cs
// Location: Revit26_Plugin.Asd_19.Models

using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.Asd_19.Models
{
    public class DrainVertexData
    {
        public int VertexIndex { get; set; }
        public XYZ Position { get; set; }
        public double PathLengthMeters { get; set; }
        public double ElevationOffsetMm { get; set; }
        public int NearestDrainId { get; set; }
        public XYZ DirectionVector { get; set; }
        public bool WasProcessed { get; set; }
        public string DrainSize { get; set; }
        public string DrainShape { get; set; }

        public string Direction =>
            DirectionVector != null ?
            $"{DirectionVector.X:F3},{DirectionVector.Y:F3},{DirectionVector.Z:F3}" :
            "0,0,0";
    }

    public class DrainExportMetrics
    {
        public int ProcessedVertices { get; set; }
        public int SkippedVertices { get; set; }
        public int DrainCount { get; set; }
        public double HighestElevationMm { get; set; }
        public double LongestPathM { get; set; }
        public double SlopePercent { get; set; }
        public int RunDurationSec { get; set; }
        public string RunDate { get; set; }
        public string RoofId { get; set; }
        public string RoofName { get; set; }
    }
}