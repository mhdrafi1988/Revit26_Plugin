// =======================================================
// File: AutoSlopePayload.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.V021
// Changes vs V06:
//   - Log callback changed from Action<string> to Action<LogEntry>
//     so the engine emits structured LogEntry (LogLevel + message)
//     instead of raw colour-tagged strings.
//   - LogColorHelper removed — level is expressed via LogLevel enum.
// =======================================================

using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlopeByPoint.V021.Core.Engine;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByPoint.V021.Core.Models
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

        /// <summary>
        /// V021, opt-in, off by default. When true: auto-clusters DrainPoints into
        /// drain groups (using the existing drain-tolerance radius as the clustering
        /// distance), builds a Delaunay adjacency graph over group centers, and for
        /// each adjacent pair raises up to 2 "ridge" vertices (nearest existing
        /// SlabShapeVertex in each perpendicular direction from the pair's midpoint).
        /// Each ridge vertex's elevation is driven by the Dijkstra distance to the
        /// FARTHER of the two groups (nearest drain within that group), instead of
        /// the standard nearest-drain-overall rule used for all other vertices.
        /// </summary>
        public bool EnableRidgePointDetection { get; set; }

        /// <summary>
        /// V021: proximity tolerance (mm) used when matching roof vertices to the
        /// actual ridge line between two adjacent drain groups — the real Voronoi
        /// edge (boundary between the two groups' territories) when one is
        /// available, or a midpoint-perpendicular fallback line for degenerate
        /// cases (fewer than 3 groups, or collinear group centers). Any roof
        /// vertex within this distance of that line becomes a ridge point — no
        /// cap on count, and a vertex may be matched by more than one pair
        /// (last-processed pair wins if so). REPURPOSED from the original
        /// "Corridor Width" (500mm, wide-band search around a straight line)
        /// to this tighter edge-hugging
        /// tolerance (confirmed default 100mm) once the search moved to the real
        /// Voronoi edge. Default 100mm if left at 0 or negative.
        /// </summary>
        public double RidgeCorridorWidthMm { get; set; } = 100.0;

        /// <summary>
        /// V021, sub-toggle of EnableRidgePointDetection (independent — ridge
        /// detection can run without drawing markers). When true, draws a
        /// circle (DetailCurve) at each resolved ridge point in the ACTIVE
        /// VIEW only, using MarkerLineStyleName / MarkerColorName / RidgePointCircleRadiusMm
        /// below. Drawn in a SubTransaction, non-fatal on failure. Has no
        /// effect if EnableRidgePointDetection is false or no ridge points
        /// were resolved.
        /// </summary>
        public bool MarkRidgePointsInView { get; set; }

        /// <summary>
        /// Name of an EXISTING line-style subcategory (under Lines), chosen by
        /// the user from styles currently in use somewhere in the project —
        /// see RidgePointMarker.GetUsedLineStyleNames. Reused and recolored
        /// (globally) to MarkerColorName. Null/empty means no used styles
        /// were found — marking is skipped with a warning, not a popup.
        /// </summary>
        public string MarkerLineStyleName { get; set; }

        /// <summary>Name from RidgeMarkerColorPalette (e.g. "Red", "Blue"). Defaults to "Red".</summary>
        public string MarkerColorName { get; set; } = RidgeMarkerColorPalette.DefaultColorName;

        /// <summary>Ridge-point marker circle RADIUS in mm. Defaults to 250mm.</summary>
        public double RidgePointCircleRadiusMm { get; set; } = RidgePointMarker.DefaultRadiusMm;

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
