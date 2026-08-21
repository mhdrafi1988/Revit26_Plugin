// =======================================================
// File: CircleMarkerService.cs
// Namespace: Revit26_Plugin.AutoSlopeByPointRPF.V003
// New in V026.
// Purpose: Places DetailCurve circles (2 half-arcs forming a closed
//          circle, planar to the active view's sketch plane) marking:
//            1. Drain group   — one circle per final drain point.
//            2. Highest group — one circle per vertex tied at max
//                                ElevationOffsetMm (calculated value,
//                                known DURING the transaction — see
//                                note below on why not
//                                ElevationFromModel_mm).
//            3. Offset group  — one circle per processed vertex with
//                                ElevationOffsetMm >= threshold,
//                                excluding vertices already circled
//                                as Highest Point (no double-circle).
//
// Contract:
//   - Must be called from inside an already-open Transaction
//     (AutoSlopeEngine's "Apply AutoSlope" tx) — this service does
//     NOT open/commit its own transaction, per confirmed spec
//     (single commit, single undo step for the whole Run).
//   - Because of that same-transaction requirement, the Highest Point
//     group compares ElevationOffsetMm (calculated: pathLength ×
//     slope) rather than ElevationFromModel_mm (which AutoSlopeEngine
//     only re-reads from Revit AFTER tx.Commit() — not yet available
//     while this service runs). Confirmed decision, not an oversight.
//   - Caller guarantees activeView is a ViewPlan; this service
//     re-checks defensively and no-ops (returns zero counts) if not,
//     so it can never partially place circles into an invalid view.
//   - Color is applied as a per-view DetailCurve graphic override
//     (OverrideGraphicSettings.SetProjectionLineColor), independent
//     of whatever color the chosen Line Style itself carries.
//   - Line Style must already exist in the project (OST_Lines
//     subcategory) — this service never creates new line styles.
// =======================================================

