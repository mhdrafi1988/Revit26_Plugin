using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.RRLPV4.Models
{
    /// <summary>
    /// Holds all data results for the roof processing operation.
    /// Pure model - no UI, no Revit calls. V4 adds DetailType, Division, EdgePoints.
    /// </summary>
    public class RoofProcessingData
    {
        /// <summary>Input Data</summary>
        public RoofBase SelectedRoof { get; set; }
        public XYZ Point1 { get; set; }
        public XYZ Point2 { get; set; }
        public DetailLineType UsedDetailType { get; set; }
        public string UsedDivisionStrategy { get; set; } // "Divide by 2/3/5"
        public double PointIntervalMeters { get; set; } = 1.0;

        /// <summary>Minimum distance allowed between P1 and P2 (meters)</summary>
        public double MinPointDistanceMeters { get; set; } = 1.0;

        /// <summary>Operation Timing</summary>
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;

        /// <summary>Results Counters</summary>
        public int DetailLinesCreated { get; set; }
        public int PerpendicularLinesCreated { get; set; }
        public int EdgePointsAdded { get; set; } // V4: Replaces ShapePointsAdded (T2 specific)
        public bool IsSuccess { get; set; }

        /// <summary>Live Logs (for UI binding)</summary>
        public List<string> LogMessages { get; } = new List<string>();

        /// <summary>Add timestamped log entry</summary>
        public void AddLog(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogMessages.Add($"{timestamp} - {message}");
        }

        /// <summary>Summary string for UI</summary>
        public string GetSummary()
        {
            return $"T1: {DetailLinesCreated} details + {PerpendicularLinesCreated} perps | " +
                   $"T2: {EdgePointsAdded} edge points | " +
                   $"Duration: {Duration:mm\\:ss} | {(IsSuccess ? "SUCCESS" : "ERRORS")}";
        }

        /// <summary>Validate inputs</summary>
        public bool IsValid()
        {
            return SelectedRoof != null &&
                   Point1 != null && Point2 != null &&
                   Point1.DistanceTo(Point2) >= MinPointDistanceMeters;
        }
    }
}