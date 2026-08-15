// =======================================================
// File: AutoSlopePayload.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.RPF_001
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

namespace Revit26_Plugin.AutoSlopeByPoint.RPF_001.Core.Models
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

        /// <summary>
        /// Opt-in: when true, the engine will insert real SlabShapeVertex points
        /// at line/arc intersection locations (where a straight roof-shape edge
        /// partially overlaps a boundary or opening arc) so distances and elevations
        /// account for curved edges instead of using a straight chord.
        /// </summary>
        public bool InsertCurveIntersectionPoints { get; set; }

        public ExportConfig ExportConfig { get; set; }

        /// <summary>
        /// Revit document title — passed in from the UI layer so that
        /// Core/Infrastructure never need to touch UIDocument directly.
        /// </summary>
        public string ProjectTitle { get; set; }

        // ── Circle Markers (V026) ─────────────────────────────
        /// <summary>Style/config for circles placed on final drain points. Null or IsEnabled=false skips this group.</summary>
        public CircleMarkerGroup DrainMarkerGroup { get; set; }

        /// <summary>Style/config for circles placed on vertices tied at max elevation. Null or IsEnabled=false skips this group.</summary>
        public CircleMarkerGroup HighestPointMarkerGroup { get; set; }

        /// <summary>Style/config for circles placed on processed vertices whose offset meets/exceeds AllowedOffsetThresholdMm. Null or IsEnabled=false skips this group.</summary>
        public CircleMarkerGroup AllowedOffsetMarkerGroup { get; set; }

        /// <summary>
        /// Threshold in millimeters for the Allowed Offset marker group.
        /// A processed vertex qualifies when its ElevationOffsetMm >= this value.
        /// Only relevant when AllowedOffsetMarkerGroup.IsEnabled is true.
        /// </summary>
        public double AllowedOffsetThresholdMm { get; set; }

        // ── Ridge Points (new in RPF_001) ─────────────────────
        /// <summary>Opt-in: when true, the engine clusters drain points into groups and detects ridge/hip points between them via Voronoi geometry.</summary>
        public bool EnableRidgePoints { get; set; }

        /// <summary>Max distance (mm) from a roof vertex to a computed ridge line for that vertex to be treated as a ridge point.</summary>
        public double RidgeToleranceMm { get; set; }

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
