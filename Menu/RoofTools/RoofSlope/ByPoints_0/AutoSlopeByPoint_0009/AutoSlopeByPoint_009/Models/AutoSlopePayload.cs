// =======================================================
// File: AutoSlopePayload.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.V009
// Changes vs V06:
//   - Log callback changed from Action<string> to Action<LogEntry>
//     so the engine emits structured LogEntry (LogLevel + message)
//     instead of raw colour-tagged strings.
//   - LogColorHelper removed — level is expressed via LogLevel enum.
// =======================================================

using Autodesk.Revit.DB;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByPoint.V009.Core.Models
{
    public class AutoSlopePayload
    {
        // ── Inputs ──────────────────────────────────────────
        public ElementId RoofId { get; set; }

        /// <summary>Raw points the user picked — before tolerance expansion.</summary>
        public List<XYZ> PickedDrainPoints { get; set; }

        /// <summary>Final drain points after tolerance radius is applied (used for calculation).</summary>
        public List<XYZ> DrainPoints { get; set; }

        public double SlopePercent { get; set; }
        public double ThresholdMeters { get; set; }
        public bool EnableDrainTolerance { get; set; }
        public int DrainToleranceMm { get; set; }
        public ExportConfig ExportConfig { get; set; }

        /// <summary>
        /// Revit document title — passed in from the UI layer so that
        /// Core/Infrastructure never need to touch UIDocument directly.
        /// </summary>
        public string ProjectTitle { get; set; }

        // ── Callbacks ────────────────────────────────────────
        /// <summary>
        /// Called by the engine to emit a structured log entry.
        /// Subscriber (ViewModel) wires this to AddLog(LogEntry).
        /// Uses Shared.Models.LogEntry so colour is driven by
        /// LogLevelToColorConverter in the UI — no HTML tags.
        /// </summary>
        public Action<LogEntry> Log { get; set; }

        /// <summary>
        /// Called exactly once when the engine finishes (success or failure).
        /// Core never imports the subscriber type — the ViewModel wires itself
        /// up as a lambda from the UI layer.
        /// </summary>
        public Action<AutoSlopeResult> OnCompleted { get; set; }
    }
}
