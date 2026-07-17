// =======================================================
// File: RidgePointEngine.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.V021
// New in V021 — Ridge Point Detection (opt-in, on by default).
// UPDATED (2nd pass): the ridge line for each adjacent group pair is
// now the REAL VORONOI EDGE between the two groups' territories
// (dual of the Delaunay triangulation already used for adjacency) —
// see BuildVoronoiEdges/DelaunayTriangulation.ComputeVoronoiEdges.
// Any roof vertex within RidgeEdgeToleranceMm (repurposed field,
// still named RidgeCorridorWidthMm/CorridorWidthFt in code — default
// 100mm) of that real ridge line becomes a ridge point. Degenerate
// inputs (fewer than 3 groups, or collinear group centers) fall back
// to the earlier midpoint-perpendicular-line method.
//
// Responsibilities (per the confirmed spec / Q&A):
//   1. Cluster the flat DrainPoints cloud into drain groups by mutual
//      proximity, using the SAME tolerance value already used for
//      drain-tolerance expansion (no separate "group tolerance" input).
//   2. Build an adjacency graph over group centers via Delaunay
//      triangulation (a group can be adjacent to multiple neighbors).
//   3. Build the Voronoi diagram (dual of that same triangulation) —
//      each Delaunay adjacency maps to exactly one Voronoi edge.
//   4. For each adjacent pair (processed shortest-center-distance-first):
//        - ridge line = the real Voronoi edge segment for this pair
//          (or the midpoint-perpendicular fallback if unavailable)
//        - EVERY existing SlabShapeVertex within tolerance of that
//          ridge line counts — no cap on count
//        - no claim-exclusivity: a vertex may be matched by more than
//          one pair; the last pair processed wins (simple overwrite)
//        - if NO vertex is found near the ridge line, the pair is
//          skipped entirely (falls back to standard per-vertex Dijkstra)
//
// Ridge ELEVATION is computed by the caller (AutoSlopeEngine), which
// already has the multi-source Dijkstra distances to every group.
// This class only answers "where are the ridge points, and which two
// groups do they mediate" — no elevation math here.
// =======================================================

