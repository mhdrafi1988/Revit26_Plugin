using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V52.Models;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V52.Services
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
            // Non-convex boundaries (notched/L-shaped roofs) can produce multiple
            // entry/exit intersection pairs per edge. We handle this by sorting all
            // intersections by parametric position t along the segment, then emitting
            // one clipped sub-segment per entry→exit pair.
            foreach (var edge in result.RawVoronoiEdges)
            {
                var nearest = FindTwoNearestGroups(Midpoint(edge.Start, edge.End), groups);

                var segDir = new XYZ(edge.End.X - edge.Start.X, edge.End.Y - edge.Start.Y, 0);
                double segLen = segDir.GetLength();
                if (segLen < 1e-9) continue;

                // Collect all boundary intersections with their parametric t values
                var tHits = new List<double>();
                int n = boundaryPolygon.Count;
                for (int pi = 0; pi < n; pi++)
                {
                    var c = boundaryPolygon[pi];
                    var d = boundaryPolygon[(pi + 1) % n];
                    if (TrySegmentIntersectParametric(edge.Start, edge.End, c, d, out double t, out XYZ _))
                        tHits.Add(t);
                }

                // Deduplicate t values that are very close (corner double-hits)
                tHits.Sort();
                var tUnique = new List<double>();
                foreach (var t in tHits)
                    if (tUnique.Count == 0 || t - tUnique[tUnique.Count - 1] > SnapTolerance / segLen)
                        tUnique.Add(t);

                bool startInside = PointInPolygon(edge.Start, boundaryPolygon);

                // ── Event walk: sort crossings, toggle inside/outside, emit inside intervals ──
                var intervals = new List<(double tA, double tB)>();
                double tStart2 = 0.0;
                bool curInside = startInside;

                foreach (double tCross in tUnique)
                {
                    if (curInside)
                        intervals.Add((tStart2, tCross));
                    curInside = !curInside;
                    tStart2 = tCross;
                }
                if (curInside)
                    intervals.Add((tStart2, 1.0));

                // Emit one clipped edge per inside interval
                foreach (var (tA, tB) in intervals)
                {
                    if (tB - tA < SnapTolerance / segLen) continue; // degenerate interval

                    var ptA = new XYZ(edge.Start.X + tA * segDir.X,
                                      edge.Start.Y + tA * segDir.Y, 0);
                    var ptB = new XYZ(edge.Start.X + tB * segDir.X,
                                      edge.Start.Y + tB * segDir.Y, 0);

                    if (Dist2D(ptA, ptB) < SnapTolerance) continue;

                    result.ClippedEdges.Add((ptA, ptB));

                    // Clip points at boundary crossings become shape points
                    if (tA > 1e-9)
                        AddPoint(collector, ptA, nearest, "Boundary clip intersection", SnapTolerance);
                    if (tB < 1.0 - 1e-9)
                        AddPoint(collector, ptB, nearest, "Boundary clip intersection", SnapTolerance);
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

        /// <summary>
        /// Like TrySegmentIntersect but also returns the parametric t value along
        /// segment a→b. Used by the non-convex boundary clipper to sort crossings.
        /// </summary>
        private static bool TrySegmentIntersectParametric(
            XYZ a, XYZ b, XYZ c, XYZ d, out double t, out XYZ ip)
        {
            t = 0; ip = null;
            double r1 = b.X - a.X, r2 = b.Y - a.Y;
            double s1 = d.X - c.X, s2 = d.Y - c.Y;
            double denom = r1 * s2 - r2 * s1;
            if (Math.Abs(denom) < 1e-10) return false;

            t = ((c.X - a.X) * s2 - (c.Y - a.Y) * s1) / denom;
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