using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlopeByPointRPF.V003.Core.Models;
using Revit26_Plugin.AutoSlopeByPointRPF.V003.Infrastructure.Helpers;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByPointRPF.V003.Core.Engine
{
    public static class CircleMarkerService
    {
        public class PlacementCounts
        {
            public int DrainCirclesPlaced { get; set; }
            public int HighestCirclesPlaced { get; set; }
            public int OffsetCirclesPlaced { get; set; }
        }

        /// <summary>
        /// Places circles for all three marker groups. Must be called inside an
        /// already-open Transaction. Returns per-group placed counts (zero for any
        /// group that was disabled, had no qualifying points, or was skipped due
        /// to an invalid active view).
        /// </summary>
        public static PlacementCounts PlaceMarkers(
            Document doc,
            View activeView,
            List<XYZ> finalDrainPoints,
            List<VertexData> vertexDataList,
            CircleMarkerGroup drainGroup,
            CircleMarkerGroup highestGroup,
            CircleMarkerGroup offsetGroup,
            double allowedOffsetThresholdMm,
            Action<LogEntry> log)
        {
            var counts = new PlacementCounts();

            // ── Guard: active view must be a plan view ─────────────────────
            if (!(activeView is ViewPlan))
            {
                log?.Invoke(new LogEntry(LogLevel.Warning,
                    "Circle Markers: active view is not a plan view — skipping circle placement."));
                return counts;
            }

            // ── Guard: sketch plane needed for DetailCurve.Create ──────────
            if (activeView.SketchPlane == null && activeView.GenLevel == null)
            {
                log?.Invoke(new LogEntry(LogLevel.Warning,
                    "Circle Markers: active view has no usable level/sketch plane — skipping circle placement."));
                return counts;
            }

            double viewElevFt = GetViewElevationFt(activeView);

            // ── Group 1: Drains ─────────────────────────────────────────────
            if (drainGroup != null && drainGroup.IsEnabled)
            {
                if (finalDrainPoints == null || finalDrainPoints.Count == 0)
                {
                    log?.Invoke(new LogEntry(LogLevel.Info,
                        "Circle Markers: Drain group enabled but there are no final drain points to circle."));
                }
                else
                {
                    int placed = 0;
                    foreach (XYZ pt in finalDrainPoints)
                    {
                        if (pt == null) continue;
                        if (PlaceOneCircle(doc, activeView, pt, viewElevFt, drainGroup, log))
                            placed++;
                    }
                    counts.DrainCirclesPlaced = placed;
                    log?.Invoke(new LogEntry(LogLevel.Success,
                        $"Circle Markers: placed {placed} drain circle(s)."));
                }
            }

            // ── Group 2: Highest Point ───────────────────────────────────────
            // All vertices tied at max ElevationOffsetMm (not just the first).
            // Uses the calculated offset (pathLength × slope), not the post-commit
            // model re-read value — ElevationFromModel_mm is only known AFTER
            // tx.Commit(), but circle placement runs inside the same transaction
            // as the slope vertex writes (confirmed: one commit, one undo step).
            var highestVertices = new List<VertexData>();
            if (highestGroup != null && highestGroup.IsEnabled)
            {
                var processed = vertexDataList?.Where(v => v.WasProcessed).ToList() ?? new List<VertexData>();
                if (processed.Count == 0)
                {
                    log?.Invoke(new LogEntry(LogLevel.Info,
                        "Circle Markers: Highest Point group enabled but there are no processed vertices."));
                }
                else
                {
                    double maxElev = processed.Max(v => v.ElevationOffsetMm);
                    // Small epsilon guards against floating point rounding when
                    // comparing values that were rounded to whole mm in VertexData.
                    const double eps = 0.001;
                    highestVertices = processed
                        .Where(v => Math.Abs(v.ElevationOffsetMm - maxElev) <= eps)
                        .ToList();

                    int placed = 0;
                    foreach (var v in highestVertices)
                    {
                        if (PlaceOneCircle(doc, activeView, v.Position, viewElevFt, highestGroup, log))
                            placed++;
                    }
                    counts.HighestCirclesPlaced = placed;
                    log?.Invoke(new LogEntry(LogLevel.Success,
                        $"Circle Markers: placed {placed} highest-point circle(s) at {maxElev:0} mm."));
                }
            }

            // ── Group 3: Allowed Offset ──────────────────────────────────────
            // Excludes vertices already circled as Highest Point — compared by
            // VertexIndex (stable identity from the engine's vertex collection).
            if (offsetGroup != null && offsetGroup.IsEnabled)
            {
                var highestIndices = new HashSet<int>(highestVertices.Select(v => v.VertexIndex));

                var qualifying = (vertexDataList ?? new List<VertexData>())
                    .Where(v => v.WasProcessed
                             && v.ElevationOffsetMm >= allowedOffsetThresholdMm
                             && !highestIndices.Contains(v.VertexIndex))
                    .ToList();

                if (qualifying.Count == 0)
                {
                    log?.Invoke(new LogEntry(LogLevel.Info,
                        $"Circle Markers: Allowed Offset group enabled but no vertices met the {allowedOffsetThresholdMm:0} mm threshold."));
                }
                else
                {
                    int placed = 0;
                    foreach (var v in qualifying)
                    {
                        if (PlaceOneCircle(doc, activeView, v.Position, viewElevFt, offsetGroup, log))
                            placed++;
                    }
                    counts.OffsetCirclesPlaced = placed;
                    log?.Invoke(new LogEntry(LogLevel.Success,
                        $"Circle Markers: placed {placed} allowed-offset circle(s) (>= {allowedOffsetThresholdMm:0} mm)."));
                }
            }

            return counts;
        }

        // ── Internal helpers ─────────────────────────────────────────────────

        /// <summary>
        /// Places a single circular DetailCurve centered at the XY of centerPt,
        /// flattened onto the view's working elevation. Applies the group's
        /// Line Style and per-view color override. Returns false (and logs a
        /// warning) if creation failed for this point, without throwing —
        /// one bad point should not abort the whole group.
        /// </summary>
        private static bool PlaceOneCircle(
            Document doc,
            View activeView,
            XYZ centerPt,
            double viewElevFt,
            CircleMarkerGroup style,
            Action<LogEntry> log)
        {
            try
            {
                double radiusFt = UnitUtils.ConvertToInternalUnits(style.RadiusMm, UnitTypeId.Millimeters);
                if (radiusFt <= 0)
                {
                    log?.Invoke(new LogEntry(LogLevel.Warning,
                        $"Circle Markers: skipped a {style.GroupLabel} circle — radius must be greater than 0."));
                    return false;
                }

                // Flatten the circle onto the plan view's working elevation so it
                // reads correctly regardless of the source point's true Z (roof
                // vertices carry slope elevation; the marker should sit on the
                // view's reference plane, not float at roof height).
                XYZ center = new XYZ(centerPt.X, centerPt.Y, viewElevFt);

                // Revit rejects a single Arc.Create with a full 2π sweep (start/end
                // coincide) — build the closed circle from two half-arcs instead,
                // which is the standard workaround for a closed circular DetailCurve.
                Arc halfA = Arc.Create(center, radiusFt, 0, Math.PI, XYZ.BasisX, XYZ.BasisY);
                Arc halfB = Arc.Create(center, radiusFt, Math.PI, 2 * Math.PI, XYZ.BasisX, XYZ.BasisY);

                DetailCurve dcA = doc.Create.NewDetailCurve(activeView, halfA);
                DetailCurve dcB = doc.Create.NewDetailCurve(activeView, halfB);

                ApplyStyle(doc, activeView, dcA, style, log);
                ApplyStyle(doc, activeView, dcB, style, log);

                return true;
            }
            catch (Exception ex)
            {
                log?.Invoke(new LogEntry(LogLevel.Warning,
                    $"Circle Markers: failed to place a {style.GroupLabel} circle — {ex.Message}"));
                return false;
            }
        }

        /// <summary>
        /// Assigns the chosen Line Style (existing OST_Lines subcategory) as the
        /// element's line-style, and applies the chosen named color as a per-view
        /// graphic override. Both are best-effort — a failure here is logged as a
        /// warning but does not remove the already-placed geometry.
        /// </summary>
        private static void ApplyStyle(
            Document doc,
            View activeView,
            DetailCurve dc,
            CircleMarkerGroup style,
            Action<LogEntry> log)
        {
            if (dc == null) return;

            // Line Style — set via the element's LineStyle property using the
            // GraphicsStyle looked up from style.LineStyleId.
            if (style.LineStyleId != null && style.LineStyleId != ElementId.InvalidElementId)
            {
                if (doc.GetElement(style.LineStyleId) is GraphicsStyle gs)
                {
                    try { dc.LineStyle = gs; }
                    catch (Exception ex)
                    {
                        log?.Invoke(new LogEntry(LogLevel.Warning,
                            $"Circle Markers: could not apply line style '{style.LineStyleName}' — {ex.Message}"));
                    }
                }
            }

            // Color — per-view projection line color override, independent of
            // the line style's own defined color.
            Color revitColor = NamedColorHelper.ToRevitColor(style.ColorName);
            if (revitColor != null)
            {
                try
                {
                    OverrideGraphicSettings ogs = activeView.GetElementOverrides(dc.Id) ?? new OverrideGraphicSettings();
                    ogs.SetProjectionLineColor(revitColor);
                    activeView.SetElementOverrides(dc.Id, ogs);
                }
                catch (Exception ex)
                {
                    log?.Invoke(new LogEntry(LogLevel.Warning,
                        $"Circle Markers: could not apply color '{style.ColorName}' — {ex.Message}"));
                }
            }
        }

        /// <summary>
        /// Working elevation (internal feet) to flatten markers onto: the active
        /// plan view's associated level elevation, or the view's GenLevel if the
        /// simpler property isn't available.
        /// </summary>
        private static double GetViewElevationFt(View activeView)
        {
            if (activeView is ViewPlan vp && vp.GenLevel != null)
                return vp.GenLevel.Elevation;
            return 0;
        }
    }
}
