// File: AutoSlopeDrainPayload.cs
// Location: Core/Models/
//
// UPDATED (V005, second round), per Rafi's confirmed decisions on 2026-07-22:
// SelectedDrainSignatures REPLACED with SelectedDrains : List<DrainItem>.
//
// Rationale: previously the payload carried lightweight position/size
// signatures, and the Engine re-detected all openings fresh + position-
// matched each signature back to a DrainItem — this let the tool detect and
// warn if the roof geometry changed while the modeless window was open.
// Rafi confirmed trading that safety net away: the loop geometry captured
// during the ORIGINAL detection pass (in the Command) is now reused directly
// at Run time rather than re-extracted, so the actual selected DrainItem
// objects (including their LoopCurves) are carried across the ExternalEvent
// boundary. There is no re-detection and no position-matching anymore — see
// AutoSlopeDrainEngine for where DrainVertices gets populated instead
// (deferred vertex-matching, only for these selected items).
//
// DrainSelectionSignature.cs is no longer used anywhere and has been deleted.
//
// NEW (V005, first round) fields, ported from AutoSlopeByPoint V018:
//   ThresholdMeters               — max TOTAL path length before a vertex is
//                                   skipped (distinct from
//                                   ConnectionThresholdMeters, which gates
//                                   graph edge creation, not path length).
//   InsertCurveIntersectionPoints — opt-in toggle, off by default.
//   VerifyElevationsAfterCommit  — opt-in toggle, off by default.

using Autodesk.Revit.DB;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByDrain.V006.Core.Models
{
    public class AutoSlopeDrainPayload
    {
        // ── Inputs ──────────────────────────────────────────────────────────
        public ElementId RoofId { get; set; }

        /// <summary>
        /// The DrainItems the user checked in the grid, carried directly across the
        /// ExternalEvent boundary (including their LoopCurves, captured during the
        /// original detection pass). The Engine does NOT re-detect or position-match
        /// these — it reuses this geometry as-is and only performs shape-edit vertex
        /// matching (X,Y -> Z) against a freshly re-read SlabShapeVertex list, for
        /// these items only. See AutoSlopeDrainEngine.
        /// </summary>
        public List<DrainItem> SelectedDrains { get; set; }

        /// <summary>Total drains DrainDetectionService found in the grid pass, regardless of selection.</summary>
        public int TotalDetectedCount { get; set; }

        public double SlopePercent { get; set; }

        /// <summary>
        /// Max distance between two vertices for them to be linked as graph neighbors at all.
        /// Gates GRAPH CONSTRUCTION, not final path length. Labeled "Max Edge Distance" in the UI.
        /// </summary>
        public double ConnectionThresholdMeters { get; set; }

        /// <summary>
        /// NEW (V005), ported from ByPoint. Max TOTAL (multi-hop) path length from a vertex to
        /// its nearest selected drain. A vertex whose shortest path exceeds this is skipped and
        /// logged as a Warning, even if every individual edge on that path passed
        /// ConnectionThresholdMeters. Labeled "Max Path Distance" in the UI.
        /// </summary>
        public double ThresholdMeters { get; set; }

        public int PathSampleCount { get; set; }

        /// <summary>
        /// NEW (V005), ported from ByPoint. When true, the engine inserts real SlabShapeVertex
        /// points at boundary/opening arc crossings before pathfinding, for accurate distance on
        /// curved edges. Off by default.
        /// </summary>
        public bool InsertCurveIntersectionPoints { get; set; }

        /// <summary>
        /// NEW (V005). When true, the engine re-reads each processed vertex's elevation from the
        /// model after Commit() and compares it to the calculated value, populating
        /// DrainVertexData.ElevationFromModel_mm / ElevationDiff_mm. Off by default — adds a
        /// re-read pass per vertex.
        /// </summary>
        public bool VerifyElevationsAfterCommit { get; set; }

        public ExportConfig ExportConfig { get; set; }

        /// <summary>Revit document title — passed in so Core/Infrastructure never touch UIDocument directly.</summary>
        public string ProjectTitle { get; set; }

        // ── Callbacks ────────────────────────────────────────────────────────
        /// <summary>Called by the engine to emit a structured log entry.</summary>
        public Action<LogEntry> Log { get; set; }

        /// <summary>Called exactly once when the engine finishes (success or failure).</summary>
        public Action<AutoSlopeDrainResult> OnCompleted { get; set; }
    }
}
