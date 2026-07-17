// =======================================================
// File: DelaunayTriangulation.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.V021
// New in V021 — Ridge Point Detection (opt-in).
// UPDATED: now also exposes the raw triangulation (ComputeTriangles)
// and derives Voronoi edges from it (ComputeVoronoiEdges) — the
// Voronoi diagram is the mathematical DUAL of Delaunay: each Delaunay
// edge shared by two triangles corresponds to a Voronoi edge
// connecting those two triangles' circumcenters. For a hull edge
// (only one triangle), the Voronoi edge is an unbounded ray from that
// triangle's circumcenter, perpendicular to the Delaunay edge —
// callers must clip this ray to a finite bound (e.g. roof bounding
// box) themselves; see RidgePointEngine.
//
// Minimal Bowyer-Watson Delaunay triangulation over a 2D point set
// (Autodesk.Revit.DB.UV). Used to build the drain-group adjacency
// graph: a group is "adjacent" to another if a Delaunay edge connects
// their centers — i.e. no other group's center lies "between" them,
// per the confirmed spec.
//
// This is a self-contained O(n^2)-ish implementation appropriate for
// the expected small N (drain group counts are typically single or
// low double digits per roof) — not intended for large point clouds.
// =======================================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByPoint.V021.Core.Engine
{
    /// <summary>
    /// One Delaunay triangle over the input point indices, with its
    /// precomputed circumcenter. Exposed publicly (was private) so
    /// ComputeVoronoiEdges — and any future caller — can consume the
    /// raw triangulation, not just the deduplicated edge list.
    /// </summary>
    public struct DelaunayTriangleInfo
    {
        public int A, B, C;
        public UV Circumcenter;

        /// <summary>Circumradius (not squared) — used by callers to order multiple triangles/junctions by "tightness" (smallest first).</summary>
        public double CircumRadius;
    }

    /// <summary>
    /// One Voronoi edge, corresponding to exactly one Delaunay adjacency
    /// (GroupAIndex/GroupBIndex, using the SAME indices as the input point
    /// list passed to ComputeVoronoiEdges). Bounded (both endpoints known)
    /// unless IsRay is true, in which case only Start + RayDirection are
    /// meaningful and the caller must clip it to a finite length.
    /// </summary>
    public class VoronoiEdge
    {
        public int PointAIndex;
        public int PointBIndex;

        /// <summary>Start point — always the known circumcenter (for both bounded edges and rays).</summary>
        public UV Start;

        /// <summary>End point — only meaningful when IsRay is false.</summary>
        public UV End;

        /// <summary>True if this edge is an unbounded ray (hull edge, only one adjacent triangle) rather than a finite segment.</summary>
        public bool IsRay;

        /// <summary>Direction to extend the ray in, when IsRay is true (already normalized, pointing away from the triangulation interior).</summary>
        public UV RayDirection;
    }

    public static class DelaunayTriangulation
    {
        private struct Triangle
        {
            public int A, B, C;
            public UV Circumcenter;
            public double CircumRadiusSq;
        }

        /// <summary>
        /// Computes the Delaunay triangulation and returns the raw triangle list
        /// (with circumcenters), for callers that need more than just the
        /// deduplicated edge list — e.g. ComputeVoronoiEdges below. Re-runs the
        /// same Bowyer-Watson construction as ComputeEdges; kept as a separate
        /// entry point so ComputeEdges' existing return type/behavior for
        /// existing callers (adjacency graph) is untouched.
        /// </summary>
        public static List<DelaunayTriangleInfo> ComputeTriangles(List<UV> points)
        {
            var raw = ComputeTrianglesInternal(points);
            return raw.Select(t => new DelaunayTriangleInfo
            {
                A = t.A,
                B = t.B,
                C = t.C,
                Circumcenter = t.Circumcenter,
                CircumRadius = Math.Sqrt(t.CircumRadiusSq)
            }).ToList();
        }

        /// <summary>
        /// Derives Voronoi edges from the Delaunay triangulation (the dual graph).
        /// Each Delaunay edge shared by exactly two triangles produces one bounded
        /// Voronoi edge (the two triangles' circumcenters). Each Delaunay edge
        /// belonging to only one triangle (a convex-hull edge of the point set)
        /// produces one Voronoi RAY starting at that triangle's circumcenter,
        /// directed perpendicular to the Delaunay edge and away from the
        /// triangulation's interior — callers must clip this ray themselves
        /// (confirmed: clip to the roof's bounding box).
        /// Degenerate inputs (fewer than 3 points, or collinear points, where
        /// ComputeTriangles/ComputeEdges falls back to a simple nearest-neighbor
        /// chain) have no well-defined Voronoi edges — returns an empty list in
        /// that case; callers should treat those adjacencies as unbounded rays
        /// through each pair's own midpoint instead (fallback, handled by caller).
        /// </summary>
        public static List<VoronoiEdge> ComputeVoronoiEdges(List<UV> points)
        {
            var result = new List<VoronoiEdge>();
            if (points.Count < 3 || ArePointsCollinear(points))
                return result; // degenerate — caller falls back to midpoint-perpendicular method

            List<Triangle> triangles = ComputeTrianglesInternal(points);

            // Map each Delaunay edge -> list of triangles that contain it (1 or 2).
            var edgeToTriangles = new Dictionary<(int, int), List<Triangle>>();
            void Register(Triangle t, int i1, int i2)
            {
                var key = i1 < i2 ? (i1, i2) : (i2, i1);
                if (!edgeToTriangles.TryGetValue(key, out var list))
                {
                    list = new List<Triangle>();
                    edgeToTriangles[key] = list;
                }
                list.Add(t);
            }

            foreach (var t in triangles)
            {
                Register(t, t.A, t.B);
                Register(t, t.B, t.C);
                Register(t, t.C, t.A);
            }

            foreach (var kvp in edgeToTriangles)
            {
                (int i1, int i2) = kvp.Key;
                List<Triangle> owners = kvp.Value;

                if (owners.Count == 2)
                {
                    result.Add(new VoronoiEdge
                    {
                        PointAIndex = i1,
                        PointBIndex = i2,
                        Start = owners[0].Circumcenter,
                        End = owners[1].Circumcenter,
                        IsRay = false
                    });
                }
                else if (owners.Count == 1)
                {
                    // Hull edge — Voronoi edge is a ray from this triangle's
                    // circumcenter, perpendicular to the Delaunay edge (i1,i2),
                    // directed AWAY from the triangulation's centroid so it points
                    // outward (toward the hull exterior) rather than back inward.
                    UV p1 = points[i1], p2 = points[i2];
                    UV edgeDir = new UV(p2.U - p1.U, p2.V - p1.V);
                    double len = Math.Sqrt(edgeDir.U * edgeDir.U + edgeDir.V * edgeDir.V);
                    if (len < 1e-9) continue;
                    edgeDir = new UV(edgeDir.U / len, edgeDir.V / len);

                    // Two perpendicular candidates; pick the one pointing away
                    // from the overall point-set centroid (outward).
                    UV perp1 = new UV(-edgeDir.V, edgeDir.U);
                    UV perp2 = new UV(edgeDir.V, -edgeDir.U);

                    double cx = points.Average(p => p.U), cy = points.Average(p => p.V);
                    UV toCircum = new UV(owners[0].Circumcenter.U - cx, owners[0].Circumcenter.V - cy);

                    double dot1 = perp1.U * toCircum.U + perp1.V * toCircum.V;
                    double dot2 = perp2.U * toCircum.U + perp2.V * toCircum.V;
                    UV outward = dot1 >= dot2 ? perp1 : perp2;

                    result.Add(new VoronoiEdge
                    {
                        PointAIndex = i1,
                        PointBIndex = i2,
                        Start = owners[0].Circumcenter,
                        End = default,
                        IsRay = true,
                        RayDirection = outward
                    });
                }
                // owners.Count should never be 0 (every edge belongs to at least
                // one triangle in a valid triangulation) — silently skip if it
                // somehow happens, rather than throwing.
            }

            return result;
        }

        /// <summary>
        /// Computes the Delaunay triangulation of the given points and returns
        /// the unique set of edges as (index, index) pairs (indices into the
        /// input list, i less than j).
        /// </summary>
        public static List<(int, int)> ComputeEdges(List<UV> points)
        {
            var edgeSet = new HashSet<(int, int)>();

            int n = points.Count;
            if (n < 2) return new List<(int, int)>();
            if (n == 2) { edgeSet.Add((0, 1)); return edgeSet.ToList(); }

            // Degenerate case: all points collinear (common for 2-3 drain groups
            // in a row) — Delaunay triangles can't form. Fall back to connecting
            // consecutive points along the dominant axis, which is the only
            // sensible "adjacency" for a collinear layout anyway.
            if (ArePointsCollinear(points))
            {
                var sorted = points
                    .Select((p, idx) => (p, idx))
                    .OrderBy(t => t.p.U).ThenBy(t => t.p.V)
                    .ToList();
                for (int k = 0; k < sorted.Count - 1; k++)
                {
                    int i1 = sorted[k].idx, i2 = sorted[k + 1].idx;
                    edgeSet.Add(i1 < i2 ? (i1, i2) : (i2, i1));
                }
                return edgeSet.ToList();
            }

            List<Triangle> triangles = ComputeTrianglesInternal(points);

            foreach (var t in triangles)
            {
                AddEdge(edgeSet, t.A, t.B);
                AddEdge(edgeSet, t.B, t.C);
                AddEdge(edgeSet, t.C, t.A);
            }

            return edgeSet.ToList();
        }

        /// <summary>
        /// Shared Bowyer-Watson construction used by both ComputeEdges and
        /// ComputeVoronoiEdges/ComputeTriangles, so the two entry points can
        /// never disagree with each other about the actual triangulation.
        /// Returns an empty list for degenerate inputs (n &lt; 3 or collinear) —
        /// callers handle those cases themselves (ComputeEdges via its own
        /// collinear fallback; ComputeVoronoiEdges by returning no edges).
        /// </summary>
        private static List<Triangle> ComputeTrianglesInternal(List<UV> points)
        {
            int n = points.Count;
            if (n < 3 || ArePointsCollinear(points)) return new List<Triangle>();
            double minU = points.Min(p => p.U), maxU = points.Max(p => p.U);
            double minV = points.Min(p => p.V), maxV = points.Max(p => p.V);
            double dMax = Math.Max(maxU - minU, maxV - minV) * 10 + 10;
            double midU = (minU + maxU) / 2, midV = (minV + maxV) / 2;

            var pts = new List<UV>(points);
            int superA = pts.Count, superB = pts.Count + 1, superC = pts.Count + 2;
            pts.Add(new UV(midU - 2 * dMax, midV - dMax));
            pts.Add(new UV(midU + 2 * dMax, midV - dMax));
            pts.Add(new UV(midU, midV + 2 * dMax));

            var triangles = new List<Triangle> { MakeTriangle(pts, superA, superB, superC) };

            for (int pi = 0; pi < points.Count; pi++)
            {
                UV p = pts[pi];
                var badTriangles = new List<Triangle>();

                foreach (var t in triangles)
                {
                    double dx = p.U - t.Circumcenter.U;
                    double dy = p.V - t.Circumcenter.V;
                    if (dx * dx + dy * dy <= t.CircumRadiusSq + 1e-9)
                        badTriangles.Add(t);
                }

                // Find boundary of the polygonal hole (edges not shared by 2 bad triangles).
                var polygon = new List<(int, int)>();
                foreach (var t in badTriangles)
                {
                    var edges = new[] { (t.A, t.B), (t.B, t.C), (t.C, t.A) };
                    foreach (var e in edges)
                    {
                        bool shared = badTriangles.Any(other =>
                            !EqualsTriangle(other, t) && TriangleHasEdge(other, e));
                        if (!shared) polygon.Add(e);
                    }
                }

                triangles.RemoveAll(t => badTriangles.Any(bt => EqualsTriangle(bt, t)));

                foreach (var e in polygon)
                    triangles.Add(MakeTriangle(pts, e.Item1, e.Item2, pi));
            }

            // Remove any triangle touching the super-triangle vertices.
            triangles.RemoveAll(t =>
                t.A >= points.Count || t.B >= points.Count || t.C >= points.Count);

            return triangles;
        }

        private static bool ArePointsCollinear(List<UV> points)
        {
            if (points.Count < 3) return true;
            UV p0 = points[0];
            UV dir = new UV(0, 0);
            for (int i = 1; i < points.Count; i++)
            {
                double du = points[i].U - p0.U, dv = points[i].V - p0.V;
                if (Math.Abs(du) > 1e-9 || Math.Abs(dv) > 1e-9)
                {
                    dir = new UV(du, dv);
                    break;
                }
            }
            if (dir.U == 0 && dir.V == 0) return true; // all points coincide

            foreach (var p in points)
            {
                double cross = (p.U - p0.U) * dir.V - (p.V - p0.V) * dir.U;
                if (Math.Abs(cross) > 1e-6) return false;
            }
            return true;
        }

        private static void AddEdge(HashSet<(int, int)> set, int i, int j)
        {
            if (i == j) return;
            set.Add(i < j ? (i, j) : (j, i));
        }

        private static bool TriangleHasEdge(Triangle t, (int, int) e)
        {
            var verts = new[] { t.A, t.B, t.C };
            return verts.Contains(e.Item1) && verts.Contains(e.Item2);
        }

        private static bool EqualsTriangle(Triangle x, Triangle y)
        {
            var xs = new[] { x.A, x.B, x.C }.OrderBy(v => v).ToArray();
            var ys = new[] { y.A, y.B, y.C }.OrderBy(v => v).ToArray();
            return xs[0] == ys[0] && xs[1] == ys[1] && xs[2] == ys[2];
        }

        private static Triangle MakeTriangle(List<UV> pts, int a, int b, int c)
        {
            UV pa = pts[a], pb = pts[b], pc = pts[c];
            ComputeCircumcircle(pa, pb, pc, out UV center, out double radiusSq);
            return new Triangle { A = a, B = b, C = c, Circumcenter = center, CircumRadiusSq = radiusSq };
        }

        private static void ComputeCircumcircle(UV a, UV b, UV c, out UV center, out double radiusSq)
        {
            double ax = a.U, ay = a.V, bx = b.U, by = b.V, cx = c.U, cy = c.V;
            double d = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));

            if (Math.Abs(d) < 1e-12)
            {
                // Degenerate/near-collinear triangle: fall back to a center far away
                // so it never wins an in-circle test in practice.
                center = new UV(ax, ay);
                radiusSq = double.MaxValue;
                return;
            }

            double ax2ay2 = ax * ax + ay * ay;
            double bx2by2 = bx * bx + by * by;
            double cx2cy2 = cx * cx + cy * cy;

            double ux = (ax2ay2 * (by - cy) + bx2by2 * (cy - ay) + cx2cy2 * (ay - by)) / d;
            double uy = (ax2ay2 * (cx - bx) + bx2by2 * (ax - cx) + cx2cy2 * (bx - ax)) / d;

            center = new UV(ux, uy);
            double dx = ax - ux, dy = ay - uy;
            radiusSq = dx * dx + dy * dy;
        }
    }
}
