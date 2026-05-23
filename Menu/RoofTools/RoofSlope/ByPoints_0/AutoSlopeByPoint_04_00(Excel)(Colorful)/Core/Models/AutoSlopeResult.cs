// =======================================================
// File: AutoSlopeResult.cs
// Location: Core/Models/
// Purpose: Plain result object returned by the engine.
//          No UI, no WPF, no ViewModel references.
//          The ViewModel subscribes via OnCompleted callback
//          in AutoSlopePayload and reads from this object.
// =======================================================

namespace Revit26_Plugin.AutoSlopeByPoint_04.Core.Models
{
    public class AutoSlopeResult
    {
        /// <summary>True if the engine completed without fatal errors.</summary>
        public bool Success { get; set; }

        /// <summary>Human-readable error message when Success is false.</summary>
        public string ErrorMessage { get; set; }

        public int    VerticesProcessed   { get; set; }
        public int    VerticesSkipped     { get; set; }
        public int    DrainCount          { get; set; }
        public double HighestElevation_mm { get; set; }
        public double LongestPath_m       { get; set; }
        public int    RunDuration_sec     { get; set; }
        public string RunDate             { get; set; }
    }
}
