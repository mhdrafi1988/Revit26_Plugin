// =======================================================
// File: AutoSlopePayload.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.WithRidge
// Changes:
//   + RidgeDetectionEnabled  — master toggle for ridge logic.
//   + DrainGroupRadiusMm     — user-defined clustering radius.
//     Two drain points within this XY distance belong to the
//     same group. Default 500 mm. Exposed in UI.
//   + RidgeLineToleranceMm   — user-defined membership band.
//     A roof vertex within this XY perpendicular distance of
//     the ridge line is treated as a ridge point.
//     Default 500 mm. Exposed in UI.
//   RidgeRatioTolerance removed — replaced by geometry-driven
//   drain-group detection. No ratio parameter needed.
// =======================================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByPoint.WithRidge.Core.Models
{
    public class AutoSlopePayload
    {
        // ── Inputs ──────────────────────────────────────────
        public ElementId RoofId { get; set; }

        /// <summary>Raw points the user picked — before tolerance expansion.</summary>
        public List<XYZ> PickedDrainPoints { get; set; }

        /// <summary>Final drain points after tolerance radius is applied.</summary>
        public List<XYZ> DrainPoints { get; set; }

        public double SlopePercent    { get; set; }
        public double ThresholdMeters { get; set; }
        public bool   EnableDrainTolerance { get; set; }
        public int    DrainToleranceMm     { get; set; }
        public ExportConfig ExportConfig   { get; set; }
        public string ProjectTitle         { get; set; }

        // ── Ridge detection ──────────────────────────────────
        /// <summary>
        /// Master toggle. When false the engine skips all ridge
        /// detection and behaves exactly like the original version.
        /// </summary>
        public bool RidgeDetectionEnabled { get; set; }

        /// <summary>
        /// Drain grouping radius in millimetres.
        /// Two drain points whose XY distance is ≤ this value
        /// are placed in the same group.
        /// Default 500 mm. User-adjustable via the UI.
        /// </summary>
        public int DrainGroupRadiusMm { get; set; } = 500;

        /// <summary>
        /// Ridge line membership tolerance in millimetres.
        /// A roof vertex whose XY perpendicular distance to the
        /// ridge line is ≤ this value is treated as a ridge point.
        /// Default 500 mm. User-adjustable via the UI.
        /// </summary>
        public int RidgeLineToleranceMm { get; set; } = 500;

        // ── Callbacks ────────────────────────────────────────
        public Action<string>          Log         { get; set; }
        public Action<AutoSlopeResult> OnCompleted { get; set; }
    }
}
