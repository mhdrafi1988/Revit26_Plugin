// =======================================================
// File: AutoSlopeResult.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.WithRidge
// Changes:
//   + RidgePointsDetected — count of vertices identified
//     as ridge points during the run. Shown in UI summary
//     and written to the Revit parameter.
// =======================================================

namespace Revit26_Plugin.AutoSlopeByPoint.WithRidge.Core.Models
{
    public class AutoSlopeResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public int VerticesProcessed { get; set; }
        public int VerticesSkipped { get; set; }
        public int PickedDrainCount { get; set; }
        public int FinalDrainCount { get; set; }
        public double HighestElevation_mm { get; set; }
        public double LongestPath_m { get; set; }
        public int RunDuration_sec { get; set; }
        public string RunDate { get; set; }

        /// <summary>
        /// Number of vertices identified as ridge points and assigned
        /// the longest-path elevation instead of the shortest.
        /// </summary>
        public int RidgePointsDetected { get; set; }
    }
}
