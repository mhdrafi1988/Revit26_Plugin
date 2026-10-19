// =======================================================
// File: InnerLoopDividerPayload.cs
// Location: Core/Models/
// New in V009. Carries a request across the ExternalEvent boundary.
// One Handler serves two distinct operations (Analyze / Apply) — see
// Operation. SelectedLoops carries the real CurveLoop geometry captured
// at Analyze time, the same way AutoSlopeByDrain's AutoSlopeDrainPayload
// carries DrainItem.LoopCurves across the same boundary.
// =======================================================

using Autodesk.Revit.DB;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.InnerLoopDivider.V009.Core.Models
{
    public enum InnerLoopDividerOperation { Analyze, Apply }

    public class InnerLoopDividerPayload
    {
        public ElementId RoofId { get; set; }

        public InnerLoopDividerOperation Operation { get; set; }

        /// <summary>Only used for Operation.Apply — the loops the user checked, including RecommendedPoints and Geometry.</summary>
        public List<RoofLoopModel> SelectedLoops { get; set; }

        /// <summary>Called by the engine to emit a structured log entry.</summary>
        public Action<LogEntry> Log { get; set; }

        /// <summary>Called exactly once when the engine finishes (success or failure).</summary>
        public Action<InnerLoopDividerResult> OnCompleted { get; set; }
    }
}
