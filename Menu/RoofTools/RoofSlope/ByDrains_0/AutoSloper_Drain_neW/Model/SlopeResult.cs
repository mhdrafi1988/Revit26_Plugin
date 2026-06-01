// Models/SlopeResult.cs
using System;

namespace Revit26_Plugin.AutoSlopeByDrain_21.Models
{
    public class SlopeResult
    {
        public double LongestPathMeters { get; set; }
        public double HighestElevationMm { get; set; }
        public double AvgSlopePercent { get; set; }
        public int VerticesProcessed { get; set; }
        public int VerticesSkipped { get; set; }
        public double SlopePercent1 { get; set; }
        public double SlopePercent2 { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }

        // Added for export
        public double RunDuration_sec { get; set; }
        public string RunDate { get; set; }

        public SlopeResult()
        {
            Success = true;
            RunDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}