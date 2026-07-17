// =======================================================
// File: AutoSlopeResult.cs
// Location: Core/Models/
// Purpose: Plain result object returned by the engine.
//          No UI, no WPF, no ViewModel references.
//          The ViewModel subscribes via OnCompleted callback
//          in AutoSlopePayload and reads from this object.
// =======================================================

namespace Revit26_Plugin.AutoSlopeByPoint.V021.Core.Models
{
    public class AutoSlopeResult
    {
        /// <summary>True if the engine completed without fatal errors.</summary>
        public bool Success { get; set; }

        /// <summary>Human-readable error message when Success is false.</summary>
        public string ErrorMessage { get; set; }

        public int VerticesProcessed { get; set; }
        public int VerticesSkipped { get; set; }

        /// <summary>Raw drain count from user selection � before tolerance expansion.</summary>
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

        /// <summary>V021, Ridge Point Detection: count of ridge points resolved (0 if feature disabled).</summary>
        public int RidgePointsResolved { get; set; }

        /// <summary>V021, Ridge Point Detection: count of adjacent group pairs skipped (no vertex found on either side).</summary>
        public int RidgePairsSkipped { get; set; }

        /// <summary>V021 (3rd pass): count of multi-group (3+) Voronoi junctions that resolved at least one ridge point.</summary>
        public int RidgeJunctionsResolved { get; set; }
    }
}