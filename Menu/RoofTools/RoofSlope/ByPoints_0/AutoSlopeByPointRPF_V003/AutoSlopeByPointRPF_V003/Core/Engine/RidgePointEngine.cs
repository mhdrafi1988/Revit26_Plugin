// =======================================================
// File: RidgePointEngine.cs
// Namespace: Revit26_Plugin.AutoSlopeByPointRPF.V003
// New in RPF_001 — Ridge Point Detection (opt-in, toggle in UI).
//
// ASSUMPTION / PROVENANCE NOTE: see DelaunayTriangulation.cs header —
// this is a fresh implementation from the confirmed algorithm spec,
// not a verbatim port of the original (unavailable) V021 source.
//
// Responsibilities:
//   1. Cluster the flat DrainPoints cloud into drain groups by mutual
//      proximity (union-find), using the SAME tolerance the caller
//      already uses for drain-tolerance expansion.
//   2. Build an adjacency graph over group centers via Delaunay
//      triangulation (a group can be adjacent to multiple neighbors).
//   3. Build the Voronoi diagram (dual of that same triangulation) —
//      each Delaunay adjacency maps to exactly one Voronoi edge. That
//      edge segment IS the ridge line for that pair. Degenerate cases
//      (fewer than 3 groups, or collinear group centers) fall back to
//      a midpoint-perpendicular-bisector line instead.
//   4. For each adjacent group pair (processed shortest-center-distance
//      first): search all roof vertices, keep any within tolerance of
//      the ridge line AND whose projection lands strictly between the
//      ridge segment's own endpoints (rejects points near the extended
//      infinite line beyond the real segment). Each candidate must also
//      classify (via caller-supplied per-vertex nearest-group lookup)
//      to one of the pair's own two groups, or it's dropped silently.
//      Drain vertices themselves are excluded from candidacy.
//   5. Junction detection (3+ groups meeting at a point) at Delaunay
//      triangle circumcenters — processed AFTER pairwise edges so
//      junction claims always win over pairwise claims on the same
//      vertex. Among pairwise-vs-pairwise overlap (no junction
//      involved), first-claim wins: pairs are processed shortest
//      group-center-distance first, and a vertex already claimed by
//      an earlier pair is skipped by a later one.
//   6. If NO vertex is found near a ridge line, that pair/junction is
//      skipped entirely — normal per-vertex Dijkstra slope still
//      applies there (no special ridge behavior).
//
// Ridge point ELEVATION is computed by the caller (AutoSlopeEngine),
// which has the per-group Dijkstra distances needed for the
// FartherGroup rule: Z = Z_drainBaseline + slope × max(distA, distB).
// This class only answers "where are the ridge points, and which
// group(s) do they mediate."
//
// Geometry note: all metric math (circumcenters, distances, midpoints)
// happens in a genuine orthonormal 2D basis derived from the roof top
// face's normal (see PlaneBasis below) — never in a Face's raw UV
// parametric space, which is not guaranteed orthonormal/unit-scale for
// a PlanarFace and was a known prior source of silently distorted
// world positions when used directly for metric geometry.
// =======================================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByPointRPF.V003.Core.Engine
{
    /// <summary>
    /// A genuine orthonormal 2D basis in the roof top face's plane,
    /// derived from the face normal — NOT the face's raw UV parametric
    /// space (which is not guaranteed orthonormal/unit-scale). Used for
    /// all Voronoi/Delaunay/circumcenter math so distances and angles
    /// are metrically correct in real-world feet.
    /// </summary>
    public class PlaneBasis
    {
        public readonly XYZ Origin;
        public readonly XYZ AxisU;
        public readonly XYZ AxisV;
        public readonly XYZ Normal;

        public PlaneBasis(XYZ origin, XYZ normal)
        {
            Origin = origin;
            Normal = normal.Normalize();

            // Any vector not parallel to Normal works as a seed for AxisU.
            XYZ seed = Math.Abs(Normal.DotProduct(XYZ.BasisX)) < 0.9 ? XYZ.BasisX : XYZ.BasisY;
            AxisU = (seed - Normal * seed.DotProduct(Normal)).Normalize();
            AxisV = Normal.CrossProduct(AxisU).Normalize();
        }

        public Pt2 ToPlane(XYZ world)
        {
            XYZ rel = world - Origin;
            return new Pt2(rel.DotProduct(AxisU), rel.DotProduct(AxisV));
        }

        public XYZ ToWorld(Pt2 plane)
        {
            return Origin + AxisU * plane.U + AxisV * plane.V;
        }
    }

    public class RidgePairResult
    {
        public int GroupAIndex;
        public int GroupBIndex;
        public List<int> MatchedVertexIndices = new List<int>();
        public XYZ RidgeLineStart;
        public XYZ RidgeLineEnd;
        public bool UsedVoronoiEdge;
        /// <summary>True only when NOTHING was found near the ridge line at all (falls back to standard Dijkstra slope for this pair's territory).</summary>
        public bool TrueSkipped;
        public bool Skipped => MatchedVertexIndices.Count == 0;
    }

    public class DrainGroup
    {
        public int Index;
        public List<XYZ> Points = new List<XYZ>();
        public XYZ Center;
    }

    public static class RidgePointEngine
    {
        /// <summary>
        /// Clusters flat drain points into groups by mutual proximity
        /// (union-find). Two points are in the same group if their
        /// distance is &lt;= groupToleranceFt (directly or transitively
        /// via a chain of other points).
        /// </summary>
        public static List<DrainGroup> ClusterDrainPoints(List<XYZ> drainPoints, double groupToleranceFt)
        {
            int n = drainPoints.Count;
            var parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;

            int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
            void Union(int a, int b) { int ra = Find(a), rb = Find(b); if (ra != rb) parent[ra] = rb; }

            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    if (drainPoints[i].DistanceTo(drainPoints[j]) <= groupToleranceFt)
                        Union(i, j);

            var groupsByRoot = new Dictionary<int, DrainGroup>();
            for (int i = 0; i < n; i++)
            {
                int root = Find(i);
                if (!groupsByRoot.TryGetValue(root, out var g))
                {
                    g = new DrainGroup { Index = groupsByRoot.Count };
                    groupsByRoot[root] = g;
                }
                g.Points.Add(drainPoints[i]);
            }

            var groups = groupsByRoot.Values.ToList();
            // Re-index sequentially (dictionary insertion order isn't guaranteed stable index-wise otherwise)
            for (int gi = 0; gi < groups.Count; gi++)
            {
                groups[gi].Index = gi;
                double sx = 0, sy = 0, sz = 0;
                foreach (var p in groups[gi].Points) { sx += p.X; sy += p.Y; sz += p.Z; }
                int c = groups[gi].Points.Count;
                groups[gi].Center = new XYZ(sx / c, sy / c, sz / c);
            }

            return groups;
        }

        /// <summary>
        /// Builds Voronoi edges (ridge lines) between adjacent drain
        /// groups. Falls back to per-pair midpoint-perpendicular lines
        /// when the true Voronoi diagram is unavailable (fewer than 3
        /// groups, or collinear group centers).
        /// </summary>
        public static List<VoronoiEdge> BuildVoronoiEdges(
            List<DrainGroup> groups, PlaneBasis basis, double clipRadiusFt)
        {
            var planePoints = groups.Select(g => basis.ToPlane(g.Center)).ToList();
            var edges = DelaunayTriangulation.ComputeVoronoiEdges(planePoints);

            // Clip unbounded rays to a finite length so downstream segment
            // math (IsBetweenSegmentEndpoints) always has two real endpoints.
            foreach (var e in edges.Where(e => e.IsRay))
            {
                e.End = new Pt2(
                    e.Start.U + e.RayDirection.U * clipRadiusFt,
                    e.Start.V + e.RayDirection.V * clipRadiusFt);
                e.IsRay = false; // now treated as a bounded (clipped) segment
            }

            return edges;
        }

        /// <summary>
        /// For every adjacent group pair (from Delaunay adjacency), finds
        /// the real Voronoi edge segment if available, else the midpoint-
        /// perpendicular-bisector fallback (degenerate cases only).
        /// Pairs are returned already ordered shortest-center-distance-first,
        /// matching the confirmed processing order for claim resolution.
        /// </summary>
        public static List<(int A, int B, XYZ Start, XYZ End, bool UsedVoronoi)> BuildRidgeLinesForPairs(
            List<DrainGroup> groups, PlaneBasis basis, List<VoronoiEdge> voronoiEdges, double fallbackHalfLengthFt)
        {
            var adjacency = DelaunayTriangulation.ComputeAdjacency(
                groups.Select(g => basis.ToPlane(g.Center)).ToList());

            // Degenerate case: <3 groups or collinear centers → adjacency is empty
            // from ComputeAdjacency (ComputeTriangles returns nothing), but 2 groups
            // should still get exactly one ridge line (their own perpendicular
            // bisector) — handle that explicitly since Delaunay needs >=3 points.
            if (groups.Count == 2)
            {
                adjacency = new HashSet<(int, int)> { (0, 1) };
            }

            var voronoiByPair = new Dictionary<(int, int), VoronoiEdge>();
            foreach (var e in voronoiEdges)
            {
                var key = e.GroupAIndex < e.GroupBIndex ? (e.GroupAIndex, e.GroupBIndex) : (e.GroupBIndex, e.GroupAIndex);
                voronoiByPair[key] = e; // last-write is fine; at most one edge per Delaunay adjacency
            }

            var results = new List<(int A, int B, XYZ Start, XYZ End, bool UsedVoronoi)>();

            foreach (var (a, b) in adjacency)
            {
                if (voronoiByPair.TryGetValue((a, b), out var ve))
                {
                    results.Add((a, b, basis.ToWorld(ve.Start), basis.ToWorld(ve.End), true));
                }
                else
                {
                    // Fallback: perpendicular bisector of the two group centers,
                    // clipped to fallbackHalfLengthFt on each side of the midpoint.
                    XYZ ca = groups[a].Center, cb = groups[b].Center;
                    XYZ mid = (ca + cb) * 0.5;
                    XYZ dir = (cb - ca);
                    if (dir.GetLength() < 1e-6) continue;
                    dir = dir.Normalize();
                    // Perpendicular in-plane direction
                    XYZ perp = basis.Normal.CrossProduct(dir).Normalize();
                    XYZ start = mid - perp * fallbackHalfLengthFt;
                    XYZ end = mid + perp * fallbackHalfLengthFt;
                    results.Add((a, b, start, end, false));
                }
            }

            // Shortest group-center-distance first, per confirmed claim-priority rule.
            return results
                .OrderBy(r => groups[r.A].Center.DistanceTo(groups[r.B].Center))
                .ToList();
        }

        /// <summary>
        /// Finds junction points (3+ groups meeting at one location) as
        /// Delaunay triangle circumcenters where the triangle's 3 group
        /// vertices are mutually close to that circumcenter (i.e. a real
        /// N-way meeting, not just an arbitrary triangle in the mesh).
        /// Returns one candidate junction per Delaunay triangle.
        /// </summary>
        public static List<(List<int> GroupIndices, XYZ Point)> FindJunctions(
            List<DrainGroup> groups, PlaneBasis basis)
        {
            var junctions = new List<(List<int>, XYZ)>();
            if (groups.Count < 3) return junctions;

            var planePoints = groups.Select(g => basis.ToPlane(g.Center)).ToList();
            var triangles = DelaunayTriangulation.ComputeTriangles(planePoints);

            foreach (var t in triangles)
            {
                junctions.Add((new List<int> { t.A, t.B, t.C }, basis.ToWorld(t.Circumcenter)));
            }

            return junctions;
        }

        /// <summary>
        /// Finds every candidate roof vertex within toleranceFt of a single
        /// POINT (true radius test — not a line/segment test). Used for
        /// junction matching, where the "ridge line" degenerates to a single
        /// meeting point and a directional segment test would incorrectly
        /// produce an elongated, arbitrarily-oriented search area instead of
        /// a circular one. Same exclusion/classification rules as
        /// FindMatchingVertices.
        /// </summary>
        public static List<int> FindMatchingVerticesNearPoint(
            List<SlabShapeVertex> allVertices,
            XYZ point,
            double toleranceFt,
            HashSet<int> drainVertexIndices,
            HashSet<int> claimedVertexIndices,
            Func<int, int> classify,
            IReadOnlyCollection<int> ownGroupIndices)
        {
            var matches = new List<int>();

            for (int i = 0; i < allVertices.Count; i++)
            {
                if (drainVertexIndices.Contains(i)) continue;
                if (claimedVertexIndices.Contains(i)) continue;

                double dist = allVertices[i].Position.DistanceTo(point);
                if (dist > toleranceFt) continue;

                int classifiedGroup = classify(i);
                if (!ownGroupIndices.Contains(classifiedGroup)) continue; // group-mismatch, drop silently

                matches.Add(i);
            }

            return matches;
        }

        /// <summary>
        /// Finds every candidate roof vertex within toleranceFt of the
        /// segment [lineStart, lineEnd] AND whose projection lands
        /// strictly between the two endpoints (rejects matches near the
        /// extended infinite line beyond the real segment). Excludes
        /// drain vertices and any vertex already claimed
        /// (claimed.Contains). Each surviving candidate is further
        /// filtered by classify(vertexIndex) — must return one of the
        /// indices in ownGroupIndices, or it's dropped silently.
        /// </summary>
        public static List<int> FindMatchingVertices(
            List<SlabShapeVertex> allVertices,
            XYZ lineStart, XYZ lineEnd,
            double toleranceFt,
            HashSet<int> drainVertexIndices,
            HashSet<int> claimedVertexIndices,
            Func<int, int> classify,
            IReadOnlyCollection<int> ownGroupIndices)
        {
            var matches = new List<int>();
            XYZ dir = lineEnd - lineStart;
            double segLenSq = dir.DotProduct(dir);
            if (segLenSq < 1e-9) return matches;

            for (int i = 0; i < allVertices.Count; i++)
            {
                if (drainVertexIndices.Contains(i)) continue;
                if (claimedVertexIndices.Contains(i)) continue;

                XYZ p = allVertices[i].Position;
                XYZ toP = p - lineStart;
                double t = toP.DotProduct(dir) / segLenSq;

                // Strictly between endpoints — rejects extended-line matches.
                if (t < 0.0 || t > 1.0) continue;

                XYZ closest = lineStart + dir * t;
                double dist = p.DistanceTo(closest);
                if (dist > toleranceFt) continue;

                int classifiedGroup = classify(i);
                if (!ownGroupIndices.Contains(classifiedGroup)) continue; // group-mismatch, drop silently

                matches.Add(i);
            }

            return matches;
        }
    }
}