using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlopeByPoint.V021.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByPoint.V021.Core.Engine
{
    /// <summary>A cluster of drain points that are mutually within the clustering tolerance.</summary>
    public class DrainGroup
    {
        public int GroupIndex;
        public List<XYZ> Points = new();
        public XYZ Center;

        /// <summary>Vertex indices (into the roof's SlabShapeVertex list) matching this group's points.</summary>
        public List<int> VertexIndices = new();
    }

    /// <summary>An adjacency edge between two drain groups, per Delaunay triangulation of group centers.</summary>
    public class GroupAdjacency
    {
        public int GroupAIndex;
        public int GroupBIndex;
        public double CenterDistanceFt;
    }

    public static class RidgePointEngine
    {
        /// <summary>
        /// Clusters drain points into groups using single-linkage proximity:
        /// two points are in the same group if within clusterToleranceFt of
        /// ANY other point already in that group (transitive union).
        /// </summary>
        public static List<DrainGroup> ClusterDrainPoints(
            List<XYZ> drainPoints, double clusterToleranceFt)
        {
            var groups = new List<DrainGroup>();
            if (drainPoints == null || drainPoints.Count == 0) return groups;

            int n = drainPoints.Count;
            var parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;

            int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
            void Union(int a, int b) { int ra = Find(a), rb = Find(b); if (ra != rb) parent[ra] = rb; }

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    if (drainPoints[i].DistanceTo(drainPoints[j]) <= clusterToleranceFt)
                        Union(i, j);
                }
            }

            var byRoot = new Dictionary<int, DrainGroup>();
            for (int i = 0; i < n; i++)
            {
                int root = Find(i);
                if (!byRoot.TryGetValue(root, out var grp))
                {
                    grp = new DrainGroup { GroupIndex = byRoot.Count };
                    byRoot[root] = grp;
                    groups.Add(grp);
                }
                grp.Points.Add(drainPoints[i]);
            }

            // Re-index sequentially (0..count-1) since dictionary insertion order
            // already matches first-seen order, but GroupIndex was set at creation
            // time using byRoot.Count which is safe/unique — no re-sort needed.
            foreach (var grp in groups)
            {
                double cx = grp.Points.Average(p => p.X);
                double cy = grp.Points.Average(p => p.Y);
                double cz = grp.Points.Average(p => p.Z);
                grp.Center = new XYZ(cx, cy, cz);
            }

            return groups;
        }

        /// <summary>
        /// Matches each group's points to roof vertex indices (within drainMatchToleranceFt),
        /// populating DrainGroup.VertexIndices. Mirrors the matching tolerance used elsewhere
        /// in AutoSlopeEngine for consistency.
        /// </summary>
        public static void MatchGroupVertices(
            List<DrainGroup> groups, List<SlabShapeVertex> vertices, double drainMatchToleranceFt)
        {
            foreach (var grp in groups)
            {
                grp.VertexIndices.Clear();
                foreach (XYZ pt in grp.Points)
                {
                    for (int i = 0; i < vertices.Count; i++)
                    {
                        if (vertices[i].Position.DistanceTo(pt) <= drainMatchToleranceFt)
                        {
                            if (!grp.VertexIndices.Contains(i))
                                grp.VertexIndices.Add(i);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Builds the Delaunay triangulation of group centers (projected to the roof's
        /// working plane via topFace's UV) and returns the resulting edges as group
        /// adjacencies. Falls back to a full nearest-neighbor mesh if fewer than 3
        /// groups exist (Delaunay needs >=3 points to form triangles) — with 2 groups
        /// the only possible adjacency is trivially between the two, and with 1 group
        /// there are none.
        /// </summary>
        public static List<GroupAdjacency> BuildDelaunayAdjacency(List<DrainGroup> groups, Face topFace)
        {
            var result = new List<GroupAdjacency>();
            if (groups == null || groups.Count < 2) return result;

            if (groups.Count == 2)
            {
                result.Add(new GroupAdjacency
                {
                    GroupAIndex = groups[0].GroupIndex,
                    GroupBIndex = groups[1].GroupIndex,
                    CenterDistanceFt = groups[0].Center.DistanceTo(groups[1].Center)
                });
                return result;
            }

            List<UV> uvPoints = ProjectGroupCentersToUV(groups, topFace);
            var edges = DelaunayTriangulation.ComputeEdges(uvPoints);

            foreach (var (ia, ib) in edges)
            {
                result.Add(new GroupAdjacency
                {
                    GroupAIndex = groups[ia].GroupIndex,
                    GroupBIndex = groups[ib].GroupIndex,
                    CenterDistanceFt = groups[ia].Center.DistanceTo(groups[ib].Center)
                });
            }

            return result;
        }

        /// <summary>
        /// Builds the Voronoi diagram of group centers (dual of the Delaunay
        /// triangulation) and returns each edge as a real-space (XYZ) segment or
        /// ray, keyed by the SAME (GroupAIndex, GroupBIndex) pairing BuildDelaunayAdjacency
        /// produces — so callers can look up "the ridge line for this adjacency"
        /// directly. Rays (hull edges, unbounded) are already clipped here to the
        /// roof's bounding box, per confirmed spec, so callers always get a finite
        /// segment back.
        /// SPECIAL CASE — exactly 2 groups: a full triangulation isn't meaningful
        /// (Bowyer-Watson needs >=3 real points to form a triangle), but the
        /// Voronoi edge between exactly 2 sites is still perfectly well-defined:
        /// it's the perpendicular bisector of the segment joining them — an
        /// infinite line, which we construct directly and clip to the roof's
        /// bounding box, same as any hull ray. This IS the real Voronoi edge for
        /// 2 groups (confirmed: use Voronoi even for 2 groups, since each group
        /// can itself contain multiple drain points).
        /// Returns an empty dictionary only for truly degenerate inputs (fewer
        /// than 2 groups, or coincident/collinear-with-zero-separation centers) —
        /// caller falls back to the midpoint-perpendicular method for those.
        /// </summary>
        public static Dictionary<(int, int), (XYZ Start, XYZ End)> BuildVoronoiEdges(
            List<DrainGroup> groups, Face topFace, BoundingBoxXYZ roofBoundingBox)
        {
            var result = new Dictionary<(int, int), (XYZ, XYZ)>();
            if (groups == null || groups.Count < 2) return result;

            if (groups.Count == 2)
            {
                XYZ a = groups[0].Center, b = groups[1].Center;
                XYZ along = b - a;
                if (along.GetLength() < 0.001) return result; // coincident centers — nothing sensible to do
                along = along.Normalize();
                XYZ mid = (a + b) * 0.5;

                XYZ normal;
                try
                {
                    IntersectionResult proj = topFace.Project(mid);
                    normal = proj != null ? topFace.ComputeNormal(proj.UVPoint) : XYZ.BasisZ;
                }
                catch { normal = XYZ.BasisZ; }

                XYZ perp = along.CrossProduct(normal);
                if (perp.GetLength() < 0.001) perp = along.CrossProduct(XYZ.BasisZ);
                perp = perp.Normalize();

                double bboxDiagonalFt = roofBoundingBox != null
                    ? roofBoundingBox.Min.DistanceTo(roofBoundingBox.Max)
                    : 1000.0;

                int keyA = groups[0].GroupIndex, keyB = groups[1].GroupIndex;
                var pairKey = keyA < keyB ? (keyA, keyB) : (keyB, keyA);
                result[pairKey] = (mid - perp * bboxDiagonalFt, mid + perp * bboxDiagonalFt);
                return result;
            }

            PlaneBasis basis = BuildPlaneBasis(groups, topFace);
            List<UV> uvPoints = ProjectGroupCentersToPlane(groups, basis);
            List<VoronoiEdge> voronoiEdges = DelaunayTriangulation.ComputeVoronoiEdges(uvPoints);

            foreach (var ve in voronoiEdges)
            {
                int groupAIdx = groups[ve.PointAIndex].GroupIndex;
                int groupBIdx = groups[ve.PointBIndex].GroupIndex;
                var key = groupAIdx < groupBIdx ? (groupAIdx, groupBIdx) : (groupBIdx, groupAIdx);

                XYZ startXyz = basis.ToWorld(ve.Start);
                XYZ endXyz;

                if (!ve.IsRay)
                {
                    endXyz = basis.ToWorld(ve.End);
                }
                else
                {
                    // Clip the ray to the roof's bounding box (confirmed spec) by
                    // extending it far enough to guarantee it exits the box, then
                    // relying on the segment-distance check downstream (which only
                    // cares about the finite portion actually near any vertex —
                    // an overlong segment doesn't change which vertices match,
                    // it just guarantees the segment fully spans the roof).
                    double bboxDiagonalFt = roofBoundingBox != null
                        ? roofBoundingBox.Min.DistanceTo(roofBoundingBox.Max)
                        : 1000.0; // generous fallback if bbox unavailable
                    XYZ rayDirXyz = basis.ToWorld(
                        new UV(ve.Start.U + ve.RayDirection.U, ve.Start.V + ve.RayDirection.V)) - startXyz;
                    if (rayDirXyz.GetLength() < 1e-9) continue;
                    rayDirXyz = rayDirXyz.Normalize();
                    endXyz = startXyz + rayDirXyz * (bboxDiagonalFt * 2.0);
                }

                result[key] = (startXyz, endXyz);
            }

            return result;
        }

        /// <summary>
        /// Builds Voronoi JUNCTION points — one per Delaunay triangle, at its
        /// circumcenter, where exactly 3 group "territories" meet (a true
        /// Voronoi vertex, not an edge). Requires >=3 groups AND a non-
        /// collinear layout (a real triangulation); returns an empty list
        /// otherwise (2-group or collinear layouts have no 3-way junctions —
        /// everything is edge-based in that case, which BuildVoronoiEdges
        /// already covers). Junctions are returned pre-sorted smallest-
        /// circumradius-first (confirmed default processing order).
        /// </summary>
        public static List<(List<int> GroupIndices, XYZ JunctionPoint, double CircumRadiusFt)> BuildVoronoiJunctions(
            List<DrainGroup> groups, Face topFace)
        {
            var result = new List<(List<int>, XYZ, double)>();
            if (groups == null || groups.Count < 3) return result;

            PlaneBasis basis = BuildPlaneBasis(groups, topFace);
            List<UV> uvPoints = ProjectGroupCentersToPlane(groups, basis);
            if (ArePointsCollinearPublicCheck(uvPoints)) return result;

            List<DelaunayTriangleInfo> triangles = DelaunayTriangulation.ComputeTriangles(uvPoints);

            foreach (var t in triangles)
            {
                var groupIndices = new List<int>
                {
                    groups[t.A].GroupIndex,
                    groups[t.B].GroupIndex,
                    groups[t.C].GroupIndex
                };
                XYZ junctionXyz = basis.ToWorld(t.Circumcenter);
                result.Add((groupIndices, junctionXyz, t.CircumRadius));
            }

            return result.OrderBy(r => r.Item3).ToList();
        }

        /// <summary>
        /// For one Voronoi junction (3+ groups meeting at a point), finds every
        /// roof vertex within toleranceFt of junctionPoint. A vertex may also be
        /// matched by another pair/junction elsewhere — no exclusivity is
        /// enforced here; the caller's processing order decides which
        /// assignment wins (last-processed wins).
        /// </summary>
        public static RidgeJunctionResult FindJunctionRidgePoints(
            int junctionIndex,
            List<int> groupIndices,
            XYZ junctionPoint,
            double circumRadiusFt,
            List<SlabShapeVertex> vertices,
            Face topFace,
            double toleranceFt)
        {
            var result = new RidgeJunctionResult
            {
                JunctionIndex = junctionIndex,
                GroupIndices = groupIndices,
                JunctionPoint = junctionPoint,
                CircumRadiusFt = circumRadiusFt,
                ToleranceFt = toleranceFt
            };

            BoundingBoxXYZ bbox = SafeGetBoundingBox(topFace);
            var candidates = new List<(int index, double dist)>();

            for (int i = 0; i < vertices.Count; i++)
            {
                XYZ p = vertices[i].Position;
                if (bbox != null && !IsWithinBoundingBox(p, bbox)) continue;

                double dist = p.DistanceTo(junctionPoint);
                if (dist <= toleranceFt)
                    candidates.Add((i, dist));
            }

            foreach (var (idx, _) in candidates.OrderBy(c => c.dist))
            {
                result.MatchedVertexIndices.Add(idx);
            }

            return result;
        }

        /// <summary>
        /// Lazily-built roof-vertex Delaunay triangulation + per-vertex drain-
        /// group classification, used ONLY as a fallback when the normal
        /// tolerance-based search finds zero matches for a pair or junction
        /// (confirmed: built lazily on first actual need, not built upfront).
        /// Reuses DelaunayTriangulation.ComputeEdges over ALL roof vertices
        /// (not drain-group centers) — the same code path, different point set.
        /// </summary>
        public class RoofVertexTopology
        {
            private readonly List<SlabShapeVertex> _vertices;
            private readonly Face _topFace;
            private Dictionary<int, List<int>> _neighborMap; // vertex index -> Delaunay-neighbor vertex indices
            private bool _built;

            public RoofVertexTopology(List<SlabShapeVertex> vertices, Face topFace)
            {
                _vertices = vertices;
                _topFace = topFace;
            }

            private void EnsureBuilt()
            {
                if (_built) return;
                _built = true;

                var uvPoints = new List<UV>(_vertices.Count);
                foreach (var v in _vertices)
                {
                    IntersectionResult proj = _topFace.Project(v.Position);
                    uvPoints.Add(proj != null ? proj.UVPoint : new UV(v.Position.X, v.Position.Y));
                }

                _neighborMap = new Dictionary<int, List<int>>();
                for (int i = 0; i < _vertices.Count; i++) _neighborMap[i] = new List<int>();

                List<(int, int)> edges = DelaunayTriangulation.ComputeEdges(uvPoints);
                foreach (var (i1, i2) in edges)
                {
                    _neighborMap[i1].Add(i2);
                    _neighborMap[i2].Add(i1);
                }
            }

            /// <summary>
            /// Finds every roof vertex that "borders" a mix of at least two
            /// different drain groups among the given groupIndices — i.e. its
            /// Delaunay neighbors include at least one vertex classified into
            /// each of at least two DIFFERENT groups from the set (confirmed
            /// "mix" rule, not a stricter single-A-single-B-edge rule). No
            /// exclusivity — a vertex may also be matched elsewhere.
            /// classify(vertexIndex) must return the group index a vertex is
            /// nearest to (by Dijkstra distance — confirmed classification
            /// method), or -1 if unclassifiable.
            /// </summary>
            public List<int> FindBoundaryVertices(
                List<int> groupIndices, Func<int, int> classify)
            {
                EnsureBuilt();

                var groupSet = new HashSet<int>(groupIndices);
                var matches = new List<int>();

                for (int i = 0; i < _vertices.Count; i++)
                {
                    var neighborGroups = new HashSet<int>();
                    foreach (int nIdx in _neighborMap[i])
                    {
                        int g = classify(nIdx);
                        if (g >= 0 && groupSet.Contains(g))
                            neighborGroups.Add(g);
                    }

                    // "Mix" rule (confirmed): neighbors must include at least 2
                    // DIFFERENT groups from the set — a single-group neighborhood
                    // doesn't count, even if that group is in groupIndices.
                    if (neighborGroups.Count >= 2)
                        matches.Add(i);
                }

                return matches;
            }
        }

        /// <summary>Local re-check of collinearity for UV points, mirroring DelaunayTriangulation's private ArePointsCollinear (duplicated here rather than exposing that private method — cheap O(n) check).</summary>
        private static bool ArePointsCollinearPublicCheck(List<UV> points)
        {
            if (points.Count < 3) return true;
            UV p0 = points[0];
            UV dir = new UV(0, 0);
            for (int i = 1; i < points.Count; i++)
            {
                double du = points[i].U - p0.U, dv = points[i].V - p0.V;
                if (Math.Abs(du) > 1e-9 || Math.Abs(dv) > 1e-9) { dir = new UV(du, dv); break; }
            }
            if (dir.U == 0 && dir.V == 0) return true;

            foreach (var p in points)
            {
                double cross = (p.U - p0.U) * dir.V - (p.V - p0.V) * dir.U;
                if (Math.Abs(cross) > 1e-6) return false;
            }
            return true;
        }

        /// <summary>
        /// BUG FIX: Face.Project / Face.Evaluate round-trip through a face's
        /// NATIVE parametric UV space, which for a PlanarFace is an arbitrary
        /// (often non-orthonormal, non-unit-scale) basis — it preserves
        /// topology but NOT real-world distances or angles. Delaunay/Voronoi
        /// math (circumcenters, perpendicular bisectors, ray directions) is
        /// metric — it assumes the 2D coordinates it's given form a genuine
        /// Euclidean plane. Running that math on raw native UV and mapping
        /// results back via Face.Evaluate silently distorted every Voronoi
        /// edge/junction position, which is why the tolerance-based match
        /// against real roof vertices was failing on every single pair
        /// (100% fallback to the loose roof-vertex boundary heuristic).
        /// Fix: build our own orthonormal (origin, X, Y, normal) basis on
        /// the face's plane and project points into/out of THAT — true
        /// feet, angle- and distance-preserving — instead of native UV.
        /// </summary>
        private readonly struct PlaneBasis
        {
            public readonly XYZ Origin;
            public readonly XYZ AxisX;
            public readonly XYZ AxisY;
            public readonly XYZ Normal;

            public PlaneBasis(XYZ origin, XYZ axisX, XYZ axisY, XYZ normal)
            {
                Origin = origin; AxisX = axisX; AxisY = axisY; Normal = normal;
            }

            public UV ToPlane(XYZ p)
            {
                XYZ d = p - Origin;
                return new UV(d.DotProduct(AxisX), d.DotProduct(AxisY));
            }

            public XYZ ToWorld(UV uv) => Origin + AxisX * uv.U + AxisY * uv.V;
        }

        /// <summary>
        /// Builds an orthonormal basis on the face's plane, anchored at the
        /// first group's center and oriented from the face normal (via
        /// Project/ComputeNormal at that point — normal lookup only, not
        /// used for distances, so native UV's scale/skew doesn't matter here).
        /// </summary>
        private static PlaneBasis BuildPlaneBasis(List<DrainGroup> groups, Face topFace)
        {
            XYZ origin = groups[0].Center;

            XYZ normal;
            try
            {
                IntersectionResult proj = topFace.Project(origin);
                normal = proj != null ? topFace.ComputeNormal(proj.UVPoint) : XYZ.BasisZ;
            }
            catch { normal = XYZ.BasisZ; }

            if (normal.GetLength() < 1e-9) normal = XYZ.BasisZ;
            normal = normal.Normalize();

            // Any vector not parallel to normal gives us a starting axis;
            // Gram-Schmidt it against normal, then cross for the second axis.
            XYZ seed = Math.Abs(normal.DotProduct(XYZ.BasisX)) < 0.9 ? XYZ.BasisX : XYZ.BasisY;
            XYZ axisX = (seed - normal * seed.DotProduct(normal));
            if (axisX.GetLength() < 1e-9) axisX = (XYZ.BasisY - normal * XYZ.BasisY.DotProduct(normal));
            axisX = axisX.Normalize();
            XYZ axisY = normal.CrossProduct(axisX).Normalize();

            return new PlaneBasis(origin, axisX, axisY, normal);
        }

        private static List<UV> ProjectGroupCentersToPlane(List<DrainGroup> groups, PlaneBasis basis)
        {
            var pts = new List<UV>(groups.Count);
            foreach (var grp in groups) pts.Add(basis.ToPlane(grp.Center));
            return pts;
        }

        private static List<UV> ProjectGroupCentersToUV(List<DrainGroup> groups, Face topFace)
        {
            var uvPoints = new List<UV>(groups.Count);
            foreach (var grp in groups)
            {
                IntersectionResult proj = topFace.Project(grp.Center);
                uvPoints.Add(proj != null ? proj.UVPoint : new UV(grp.Center.X, grp.Center.Y));
            }
            return uvPoints;
        }

        /// <summary>
        /// For one adjacent group pair, finds EVERY roof vertex within
        /// edgeToleranceFt of the pair's actual RIDGE LINE — which is the real
        /// Voronoi edge segment between the two groups' territories when one is
        /// available (voronoiSegment provided), or the midpoint-perpendicular
        /// line as a fallback for degenerate cases (fewer than 3 groups total,
        /// or collinear group centers — see BuildVoronoiEdges). No
        /// exclusivity — a vertex may also be matched by another pair or
        /// junction; callers process pairs shortest-distance-first, so a
        /// later assignment to the same vertex simply overwrites the earlier
        /// one (last-write-wins).
        /// </summary>
        public static RidgePairResult FindRidgePoints(
            int pairIndex,
            DrainGroup groupA,
            DrainGroup groupB,
            List<SlabShapeVertex> vertices,
            Face topFace,
            double edgeToleranceFt,
            (XYZ Start, XYZ End)? voronoiSegment)
        {
            var result = new RidgePairResult
            {
                PairIndex = pairIndex,
                GroupAIndex = groupA.GroupIndex,
                GroupBIndex = groupB.GroupIndex,
                GroupACenter = groupA.Center,
                GroupBCenter = groupB.Center,
                CenterDistanceFt = groupA.Center.DistanceTo(groupB.Center),
                CorridorWidthFt = edgeToleranceFt
            };

            XYZ segStart, segEnd;

            if (voronoiSegment.HasValue)
            {
                // Real Voronoi edge — the actual boundary between the two groups'
                // territories, not just a straight line through the midpoint.
                segStart = voronoiSegment.Value.Start;
                segEnd = voronoiSegment.Value.End;
                result.RidgeLineStart = segStart;
                result.RidgeLineEnd = segEnd;
                result.UsedVoronoiEdge = true;
            }
            else
            {
                // Fallback: old midpoint-perpendicular method, for degenerate
                // inputs where a Voronoi diagram isn't well-defined (fewer than
                // 3 groups total, or collinear group centers).
                XYZ a = groupA.Center;
                XYZ b = groupB.Center;
                XYZ mid = (a + b) * 0.5;

                XYZ along = (b - a);
                if (along.GetLength() < 0.001) return result; // coincident centers — nothing sensible to do
                along = along.Normalize();

                XYZ normal;
                try
                {
                    IntersectionResult proj = topFace.Project(mid);
                    normal = proj != null ? topFace.ComputeNormal(proj.UVPoint) : XYZ.BasisZ;
                }
                catch { normal = XYZ.BasisZ; }

                XYZ perp = along.CrossProduct(normal);
                if (perp.GetLength() < 0.001) perp = along.CrossProduct(XYZ.BasisZ);
                perp = perp.Normalize();

                BoundingBoxXYZ fallbackBbox = SafeGetBoundingBox(topFace);
                double halfSpan = fallbackBbox != null
                    ? fallbackBbox.Min.DistanceTo(fallbackBbox.Max)
                    : 1000.0;

                segStart = mid - perp * halfSpan;
                segEnd = mid + perp * halfSpan;
                result.RidgeLineStart = segStart;
                result.RidgeLineEnd = segEnd;
                result.UsedVoronoiEdge = false;
            }

            BoundingBoxXYZ bbox = SafeGetBoundingBox(topFace);

            List<int> matches = FindAllNearSegment(segStart, segEnd, vertices, bbox, edgeToleranceFt);

            foreach (int idx in matches)
            {
                result.MatchedVertexIndices.Add(idx);
            }

            return result;
        }

        /// <summary>
        /// Returns every vertex index whose distance to the finite segment
        /// [segStart, segEnd] is within toleranceFt, bounded by the roof's
        /// bounding box. No exclusivity — a vertex may also be matched by
        /// another pair elsewhere. Distance-to-SEGMENT (not
        /// infinite line) — a vertex must be near the actual ridge line's extent,
        /// not just near its infinite extension, matching the "on or near a
        /// Voronoi edge" spec. Results are ordered by distance (closest first)
        /// purely for readability in logs/exports.
        /// </summary>
        private static List<int> FindAllNearSegment(
            XYZ segStart, XYZ segEnd, List<SlabShapeVertex> vertices,
            BoundingBoxXYZ bbox, double toleranceFt)
        {
            var candidates = new List<(int index, double dist)>();

            XYZ segVec = segEnd - segStart;
            double segLenSq = segVec.DotProduct(segVec);

            for (int i = 0; i < vertices.Count; i++)
            {
                XYZ p = vertices[i].Position;
                if (bbox != null && !IsWithinBoundingBox(p, bbox)) continue;

                double dist = DistancePointToSegment(p, segStart, segEnd, segVec, segLenSq);

                if (dist <= toleranceFt)
                    candidates.Add((i, dist));
            }

            return candidates
                .OrderBy(c => c.dist)
                .Select(c => c.index)
                .ToList();
        }

        /// <summary>Shortest distance from point p to the finite segment [a, a+segVec].</summary>
        private static double DistancePointToSegment(XYZ p, XYZ a, XYZ b, XYZ segVec, double segLenSq)
        {
            if (segLenSq < 1e-12) return p.DistanceTo(a); // degenerate zero-length segment

            double t = (p - a).DotProduct(segVec) / segLenSq;
            t = Math.Max(0.0, Math.Min(1.0, t)); // clamp to the segment, not the infinite line

            XYZ closest = a + segVec * t;
            return p.DistanceTo(closest);
        }

        private static bool IsWithinBoundingBox(XYZ p, BoundingBoxXYZ bbox)
        {
            const double padFt = 0.05; // ~15mm slack for boundary-riding points
            return p.X >= bbox.Min.X - padFt && p.X <= bbox.Max.X + padFt &&
                   p.Y >= bbox.Min.Y - padFt && p.Y <= bbox.Max.Y + padFt &&
                   p.Z >= bbox.Min.Z - padFt && p.Z <= bbox.Max.Z + padFt;
        }

        /// <summary>
        /// Public wrapper so callers (e.g. AutoSlopeEngine, when calling
        /// BuildVoronoiEdges) can get the same roof bounding box used
        /// internally for vertex search bounds and ray-clipping, without
        /// duplicating the bounding-box derivation logic.
        /// </summary>
        public static BoundingBoxXYZ GetRoofBoundingBox(Face topFace) => SafeGetBoundingBox(topFace);

        private static BoundingBoxXYZ SafeGetBoundingBox(Face face)
        {
            try
            {
                BoundingBoxUV bb = face.GetBoundingBox();
                if (bb == null) return null;

                // Sample the 4 UV corners to build a real-space XYZ bounding box —
                // cheaper and more robust than trying to derive one analytically.
                var corners = new[]
                {
                    face.Evaluate(new UV(bb.Min.U, bb.Min.V)),
                    face.Evaluate(new UV(bb.Min.U, bb.Max.V)),
                    face.Evaluate(new UV(bb.Max.U, bb.Min.V)),
                    face.Evaluate(new UV(bb.Max.U, bb.Max.V)),
                };

                double minX = corners.Min(c => c.X), maxX = corners.Max(c => c.X);
                double minY = corners.Min(c => c.Y), maxY = corners.Max(c => c.Y);
                double minZ = corners.Min(c => c.Z), maxZ = corners.Max(c => c.Z);

                var box = new BoundingBoxXYZ
                {
                    Min = new XYZ(minX, minY, minZ),
                    Max = new XYZ(maxX, maxY, maxZ)
                };
                return box;
            }
            catch
            {
                return null;
            }
        }
    }
}
