using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V51.Models;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V51.Services
{
    /// <summary>
    /// Clips raw Voronoi edges against the roof boundary polygon and collects
    /// all shape point locations according to the rules:
    ///   1. Voronoi vertices inside boundary
    ///   2. Edge-to-boundary intersections (clip points)
    ///   3. Edge-to-edge intersections inside boundary
    ///   4. Arc/curve tessellation points on boundary (already flattened by RoofBoundaryService)
    ///
    /// Fix C (Case 4): SegmentPolygonIntersections results are now de-duplicated within
    /// SnapTolerance before the ≥2 count check, preventing false discards caused by
    /// a segment hitting a polygon corner vertex shared by two adjacent edges.
    ///
    /// Uses a Sutherland–Hodgman style segment-vs-polygon clip for edge clipping.
    /// No Revit transaction required.
    /// </summary>
    public class VoronoiClippingService
    {
        /// <summary>Points closer than this are considered the same (Revit internal units = feet).</summary>
        public double SnapTolerance { get; set; } = 1.0 / 304.8; // 1 mm in feet

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Clips all raw Voronoi edges against <paramref name="boundaryPolygon"/> and
        /// populates <see cref="VoronoiRidgeResult.ClippedEdges"/> and
        /// <see cref="VoronoiRidgeResult.ShapePoints"/>.
        /// </summary>
        public void ClipAndCollect(
            VoronoiRidgeResult result,
            List<XYZ> boundaryPolygon,
            List<DrainGroup> groups)
        {
            result.ClippedEdges.Clear();
            result.ShapePoints.Clear();
            result.ShapePointGroupMap.Clear();

            var collector = new List<(XYZ point, List<int> groupIndices, string note)>();

            // ── 1. Clip each raw edge ─────────────────────────────────────────────
            foreach (var edge in result.RawVoronoiEdges)
            {
                // Find which two groups own this edge (nearest sites to the edge midpoint)
                var mid = Midpoint(edge.Start, edge.End);
                var nearest = FindTwoNearestGroups(mid, groups);

                bool startInside = PointInPolygon(edge.Start, boundaryPolygon);
                bool endInside = PointInPolygon(edge.End, boundaryPolygon);

                if (startInside && endInside)
                {
                    // Fully inside — accept
                    result.ClippedEdges.Add(edge);
                }
                else if (!startInside && !endInside)
                {
                    // Both outside — check if segment crosses boundary.
                    // FIX C: deduplicate intersections before counting so that a segment
                    // touching a polygon corner (hit by two adjacent edges) is not counted
                    // as two distinct intersection points.
                    var rawIntersections = SegmentPolygonIntersections(edge.Start, edge.End, boundaryPolygon);
                    var intersections = DeduplicatePoints(rawIntersections, SnapTolerance);

                    if (intersections.Count >= 2)
                    {
                        // Segment passes through the polygon (entry + exit)
                        result.ClippedEdges.Add((intersections[0], intersections[1]));
                        AddPoint(collector, intersections[0], nearest, "Boundary clip (pass-through start)", SnapTolerance);
                        AddPoint(collector, intersections[1], nearest, "Boundary clip (pass-through end)", SnapTolerance);
                    }
                    // Else: fully outside — discard
                }
                else
                {
                    // Partially inside — clip to boundary.
                    // FIX C: also deduplicate here to avoid picking a spurious corner duplicate
                    // as the clip point when the inside end is exactly on the boundary.
                    var rawIntersections = SegmentPolygonIntersections(edge.Start, edge.End, boundaryPolygon);
                    var intersections = DeduplicatePoints(rawIntersections, SnapTolerance);

                    XYZ insideEnd = startInside ? edge.Start : edge.End;
                    XYZ clipPoint = intersections.Count > 0 ? intersections[0] : null;

                    if (clipPoint != null)
                    {
                        result.ClippedEdges.Add((insideEnd, clipPoint));
                        AddPoint(collector, clipPoint, nearest, "Boundary clip intersection", SnapTolerance);
                    }
                }
            }

            // ── 2. Voronoi vertices inside boundary ───────────────────────────────
            foreach (var v in result.RawVoronoiVertices)
            {
                if (!PointInPolygon(v, boundaryPolygon)) continue;
                var nearestGroups = FindThreeNearestGroups(v, groups);
                AddPoint(collector, v, nearestGroups, "Voronoi vertex (circumcenter)", SnapTolerance);
            }

            // ── 3. Edge-to-edge intersections ─────────────────────────────────────
            for (int i = 0; i < result.ClippedEdges.Count; i++)
            {
                for (int j = i + 1; j < result.ClippedEdges.Count; j++)
                {
                    if (TrySegmentIntersect(
                        result.ClippedEdges[i].Start, result.ClippedEdges[i].End,
                        result.ClippedEdges[j].Start, result.ClippedEdges[j].End,
                        out XYZ ip))
                    {
                        if (PointInPolygon(ip, boundaryPolygon))
                        {
                            var g = FindTwoNearestGroups(ip, groups);
                            AddPoint(collector, ip, g, "Edge-to-edge intersection", SnapTolerance);
                        }
                    }
                }
            }

            // ── Finalise shape points ─────────────────────────────────────────────
            for (int i = 0; i < collector.Count; i++)
            {
                result.ShapePoints.Add(collector[i].point);
                result.ShapePointGroupMap[i] = collector[i].groupIndices;
            }
        }

        // ── Point-in-polygon (ray casting) ────────────────────────────────────────

        public static bool PointInPolygon(XYZ p, List<XYZ> poly)
        {
            int n = poly.Count;
            bool ins = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xi = poly[i].X, yi = poly[i].Y;
                double xj = poly[j].X, yj = poly[j].Y;
                if (((yi > p.Y) != (yj > p.Y)) &&
                    (p.X < (xj - xi) * (p.Y - yi) / (yj - yi) + xi))
                    ins = !ins;
            }
            return ins;
        }

        // ── Segment vs polygon intersections ─────────────────────────────────────

        private static List<XYZ> SegmentPolygonIntersections(XYZ a, XYZ b, List<XYZ> poly)
        {
            var result = new List<XYZ>();
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                var c = poly[i];
                var d = poly[(i + 1) % n];
                if (TrySegmentIntersect(a, b, c, d, out XYZ ip))
                    result.Add(ip);
            }
            return result;
        }

        // ── FIX C helper: de-duplicate a point list within a snap tolerance ───────

        /// <summary>
        /// Returns a new list with near-duplicate points removed (first occurrence wins).
        /// Prevents polygon-corner double-hits from inflating the intersection count.
        /// </summary>
        private static List<XYZ> DeduplicatePoints(List<XYZ> pts, double snapTol)
        {
            var unique = new List<XYZ>(pts.Count);
            foreach (var p in pts)
            {
                bool isDuplicate = false;
                foreach (var u in unique)
                {
                    if (Dist2D(u, p) < snapTol) { isDuplicate = true; break; }
                }
                if (!isDuplicate)
                    unique.Add(p);
            }
            return unique;
        }

        // ── Segment-segment intersection ─────────────────────────────────────────

        private static bool TrySegmentIntersect(XYZ a, XYZ b, XYZ c, XYZ d, out XYZ ip)
        {
            ip = null;
            double r1 = (b.X - a.X), r2 = (b.Y - a.Y);
            double s1 = (d.X - c.X), s2 = (d.Y - c.Y);
            double denom = r1 * s2 - r2 * s1;
            if (Math.Abs(denom) < 1e-10) return false; // parallel

            double t = ((c.X - a.X) * s2 - (c.Y - a.Y) * s1) / denom;
            double u = ((c.X - a.X) * r2 - (c.Y - a.Y) * r1) / denom;

            if (t < 0 || t > 1 || u < 0 || u > 1) return false;

            ip = new XYZ(a.X + t * r1, a.Y + t * r2, 0);
            return true;
        }

        // ── Group proximity helpers ───────────────────────────────────────────────

        private static List<int> FindTwoNearestGroups(XYZ p, List<DrainGroup> groups)
        {
            return FindNNearestGroups(p, groups, 2);
        }

        private static List<int> FindThreeNearestGroups(XYZ p, List<DrainGroup> groups)
        {
            return FindNNearestGroups(p, groups, 3);
        }

        private static List<int> FindNNearestGroups(XYZ p, List<DrainGroup> groups, int n)
        {
            var sorted = new SortedList<double, int>();
            foreach (var g in groups)
            {
                double d = Dist2D(p, g.Centroid);
                while (sorted.ContainsKey(d)) d += 1e-12; // avoid key collision
                sorted.Add(d, g.GroupIndex);
            }

            var result = new List<int>();
            int count = 0;
            foreach (var kv in sorted)
            {
                result.Add(kv.Value);
                if (++count >= n) break;
            }
            return result;
        }

        // ── De-duplicating point collector ────────────────────────────────────────

        private static void AddPoint(
            List<(XYZ point, List<int> groupIndices, string note)> collector,
            XYZ newPt,
            List<int> groupIndices,
            string note,
            double snapTol)
        {
            foreach (var existing in collector)
            {
                if (Dist2D(existing.point, newPt) < snapTol) return; // duplicate
            }
            collector.Add((newPt, groupIndices, note));
        }

        // ── Math utilities ────────────────────────────────────────────────────────

        private static XYZ Midpoint(XYZ a, XYZ b)
            => new XYZ((a.X + b.X) / 2, (a.Y + b.Y) / 2, 0);

        private static double Dist2D(XYZ a, XYZ b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}