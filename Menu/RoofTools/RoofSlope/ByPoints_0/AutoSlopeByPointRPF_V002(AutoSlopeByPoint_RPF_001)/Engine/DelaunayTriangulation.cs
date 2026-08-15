// =======================================================
// File: DelaunayTriangulation.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.RPF_001
// New in RPF_001 — Ridge Point Detection (opt-in).
//
// ASSUMPTION / PROVENANCE NOTE: this suite previously carried a Ridge
// Point Detection feature in an earlier AutoSlopeByPoint version
// (V018→V021) that is NOT present in the V026 codebase this fork is
// based on, and the original tested source for that feature is not
// available to reference verbatim. This file is a FRESH implementation
// written from the confirmed algorithm/behavior spec (see chat history),
// not a byte-for-byte port. Treat as unverified-against-original until
// exercised against real roof geometry.
//
// Minimal Bowyer-Watson Delaunay triangulation over a 2D point set,
// operating in a genuine orthonormal in-plane basis (see PlaneBasis in
// RidgePointEngine.cs) rather than a Revit Face's raw UV parametric
// space — UV space is NOT guaranteed orthonormal/unit-scale for a
// PlanarFace, and building metric geometry (circumcenters, distances)
// directly in UV was a known, previously-fixed source of silently
// distorted world positions. All points here are plane-local 2D
// coordinates (U,V) from PlaneBasis.ToPlane(), already safe to use
// metrically.
//
// Used to build the drain-group adjacency graph: a group is "adjacent"
// to another if a Delaunay edge connects their centers — i.e. no other
// group's center lies "between" them. The Voronoi diagram (dual of
// this triangulation) then gives the actual ridge line between two
// adjacent groups' territories.
//
// Self-contained O(n^2)-ish implementation appropriate for the
// expected small N (drain group counts are typically single or low
// double digits per roof) — not intended for large point clouds.
// =======================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByPointRPF.V002.RPF_001.Core.Engine
{
    /// <summary>Simple 2D point in plane-local (orthonormal) coordinates.</summary>
    public struct Pt2
    {
        public double U, V;
        public Pt2(double u, double v) { U = u; V = v; }
        public double DistanceTo(Pt2 o) => Math.Sqrt((U - o.U) * (U - o.U) + (V - o.V) * (V - o.V));
    }

    /// <summary>
    /// One Delaunay triangle over input point indices, with its
    /// precomputed circumcenter (in the same plane-local 2D space).
    /// Exposed publicly so ComputeVoronoiEdges — and RidgePointEngine's
    /// junction detection — can consume the raw triangulation.
    /// </summary>
    public struct DelaunayTriangleInfo
    {
        public int A, B, C;
        public Pt2 Circumcenter;
    }

    /// <summary>
    /// One Voronoi edge, corresponding to exactly one Delaunay adjacency
    /// (GroupAIndex/GroupBIndex — same indices as the input point list
    /// passed to ComputeVoronoiEdges). Bounded (both endpoints known)
    /// unless IsRay is true, in which case only Start + RayDirection are
    /// meaningful and the caller must clip it to a finite length.
    /// </summary>
    public class VoronoiEdge
    {
        public int GroupAIndex;
        public int GroupBIndex;
        public Pt2 Start;
        public Pt2 End;          // only meaningful if !IsRay
        public bool IsRay;
        public Pt2 RayDirection; // unit vector, only meaningful if IsRay
    }

    public static class DelaunayTriangulation
    {
        private const double EPS = 1e-9;

        /// <summary>
        /// Returns the set of unique undirected adjacency pairs (i,j), i&lt;j,
        /// derived from a Bowyer-Watson Delaunay triangulation of points.
        /// </summary>
        public static HashSet<(int A, int B)> ComputeAdjacency(IReadOnlyList<Pt2> points)
        {
            var edges = new HashSet<(int, int)>();
            var triangles = ComputeTriangles(points);
            foreach (var t in triangles)
            {
                AddEdge(edges, t.A, t.B);
                AddEdge(edges, t.B, t.C);
                AddEdge(edges, t.C, t.A);
            }
            return edges;
        }

        private static void AddEdge(HashSet<(int, int)> set, int a, int b)
        {
            set.Add(a < b ? (a, b) : (b, a));
        }

        /// <summary>
        /// Bowyer-Watson triangulation. Returns empty for &lt;3 points or
        /// fully collinear input (both are degenerate cases the caller
        /// falls back on).
        /// </summary>
        public static List<DelaunayTriangleInfo> ComputeTriangles(IReadOnlyList<Pt2> points)
        {
            var result = new List<DelaunayTriangleInfo>();
            int n = points.Count;
            if (n < 3) return result;
            if (IsCollinear(points)) return result;

            // Super-triangle large enough to contain all input points.
            double minU = points.Min(p => p.U), maxU = points.Max(p => p.U);
            double minV = points.Min(p => p.V), maxV = points.Max(p => p.V);
            double dx = maxU - minU, dy = maxV - minV;
            double deltaMax = Math.Max(dx, dy) * 10 + 10;
            double midU = (minU + maxU) / 2, midV = (minV + maxV) / 2;

            var pts = new List<Pt2>(points);
            int superA = pts.Count; pts.Add(new Pt2(midU - 2 * deltaMax, midV - deltaMax));
            int superB = pts.Count; pts.Add(new Pt2(midU, midV + 2 * deltaMax));
            int superC = pts.Count; pts.Add(new Pt2(midU + 2 * deltaMax, midV - deltaMax));

            var tris = new List<(int A, int B, int C)> { (superA, superB, superC) };

            for (int pi = 0; pi < n; pi++)
            {
                Pt2 p = pts[pi];
                var badTriangles = new List<(int A, int B, int C)>();

                foreach (var t in tris)
                {
                    if (InCircumcircle(pts[t.A], pts[t.B], pts[t.C], p))
                        badTriangles.Add(t);
                }

                // Find boundary of the polygonal hole (edges not shared by two bad triangles)
                var polygon = new List<(int, int)>();
                foreach (var t in badTriangles)
                {
                    var edgesOfT = new[] { (t.A, t.B), (t.B, t.C), (t.C, t.A) };
                    foreach (var e in edgesOfT)
                    {
                        bool shared = badTriangles.Any(other =>
                            !EqualTri(other, t) && TriHasEdge(other, e));
                        if (!shared) polygon.Add(e);
                    }
                }

                tris.RemoveAll(t => badTriangles.Any(bt => EqualTri(bt, t)));

                foreach (var e in polygon)
                    tris.Add((e.Item1, e.Item2, pi));
            }

            // Remove any triangle referencing a super-vertex
            tris.RemoveAll(t => t.A >= n || t.B >= n || t.C >= n);

            foreach (var t in tris)
            {
                Pt2 cc = Circumcenter(points[t.A], points[t.B], points[t.C]);
                result.Add(new DelaunayTriangleInfo { A = t.A, B = t.B, C = t.C, Circumcenter = cc });
            }

            return result;
        }

        /// <summary>
        /// Derives Voronoi edges as the dual of the Delaunay triangulation:
        /// each Delaunay edge shared by two triangles maps to a bounded
        /// Voronoi edge between those triangles' circumcenters. A Delaunay
        /// edge belonging to only one triangle (hull edge) maps to an
        /// unbounded ray from that triangle's circumcenter, perpendicular
        /// to the Delaunay edge, pointing away from the triangle's
        /// interior. Callers (RidgePointEngine) must clip rays to a finite
        /// bound (roof bounding box).
        /// </summary>
        public static List<VoronoiEdge> ComputeVoronoiEdges(IReadOnlyList<Pt2> points)
        {
            var result = new List<VoronoiEdge>();
            var triangles = ComputeTriangles(points);
            if (triangles.Count == 0) return result;

            // Map each undirected Delaunay edge -> list of triangle indices containing it
            var edgeToTris = new Dictionary<(int, int), List<int>>();
            for (int ti = 0; ti < triangles.Count; ti++)
            {
                var t = triangles[ti];
                foreach (var e in new[] { Key(t.A, t.B), Key(t.B, t.C), Key(t.C, t.A) })
                {
                    if (!edgeToTris.TryGetValue(e, out var list))
                        edgeToTris[e] = list = new List<int>();
                    list.Add(ti);
                }
            }

            foreach (var kvp in edgeToTris)
            {
                var (a, b) = kvp.Key;
                var triIdxs = kvp.Value;

                if (triIdxs.Count == 2)
                {
                    var t1 = triangles[triIdxs[0]];
                    var t2 = triangles[triIdxs[1]];
                    result.Add(new VoronoiEdge
                    {
                        GroupAIndex = a,
                        GroupBIndex = b,
                        Start = t1.Circumcenter,
                        End = t2.Circumcenter,
                        IsRay = false
                    });
                }
                else if (triIdxs.Count == 1)
                {
                    var t = triangles[triIdxs[0]];
                    Pt2 pa = points[a], pb = points[b];

                    // Perpendicular to edge a-b
                    double ex = pb.U - pa.U, ey = pb.V - pa.V;
                    double perpU = -ey, perpV = ex;
                    double len = Math.Sqrt(perpU * perpU + perpV * perpV);
                    if (len < EPS) continue;
                    perpU /= len; perpV /= len;

                    // Ensure the ray points AWAY from the triangle's third vertex
                    // (i.e. away from the triangulation's interior at this hull edge).
                    int thirdIdx = t.A != a && t.A != b ? t.A : (t.B != a && t.B != b ? t.B : t.C);
                    Pt2 third = points[thirdIdx];
                    double midU = (pa.U + pb.U) / 2, midV = (pa.V + pb.V) / 2;
                    double towardThirdU = third.U - midU, towardThirdV = third.V - midV;
                    double dot = perpU * towardThirdU + perpV * towardThirdV;
                    if (dot > 0) { perpU = -perpU; perpV = -perpV; }

                    result.Add(new VoronoiEdge
                    {
                        GroupAIndex = a,
                        GroupBIndex = b,
                        Start = t.Circumcenter,
                        IsRay = true,
                        RayDirection = new Pt2(perpU, perpV)
                    });
                }
                // triIdxs.Count should never exceed 2 for a valid triangulation.
            }

            return result;
        }

        private static (int, int) Key(int a, int b) => a < b ? (a, b) : (b, a);

        private static bool EqualTri((int A, int B, int C) x, (int A, int B, int C) y)
            => x.A == y.A && x.B == y.B && x.C == y.C;

        private static bool TriHasEdge((int A, int B, int C) t, (int, int) e)
        {
            var edges = new[] { (t.A, t.B), (t.B, t.C), (t.C, t.A), (t.B, t.A), (t.C, t.B), (t.A, t.C) };
            return edges.Contains(e);
        }

        private static bool InCircumcircle(Pt2 a, Pt2 b, Pt2 c, Pt2 p)
        {
            // Standard incircle determinant test.
            double ax = a.U - p.U, ay = a.V - p.V;
            double bx = b.U - p.U, by = b.V - p.V;
            double cx = c.U - p.U, cy = c.V - p.V;

            double aSq = ax * ax + ay * ay;
            double bSq = bx * bx + by * by;
            double cSq = cx * cx + cy * cy;

            double det =
                ax * (by * cSq - bSq * cy) -
                ay * (bx * cSq - bSq * cx) +
                aSq * (bx * cy - by * cx);

            // Sign depends on triangle winding; use consistent orientation check.
            double orient = (b.U - a.U) * (c.V - a.V) - (b.V - a.V) * (c.U - a.U);
            return orient > 0 ? det > EPS : det < -EPS;
        }

        private static Pt2 Circumcenter(Pt2 a, Pt2 b, Pt2 c)
        {
            double d = 2 * (a.U * (b.V - c.V) + b.U * (c.V - a.V) + c.U * (a.V - b.V));
            if (Math.Abs(d) < EPS) return new Pt2((a.U + b.U + c.U) / 3, (a.V + b.V + c.V) / 3);

            double aSq = a.U * a.U + a.V * a.V;
            double bSq = b.U * b.U + b.V * b.V;
            double cSq = c.U * c.U + c.V * c.V;

            double ux = (aSq * (b.V - c.V) + bSq * (c.V - a.V) + cSq * (a.V - b.V)) / d;
            double uy = (aSq * (c.U - b.U) + bSq * (a.U - c.U) + cSq * (b.U - a.U)) / d;

            return new Pt2(ux, uy);
        }

        private static bool IsCollinear(IReadOnlyList<Pt2> points)
        {
            if (points.Count < 3) return true;
            Pt2 p0 = points[0], p1 = points[1];
            for (int i = 2; i < points.Count; i++)
            {
                double cross = (p1.U - p0.U) * (points[i].V - p0.V) - (p1.V - p0.V) * (points[i].U - p0.U);
                if (Math.Abs(cross) > EPS) return false;
            }
            return true;
        }
    }
}
