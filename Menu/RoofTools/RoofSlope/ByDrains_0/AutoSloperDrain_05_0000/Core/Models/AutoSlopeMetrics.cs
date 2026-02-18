using System;

namespace Revit26_Plugin.AutoSlope.V5_00.Core.Models
{
    public class AutoSlopeMetrics
    {
        public int Processed { get; set; }
        public int Skipped { get; set; }
        public double HighestElevation { get; set; }
        public double LongestPath { get; set; }
        public int DurationSeconds { get; set; }
        public string RunDate { get; set; } = DateTime.Now.ToString("dd-MMM-yyyy HH:mm");
    }
}