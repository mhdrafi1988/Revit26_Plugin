// =======================================================
// File: AutoSlopeResult.cs
// Location: Core/Models/
// Purpose: Plain result object returned by the engine.
//          No UI, no WPF, no ViewModel references.
//          The ViewModel subscribes via OnCompleted callback
//          in AutoSlopePayload and reads from this object.
// Updates:
//   #11 Added AvgSlopePercent — average slope across all
//       processed vertices, computed by the engine.
//   #11 Added Percentage2Applied — reserved for a second
//       slope zone value (engine sets it; UI displays it).
// =======================================================

namespace Revit26_Plugin.AutoSlopeByPoint_04.Core.Models
{
    public class AutoSlopeResult
    {
        /// <summary>True if the engine completed without fatal errors.</summary>
        public bool Success { get; set; }

        /// <summary>Human-readable error message when Success is false.</summary>
        public string ErrorMessage { get; set; }

        public int VerticesProcessed { get; set; }
        public int VerticesSkipped   { get; set; }

        /// <summary>Raw drain count from user selection — before tolerance expansion.</summary>
        public int PickedDrainCount { get; set; }

        /// <summary>Final drain count after tolerance radius is applied.</summary>
        public int FinalDrainCount { get; set; }

        public double HighestElevation_mm { get; set; }
        public double LongestPath_m       { get; set; }
        public int    RunDuration_sec     { get; set; }
        public string RunDate             { get; set; }

        /// <summary>
        /// Average slope percentage applied across all processed vertices.
        /// Computed by the engine as (sum of elevationOffset / pathLength) * 100
        /// and averaged. Zero when no vertices were processed.
        /// </summary>
        public double AvgSlopePercent { get; set; }

        /// <summary>
        /// Secondary slope percentage (e.g. for a second drainage zone).
        /// Reserved for future engine logic; set to 0.0 when not used.
        /// </summary>
        public double Percentage2Applied { get; set; }
    }
}
