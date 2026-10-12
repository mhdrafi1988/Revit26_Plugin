// File: CircleMarkerService.cs
// Location: Core/Engine/
// Ported from AutoSlopeByPoint V028's CircleMarkerService.cs, adapted for
// ByDrain's DrainVertexData / selected-DrainItem-center-point shape.
//
// Purpose: Places DetailCurve circles (2 half-arcs forming a closed circle,
//          planar to the active view's sketch plane) marking:
//            1. Drain group   — one circle per selected drain opening
//                                (centered on the DrainItem's CenterPoint).
//            2. Highest group — one circle per vertex tied at max
//                                ElevationOffsetMm among processed vertices
//                                (calculated value, known DURING the
//                                transaction — same reasoning as ByPoint:
//                                the post-commit re-read value isn't
//                                available yet while this service runs).
//
// Contract:
//   - Must be called from inside an already-open Transaction (the same
//     "Auto Roof Sloper - Apply Slopes" tx RoofSlopeProcessorService uses) —
//     this service does NOT open/commit its own transaction (one commit,
//     one undo step for the whole Run, same as ByPoint).
//   - Caller guarantees activeView is a ViewPlan; this service re-checks
//     defensively and no-ops (returns zero counts) if not.
//   - Color is applied as a per-view DetailCurve graphic override
//     (OverrideGraphicSettings.SetProjectionLineColor), independent of
//     whatever color the chosen Line Style itself carries.
//   - Line Style must already exist in the project (OST_Lines subcategory)
//     — this service never creates new line styles.

