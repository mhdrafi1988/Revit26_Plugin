// File: AutoSlopeDrainPayload.cs
// Location: Core/Models/
// Mirrors AutoSlopeByPoint's AutoSlopePayload pattern.
//
// NOTE ON STALE HANDLES: the grid the user interacts with is populated from
// a one-time DrainDetectionService pass run in the Command (before the
// window is shown). Because the window is now MODELESS, the roof could in
// theory be edited before Run is clicked, so we do NOT carry SlabShapeVertex
// references across the ExternalEvent boundary, and we do NOT match by index
// either (index order could silently point at the wrong drain if the count
// or ordering changed). Instead we carry SelectedDrainSignatures (center
// point + size) and ExpectedDrainCount; the Engine re-detects drains fresh
// inside Execute() and position-matches each signature — same "always
// re-fetch fresh handles after regenerate" principle used elsewhere in the
// suite, made robust to concurrent model edits.

using Autodesk.Revit.DB;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByDrain.V003.Core.Models
{
    public class AutoSlopeDrainPayload
    {
        // ── Inputs ──────────────────────────────────────────
        public ElementId RoofId { get; set; }

        /// <summary>
        /// Signatures (center point + size) of the drains the user selected, captured from the
        /// DrainItem list shown in the grid. Matched against a fresh re-detection by position —
        /// NOT by index — so an edit to the roof while the modeless window is open can't
        /// silently apply slope calculations to the wrong drain.
        /// </summary>
        public List<DrainSelectionSignature> SelectedDrainSignatures { get; set; }

        /// <summary>Drain count shown in the grid when the user clicked Run — used to detect
        /// and warn if the roof geometry changed since then.</summary>
        public int ExpectedDrainCount { get; set; }

        public double SlopePercent { get; set; }
        public double ConnectionThresholdMeters { get; set; }
        public int PathSampleCount { get; set; }

        public ExportConfig ExportConfig { get; set; }

        /// <summary>Revit document title — passed in so Core/Infrastructure never touch UIDocument directly.</summary>
        public string ProjectTitle { get; set; }

        // ── Callbacks ────────────────────────────────────────
        /// <summary>Called by the engine to emit a structured log entry.</summary>
        public Action<LogEntry> Log { get; set; }

        /// <summary>Called exactly once when the engine finishes (success or failure).</summary>
        public Action<AutoSlopeDrainResult> OnCompleted { get; set; }
    }
}
