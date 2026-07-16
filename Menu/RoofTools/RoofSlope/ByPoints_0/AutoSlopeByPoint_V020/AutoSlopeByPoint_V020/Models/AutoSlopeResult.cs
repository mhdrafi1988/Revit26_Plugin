// =======================================================
// File: AutoSlopeResult.cs
// Location: Core/Models/
// Purpose: Plain result object returned by the engine.
//          No UI, no WPF, no ViewModel references.
//          The ViewModel subscribes via OnCompleted callback
//          in AutoSlopePayload and reads from this object.
// =======================================================

namespace Revit26_Plugin.AutoSlopeByPoint.V020.Core.Models
{
    public class AutoSlopeResult
    {
        /// <summary>True if the engine completed without fatal errors.</summary>
        public bool Success { get; set; }

        /// <summary>Human-readable error message when Success is false.</summary>
        public string ErrorMessage { get; set; }

        public int VerticesProcessed { get; set; }
        public int VerticesSkipped { get; set; }

        /// <summary>Raw drain count from user selection — before tolerance expansion.</summary>
        public int PickedDrainCount { get; set; }

        /// <summary>Final drain count after tolerance radius is applied.</summary>
        public int FinalDrainCount { get; set; }
        public double HighestElevation_mm { get; set; }
        public double LongestPath_m { get; set; }
        public int RunDuration_sec { get; set; }
        public string RunDate { get; set; }

        /// <summary>Plugin/tool version string, e.g. "P.10.00".</summary>
        public string Version { get; set; }

        /// <summary>Parameter-write status code: 1=OK, 2=Partial, 3=Failed.</summary>
        public int Status { get; set; }

        /// <summary>
        /// Path of the Excel file auto-exported after Run.
        /// Null if export was skipped (ExportToExcel = false or EPPlus missing).
        /// </summary>
        public string ExportedFilePath { get; set; }

        // ── Curve / tangent summary (only populated when
        //    InsertCurveIntersectionPoints is enabled; 0 otherwise) ──────────

        /// <summary>Count of boundary/opening arcs found on the roof (GetBoundaryArcs().Count).</summary>
        public int CurvesFound { get; set; }

        /// <summary>
        /// Total count of TangentRoute hops used across ALL processed vertices'
        /// final paths (sum, not distinct vertices — a single path can use more
        /// than one tangent hop).
        /// </summary>
        public int TangentAdded { get; set; }

        /// <summary>
        /// Count of DISTINCT vertices whose final path used at least one
        /// TangentRoute hop. Distinct from TangentAdded, which counts total hops.
        /// </summary>
        public int TangentPathUsed { get; set; }

        /// <summary>
        /// Count of DISTINCT tangent points (the ptA/ptB coordinates used to
        /// route around an arc) across all final paths, deduped by curveTolFt
        /// proximity — a single physical tangent point reused by several
        /// vertices' paths counts once, not once per hop.
        /// </summary>
        public int TangentCurveCount { get; set; }
    }
}