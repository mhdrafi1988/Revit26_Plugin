// File: AutoSlopeDrainResult.cs
// Location: Core/Models/
// Plain result object returned by AutoSlopeDrainEngine. No UI/WPF references.
//
// Base fields ported unchanged from AutoSlopeByDrain V004.
//
// NEW (V005), per Rafi's confirmed decisions on 2026-07-21:
//   Version                — ported from ByPoint's AutoSlopeResult.Version.
//                            Default value is "006" (confirmed by Rafi).
//   TotalDetectedCount /
//   SelectedCount           — replaces ByPoint's PickedDrainCount/FinalDrainCount
//                             pattern, adapted for drain-detection workflow:
//                             TotalDetectedCount = every drain the grid found;
//                             SelectedCount = how many were checked at Run time.
//   ArcsCalculated          — boundary/opening arc count when curve-intersection
//                             insertion is enabled; 0 when the feature is off.
//   UnreachableVertexCount  — subset of VerticesSkipped that were skipped because
//                             they had no path to any selected drain at all
//                             (disconnected graph), not just "too far" — see
//                             AutoSlopeDrainEngine's Max Edge Distance handling.
//
// RENAMED: ExportedDetailedFilePath / ExportedSummaryFilePath (CSV, two files)
// collapsed into a single ExportedFilePath, since export is now one Excel
// workbook, not two CSVs.

namespace Revit26_Plugin.AutoSlopeByDrain.V005.Core.Models
{
    public class AutoSlopeDrainResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }

        public int VerticesModified { get; set; }
        public int VerticesProcessed { get; set; }
        public int VerticesSkipped { get; set; }
        public int DrainCount { get; set; }
        public double HighestElevation_mm { get; set; }
        public double LongestPath_m { get; set; }
        public int RunDuration_sec { get; set; }
        public string RunDate { get; set; }

        /// <summary>Parameter-write status code: 1=OK, 2=Partial, 3=Failed.</summary>
        public int Status { get; set; }

        /// <summary>Tool version string, e.g. "006".</summary>
        public string Version { get; set; }

        /// <summary>Total drains DrainDetectionService found, before user selection.</summary>
        public int TotalDetectedCount { get; set; }

        /// <summary>Drains the user had checked in the grid at Run time.</summary>
        public int SelectedCount { get; set; }

        /// <summary>Boundary/opening arcs processed when curve-intersection insertion is enabled. 0 if off.</summary>
        public int ArcsCalculated { get; set; }

        /// <summary>Vertices skipped because they were unreachable from any selected drain (subset of VerticesSkipped).</summary>
        public int UnreachableVertexCount { get; set; }

        /// <summary>Vertices skipped because a path existed but exceeded Max Path Distance (subset of VerticesSkipped, distinct from UnreachableVertexCount).</summary>
        public int OverThresholdVertexCount { get; set; }

        /// <summary>Path of the exported Excel workbook, or null if export was skipped/failed.</summary>
        public string ExportedFilePath { get; set; }
    }
}