using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlopeByDrain.V007.Core.Models;
using Revit26_Plugin.AutoSlopeByDrain.V007.Infrastructure.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByDrain.V007.Core.Engine
{
    public static class CircleMarkerService
    {
        public class PlacementCounts
        {
            public int DrainCirclesPlaced { get; set; }
            public int HighestCirclesPlaced { get; set; }
        }

        /// <summary>
        /// Places circles for the Drain and Highest Point marker groups. Must be
        /// called inside an already-open Transaction. Returns per-group placed
        /// counts (zero for any group that was disabled, had no qualifying
        /// points, or was skipped due to an invalid active view).
        /// </summary>
        public static PlacementCounts PlaceMarkers(
            Document doc,
            View activeView,
            List<XYZ> drainCenterPoints,
            List<DrainVertexData> vertexDataList,
            CircleMarkerGroup drainGroup,
            CircleMarkerGroup highestGroup,
            double clusterToleranceMm,
            Action<string> log)
        {
            var counts = new PlacementCounts();

            double clusterToleranceFt = clusterToleranceMm > 0
                ? UnitUtils.ConvertToInternalUnits(clusterToleranceMm, UnitTypeId.Millimeters)
                : 0;

            // ── Guard: active view must be a plan view ─────────────────────
            if (!(activeView is ViewPlan))
            {
                log?.Invoke("Circle Markers: active view is not a plan view — skipping circle placement.");
                return counts;
            }

            // ── Guard: sketch plane needed for DetailCurve.Create ──────────
            if (activeView.SketchPlane == null && activeView.GenLevel == null)
            {
                log?.Invoke("Circle Markers: active view has no usable level/sketch plane — skipping circle placement.");
                return counts;
            }

            double viewElevFt = GetViewElevationFt(activeView);

            // ── Group 1: Drains ─────────────────────────────────────────────
            if (drainGroup != null && drainGroup.IsEnabled)
            {
                if (drainCenterPoints == null || drainCenterPoints.Count == 0)
                {
                    log?.Invoke("Circle Markers: Drain group enabled but there are no selected drain points to circle.");
                }
                else
                {
                    var clusteredDrainPoints = ClusterPoints(drainCenterPoints, clusterToleranceFt);
                    int placed = 0;
                    foreach (XYZ pt in clusteredDrainPoints)
                    {
                        if (pt == null) continue;
                        if (PlaceOneCircle(doc, activeView, pt, viewElevFt, drainGroup, log))
                            placed++;
                    }
                    counts.DrainCirclesPlaced = placed;
                    log?.Invoke($"Circle Markers: placed {placed} drain circle(s) for {drainCenterPoints.Count} opening(s).");
                }
            }

            // ── Group 2: Highest Point ───────────────────────────────────────
            // All vertices tied at max ElevationOffsetMm (not just the first).
            // Uses the calculated offset (pathLength × slope), not a post-commit
            // model re-read value, since circle placement runs inside the same
            // transaction as the slope vertex writes.
            if (highestGroup != null && highestGroup.IsEnabled)
            {
                var processed = vertexDataList?.Where(v => v.WasProcessed).ToList() ?? new List<DrainVertexData>();
                if (processed.Count == 0)
                {
                    log?.Invoke("Circle Markers: Highest Point group enabled but there are no processed vertices.");
                }
                else
                {
                    double maxElev = processed.Max(v => v.ElevationOffsetMm);
                    // Small epsilon guards against floating point rounding when
                    // comparing values that were rounded to whole mm in DrainVertexData.
                    const double eps = 0.001;
                    var highestVertices = processed
                        .Where(v => Math.Abs(v.ElevationOffsetMm - maxElev) <= eps)
                        .ToList();

                    var clusteredHighestPoints = ClusterPoints(
                        highestVertices.Select(v => v.Position).ToList(), clusterToleranceFt);
                    int placed = 0;
                    foreach (var pt in clusteredHighestPoints)
                    {
                        if (PlaceOneCircle(doc, activeView, pt, viewElevFt, highestGroup, log))
                            placed++;
                    }
                    counts.HighestCirclesPlaced = placed;
                    log?.Invoke($"Circle Markers: placed {placed} highest-point circle(s) for {highestVertices.Count} vertex/vertices at {maxElev:0} mm.");
                }
            }

            return counts;
        }

        // ── Internal helpers ─────────────────────────────────────────────────

        /// <summary>
        /// Merges points that lie within toleranceFt of another point in the
        /// same group (transitively — chained via union-find) into a single
        /// point at the center of that group's bounding box. A point with
        /// nothing else nearby is returned unchanged (standalone).
        /// </summary>
        private static List<XYZ> ClusterPoints(List<XYZ> points, double toleranceFt)
        {
            var valid = (points ?? new List<XYZ>()).Where(p => p != null).ToList();
            if (valid.Count <= 1 || toleranceFt <= 0)
                return valid;

            int n = valid.Count;
            int[] parent = Enumerable.Range(0, n).ToArray();
            int Find(int i)
            {
                while (parent[i] != i)
                {
                    parent[i] = parent[parent[i]];
                    i = parent[i];
                }
                return i;
            }
            void Union(int a, int b)
            {
                int ra = Find(a), rb = Find(b);
                if (ra != rb) parent[ra] = rb;
            }

            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    if (valid[i].DistanceTo(valid[j]) <= toleranceFt)
                        Union(i, j);

            var groups = new Dictionary<int, List<XYZ>>();
            for (int i = 0; i < n; i++)
            {
                int root = Find(i);
                if (!groups.TryGetValue(root, out var list))
                    groups[root] = list = new List<XYZ>();
                list.Add(valid[i]);
            }

            var centers = new List<XYZ>();
            foreach (var group in groups.Values)
            {
                double minX = group.Min(p => p.X), maxX = group.Max(p => p.X);
                double minY = group.Min(p => p.Y), maxY = group.Max(p => p.Y);
                double avgZ = group.Average(p => p.Z);
                centers.Add(new XYZ((minX + maxX) / 2.0, (minY + maxY) / 2.0, avgZ));
            }
            return centers;
        }

        /// <summary>
        /// Places a single circular DetailCurve centered at the XY of centerPt,
        /// flattened onto the view's working elevation. Returns false (and logs
        /// a warning) if creation failed for this point, without throwing.
        /// </summary>
        private static bool PlaceOneCircle(
            Document doc,
            View activeView,
            XYZ centerPt,
            double viewElevFt,
            CircleMarkerGroup style,
            Action<string> log)
        {
            try
            {
                double radiusFt = UnitUtils.ConvertToInternalUnits(style.RadiusMm, UnitTypeId.Millimeters);
                if (radiusFt <= 0)
                {
                    log?.Invoke($"Circle Markers: skipped a {style.GroupLabel} circle — radius must be greater than 0.");
                    return false;
                }

                // Flatten the circle onto the plan view's working elevation so it
                // reads correctly regardless of the source point's true Z.
                XYZ center = new XYZ(centerPt.X, centerPt.Y, viewElevFt);

                // Revit rejects a single Arc.Create with a full 2π sweep — build the
                // closed circle from two half-arcs instead.
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
                log?.Invoke($"Circle Markers: failed to place a {style.GroupLabel} circle — {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Assigns the chosen Line Style and per-view color override. Both are
        /// best-effort — a failure here is logged but does not remove the
        /// already-placed geometry.
        /// </summary>
        private static void ApplyStyle(
            Document doc,
            View activeView,
            DetailCurve dc,
            CircleMarkerGroup style,
            Action<string> log)
        {
            if (dc == null) return;

            if (style.LineStyleId != null && style.LineStyleId != ElementId.InvalidElementId)
            {
                if (doc.GetElement(style.LineStyleId) is GraphicsStyle gs)
                {
                    try { dc.LineStyle = gs; }
                    catch (Exception ex)
                    {
                        log?.Invoke($"Circle Markers: could not apply line style '{style.LineStyleName}' — {ex.Message}");
                    }
                }
            }

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
                    log?.Invoke($"Circle Markers: could not apply color '{style.ColorName}' — {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Working elevation (internal feet) to flatten markers onto: the active
        /// plan view's associated level elevation, or 0 if unavailable.
        /// </summary>
        private static double GetViewElevationFt(View activeView)
        {
            if (activeView is ViewPlan vp && vp.GenLevel != null)
                return vp.GenLevel.Elevation;
            return 0;
        }
    }
}
