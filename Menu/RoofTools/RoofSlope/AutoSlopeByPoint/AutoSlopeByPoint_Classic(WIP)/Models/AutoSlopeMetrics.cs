namespace Revit26_Plugin.AutoSlopeByPoint_WIP2.Models
{
    public class AutoSlopeMetrics
    {
        public int Processed { get; set; }
        public int Skipped { get; set; }
        public double HighestElevation { get; set; } // in mm
        public double LongestPath { get; set; } // in meters
        public int RunDurationSeconds { get; set; }
    }
}