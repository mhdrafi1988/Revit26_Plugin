// =======================================================
// File: AutoSlopePayload.cs
// Fixes:
//   #9  DrainToleranceMm changed from double to int.
//       Millimetre tolerance values carry no sub-millimetre
//       precision anywhere in the codebase; int is correct
//       and matches the ViewModel property and AppConstants.
// =======================================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByPoint_05.Core.Models
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
        public int DrainToleranceMm { get; set; }   // Fix #9: was double
        public ExportConfig ExportConfig { get; set; }

        /// <summary>
        /// Revit document title — passed in from the UI layer so that
        /// Core/Infrastructure never need to touch UIDocument directly.
        /// </summary>
        public string ProjectTitle { get; set; }

        // ── Callbacks ────────────────────────────────────────
        /// <summary>
        /// Called by the engine to emit a log line.
        /// Subscriber (ViewModel) wires this to AddLog.
        /// </summary>
        public Action<string> Log { get; set; }

        /// <summary>
        /// Called exactly once when the engine finishes (success or failure).
        /// Core never imports the subscriber type — the ViewModel wires itself
        /// up as a lambda from the UI layer.
        /// </summary>
        public Action<AutoSlopeResult> OnCompleted { get; set; }
    }
}