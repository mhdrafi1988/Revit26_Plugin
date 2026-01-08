using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit22_Plugin.RRLPV3.Models
{
    /// <summary>
    /// Holds all data & results for the roof processing operation.
    /// Pure model (no UI, no Revit calls).
    /// </summary>
    public class RoofData
    {
        // -------------------------
        // Input Data
        // -------------------------

        public RoofBase SelectedRoof { get; set; }
        public XYZ Point1 { get; set; }
        public XYZ Point2 { get; set; }

        /// <summary>
        /// Interval between shape points (meters).
        /// </summary>
        public double PointInterval { get; set; } = 1.0;

        /// <summary>
        /// Minimum distance allowed between P1 and P2 (meters).
        /// </summary>
        public double MinPointDistance { get; set; } = 1.0;

        // -------------------------
        // Operation Timing
        // -------------------------

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public TimeSpan Duration => EndTime - StartTime;

        // -------------------------
        // Results
        // -------------------------

        public int DetailLinesCreated { get; set; }
        public int PerpendicularLinesCreated { get; set; }
        public int ShapePointsAdded { get; set; }

        public bool IsSuccess { get; set; }

        // -------------------------
        // Logs
        // -------------------------

        public List<string> LogMessages { get; } = new List<string>();

        public void AddLog(string message)
        {
            LogMessages.Add($"{DateTime.Now:HH:mm:ss} - {message}");
        }
    }
}
