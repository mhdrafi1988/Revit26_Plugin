using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByPoint.V012.Core.Engine
{
    /// <summary>
    /// Visibility-graph point-to-drain path length calculator.
    ///
    /// Nodes: every roof vertex (original indices 0.._verts.Count-1, drains
    /// included), plus synthetic nodes added while building the graph:
    ///   - TANGENT points: for a vertex with no clear line to a nearby arc,
    ///     the exact tangent point(s) from that vertex to the arc's circle
    ///     (closed-form: d = dist(vertex,center), L = sqrt(d²-r²), tangent
    ///     angle offset = acos(r/d) from the center→vertex direction).
    ///   - BITANGENT points: for each pair of arcs, the external tangent line
    ///     connecting them (so a path can go from hugging one arc straight to
    ///     hugging the next, for paths that cross two obstacles in sequence).
    ///   - If a computed tangent/bitangent parameter falls outside the arc's
    ///     actual bounded segment, it's clamped to the nearest arc endpoint
    ///     (mandatory waypoint) instead of being discarded.
    ///
    /// Edges: a straight line between any two nodes (validated: stays on the
    /// roof face, doesn't cut across another arc mid-span), OR the true arc
    /// length between two nodes known to lie on the same arc.
    ///
    /// Dijkstra runs once over this whole graph (multi-source, from all
    /// drains). This supersedes the previous ray-cast/arc-walk + graph-fallback
    /// hybrid — visibility graphs are the standard approach for shortest paths
    /// around circular obstacles, and generalize cleanly to multiple arcs in
    /// sequence via bitangents.
    /// </summary>
    public class DijkstraPathEngine
    {
        private readonly List<SlabShapeVertex> _verts;
        private readonly Face _topFace;
        private readonly double _edgeThresholdFt;
        private readonly List<Arc> _arcs;
        private readonly double _curveTolFt;
        private const double PROJ_TOL = 0.00328084;

        private readonly int _baseCount; // number of original roof vertices

        // Full node list: [0.._baseCount-1] = original vertices, rest = synthetic
        // (tangent / bitangent / snapped-endpoint) nodes added while building the graph.
        private readonly List<XYZ> _nodes = new();
        private readonly Dictionary<int, VertexCurveInfo> _nodeCurve = new(); // node index -> arc it lies on (if any)
        private readonly Dictionary<int, List<int>> _adj = new();
        private readonly Dictionary<(int, int), double> _edgeWeight = new();

        // Per-arc convexity classification: true = obstacle (opening / true
        // exterior bulge) that paths must route AROUND via tangent/bitangent
        // lines; false = concave boundary arc where the chord-to-arc sliver
        // is real roof face, so straight chords across it are valid and
        // obstacle-avoidance routing is unnecessary. See ClassifyArcs().
        private readonly List<(Arc arc, bool isObstacle)> _arcClassification = new();

        /// <summary>
        /// Verbose graph-build trace: arc classification, tangent-point counts,
        /// and the specific reason every rejected candidate edge failed.
        /// Populated during construction; read after building to diagnose why
        /// a vertex ended up on a long detour instead of a short local route.
        /// </summary>
        public List<string> Diagnostics { get; } = new();

        private double[] _fullDist;
        private int[] _fullPred;

        /// <summary>One leg of a solved path — either a straight line or an arc segment.</summary>
        public struct PathHop
        {
            public string Description;
            public double LengthFt;
            public bool IsArcHop;

            public PathHop(string description, double lengthFt, bool isArcHop)
            {
                Description = description;
                LengthFt = lengthFt;
                IsArcHop = isArcHop;
            }
        }

        /// <param name="vertices">All roof shape vertices (including any inserted curve-intersection points).</param>
        /// <param name="topFace">Roof top face.</param>
        /// <param name="edgeThresholdFt">Max candidate edge length (perf/practicality cap on straight edges).</param>
        /// <param name="arcs">Boundary/opening arcs (outer + inner loops). Pass null/empty to disable arc handling entirely.</param>
        /// <param name="curveTolFt">Tolerance for treating a point as "on" an arc / two nodes as the same point.</param>
        public DijkstraPathEngine(
            List<SlabShapeVertex> vertices,
            Face topFace,
            double edgeThresholdFt,
            List<Arc> arcs = null,
            double curveTolFt = 0.0033) // ~1mm default
        {
            _verts = vertices;
            _topFace = topFace;
            _edgeThresholdFt = edgeThresholdFt;
            _arcs = arcs ?? new List<Arc>();
            _curveTolFt = curveTolFt;
            _baseCount = vertices.Count;

            foreach (var v in vertices) _nodes.Add(v.Position);
            for (int i = 0; i < _baseCount; i++) _adj[i] = new List<int>();

            BuildVisibilityGraph();
        }

        // ────────────────────────────────────────────────────────────────
        // Public API (unchanged shape from the graph/Dijkstra version)
        // ────────────────────────────────────────────────────────────────

        public double[] ComputeAllDistances(HashSet<int> drains)
        {
            int total = _nodes.Count;
            var dist = new double[total];
            var pred = new int[total];
            for (int i = 0; i < total; i++) { dist[i] = double.PositiveInfinity; pred[i] = -1; }

            var pq = new SortedSet<(double, int)>(
                Comparer<(double, int)>.Create((a, b) =>
                {
                    int cmp = a.Item1.CompareTo(b.Item1);
                    return cmp != 0 ? cmp : a.Item2.CompareTo(b.Item2);
                }));

            foreach (int drain in drains)
            {
                dist[drain] = 0;
                pq.Add((0, drain));
            }

            while (pq.Count > 0)
            {
                var (d, v) = pq.Min;
                pq.Remove(pq.Min);
                if (d > dist[v]) continue;

                if (!_adj.TryGetValue(v, out var neighbors)) continue;
                foreach (int nb in neighbors)
                {
                    double nd = d + Weight(v, nb);
                    if (nd < dist[nb])
                    {
                        dist[nb] = nd;
                        pred[nb] = v;
                        pq.Add((nd, nb));
                    }
                }
            }

            _fullDist = dist;
            _fullPred = pred;

            // Only the original vertices are meaningful to the caller.
            var result = new double[_baseCount];
            Array.Copy(dist, result, _baseCount);
            return result;
        }

        /// <summary>Hop-by-hop breakdown of the winning path for a vertex, from the last ComputeAllDistances() call.</summary>
        public List<PathHop> GetPathFrom(int vertexIndex)
        {
            var hops = new List<PathHop>();
            if (_fullPred == null) return hops;

            int cur = vertexIndex;
            int guard = _nodes.Count + 5;
            while (_fullPred[cur] != -1 && guard-- > 0)
            {
                int prev = _fullPred[cur];
                double len = Weight(prev, cur);
                bool isArc = _nodeCurve.TryGetValue(prev, out var cp) &&
                             _nodeCurve.TryGetValue(cur, out var cc) &&
                             ReferenceEquals(cp.Curve, cc.Curve);

                string desc = isArc
                    ? $"arc {NodeLabel(prev)} → {NodeLabel(cur)}"
                    : $"line {NodeLabel(prev)} → {NodeLabel(cur)}";

                hops.Add(new PathHop(desc, len, isArc));
                cur = prev;
            }
            hops.Reverse();
            return hops;
        }

        /// <summary>True if the vertex ended up unreachable in the last ComputeAllDistances() call.</summary>
        public string GetSkipReason(int vertexIndex)
        {
            if (_fullDist == null) return "not computed";
            return double.IsInfinity(_fullDist[vertexIndex])
                ? "no valid path found through the visibility graph (isolated from every drain)"
                : string.Empty;
        }

        private string NodeLabel(int nodeIndex) =>
            nodeIndex < _baseCount ? $"v{nodeIndex}" : $"pt{nodeIndex}";

        private double Weight(int i, int j) =>
            _edgeWeight.TryGetValue((i, j), out double w) ? w : _nodes[i].DistanceTo(_nodes[j]);

        // ────────────────────────────────────────────────────────────────
        // Graph construction
        // ────────────────────────────────────────────────────────────────

        private void BuildVisibilityGraph()
        {
            // Map original vertices that already sit on an arc (e.g. inserted
            // curve-intersection points) so same-arc pairs use arc length.
            var positions = new List<XYZ>(_baseCount);
            for (int i = 0; i < _baseCount; i++) positions.Add(_nodes[i]);
            var baseCurveMap = CurveIntersectionHelper.MapVerticesOnCurves(positions, _arcs, _curveTolFt);
            foreach (var kv in baseCurveMap) _nodeCurve[kv.Key] = kv.Value;

            ClassifyArcs();

            // ── Step A: tangent points from every original vertex to every
            //    OBSTACLE arc it doesn't already sit on. Concave boundary
            //    arcs are skipped — no obstacle to route around.
            for (int i = 0; i < _baseCount; i++)
            {
                if (_nodeCurve.ContainsKey(i)) continue; // already on an arc — no tangent needed from itself
                XYZ from = _nodes[i];

                foreach (Arc arc in _arcs)
                {
                    if (!IsObstacleArc(arc)) continue; // concave boundary — no routing needed

                    int arcIdx = ArcIndexOf(arc);
                    var tangents = ComputeTangentPoints(from, arc);
                    Diagnostics.Add($"{NodeLabel(i)} -> arc{arcIdx}: {tangents.Count} tangent point(s) computed");

                    foreach (var (param, point) in tangents)
                    {
                        int node = GetOrAddNode(point, arc, param);
                        TryAddStraightEdge(i, node);
                    }
                }
            }

            // ── Step B: bitangents between every pair of OBSTACLE arcs, so a
            //    path can go directly from hugging one arc to hugging the
            //    next. Skipped if either arc is a concave boundary arc.
            for (int a1 = 0; a1 < _arcs.Count; a1++)
            {
                for (int a2 = a1 + 1; a2 < _arcs.Count; a2++)
                {
                    if (!IsObstacleArc(_arcs[a1]) || !IsObstacleArc(_arcs[a2])) continue;

                    foreach (var (p1, param1, p2, param2) in ComputeExternalBitangents(_arcs[a1], _arcs[a2]))
                    {
                        int n1 = GetOrAddNode(p1, _arcs[a1], param1);
                        int n2 = GetOrAddNode(p2, _arcs[a2], param2);
                        TryAddStraightEdge(n1, n2);
                    }
                }
            }

            // ── Step C: same-arc pairs use arc length (mirrors the earlier
            //    arc-edge-validity fix — the arc IS the boundary, valid by
            //    definition, chord-based checks would wrongly reject it).
            var onArcNodes = _nodeCurve.Keys.ToList();
            for (int a = 0; a < onArcNodes.Count; a++)
            {
                for (int b = a + 1; b < onArcNodes.Count; b++)
                {
                    int i = onArcNodes[a], j = onArcNodes[b];
                    if (!ReferenceEquals(_nodeCurve[i].Curve, _nodeCurve[j].Curve)) continue;

                    double arcLen = CurveIntersectionHelper.ArcLengthBetween(
                        _nodeCurve[i].Curve, _nodeCurve[i].Parameter, _nodeCurve[j].Parameter);
                    AddEdge(i, j, arcLen);
                }
            }

            // ── Step D: plain straight edges between original vertices (handles
            //    non-arc / straight-edge concavities as routing waypoints, same
            //    as the previous graph fallback).
            //
            //    Same-arc pairs on an OBSTACLE arc skip the straight attempt —
            //    the arc length from Step C is the only valid connection.
            //    Same-arc pairs on a CONCAVE boundary arc still get a straight
            //    attempt: the chord may be shorter than the arc length and is
            //    geometrically valid there, so let Dijkstra pick whichever is
            //    actually shorter instead of forcing the arc route.
            for (int i = 0; i < _baseCount; i++)
            {
                for (int j = i + 1; j < _baseCount; j++)
                {
                    if (_nodeCurve.ContainsKey(i) && _nodeCurve.ContainsKey(j) &&
                        ReferenceEquals(_nodeCurve[i].Curve, _nodeCurve[j].Curve) &&
                        IsObstacleArc(_nodeCurve[i].Curve))
                        continue; // obstacle arc — arc length from Step C is authoritative

                    TryAddStraightEdge(i, j);
                }
            }
        }

        private void TryAddStraightEdge(int i, int j)
        {
            if (i == j) return;
            XYZ a = _nodes[i], b = _nodes[j];
            double chord = a.DistanceTo(b);

            if (chord < 0.033)
            {
                Diagnostics.Add($"edge {NodeLabel(i)}-{NodeLabel(j)} skipped: chord {chord:F4}ft below min (0.033ft)");
                return;
            }
            if (chord > _edgeThresholdFt)
            {
                Diagnostics.Add($"edge {NodeLabel(i)}-{NodeLabel(j)} skipped: chord {chord:F2}ft exceeds threshold {_edgeThresholdFt:F2}ft");
                return;
            }
            if (!IsValidEdge(a, b))
            {
                Diagnostics.Add($"edge {NodeLabel(i)}-{NodeLabel(j)} rejected: leaves top face");
                return;
            }
            if (CutsAcrossArc(a, b, out int blockingArcIdx))
            {
                Diagnostics.Add($"edge {NodeLabel(i)}-{NodeLabel(j)} rejected: crosses arc{blockingArcIdx} (obstacle)");
                return;
            }

            AddEdge(i, j, chord);
        }

        private void AddEdge(int i, int j, double weight)
        {
            if (!_adj.ContainsKey(i)) _adj[i] = new List<int>();
            if (!_adj.ContainsKey(j)) _adj[j] = new List<int>();
            _adj[i].Add(j);
            _adj[j].Add(i);
            _edgeWeight[(i, j)] = weight;
            _edgeWeight[(j, i)] = weight;
        }

        /// <summary>Finds an existing node within tolerance, or appends a new one.</summary>
        private int GetOrAddNode(XYZ pos, Arc onArc, double param)
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i].DistanceTo(pos) <= _curveTolFt)
                {
                    if (onArc != null && !_nodeCurve.ContainsKey(i))
                        _nodeCurve[i] = new VertexCurveInfo { Curve = onArc, Parameter = param };
                    return i;
                }
            }

            _nodes.Add(pos);
            int idx = _nodes.Count - 1;
            _adj[idx] = new List<int>();
            if (onArc != null)
                _nodeCurve[idx] = new VertexCurveInfo { Curve = onArc, Parameter = param };
            return idx;
        }

        // ────────────────────────────────────────────────────────────────
        // Arc convexity classification
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Classifies every arc as either an OBSTACLE (opening / true convex
        /// bulge — the chord-to-arc sliver is void, so paths must route
        /// around it via tangent/bitangent lines) or a CONCAVE BOUNDARY arc
        /// (the sliver is real roof face, so straight chords across it are
        /// valid and obstacle-avoidance routing is unnecessary/wrong).
        /// </summary>
        private void ClassifyArcs()
        {
            for (int k = 0; k < _arcs.Count; k++)
            {
                Arc arc = _arcs[k];
                bool isObstacle = IsArcAnObstacle(arc);
                _arcClassification.Add((arc, isObstacle));
                Diagnostics.Add(
                    $"arc{k}: radius {arc.Radius:F3}ft, classified " +
                    (isObstacle ? "OBSTACLE (routes around it)" : "CONCAVE boundary (chords across it allowed)"));
            }
        }

        private bool IsObstacleArc(Arc arc)
        {
            foreach (var (a, isObstacle) in _arcClassification)
                if (ReferenceEquals(a, arc))
                    return isObstacle;

            return true; // unclassified — fall back to the safe/conservative behavior
        }

        private int ArcIndexOf(Arc arc)
        {
            for (int k = 0; k < _arcs.Count; k++)
                if (ReferenceEquals(_arcs[k], arc)) return k;
            return -1;
        }

        /// <summary>
        /// Probes the sliver between an arc's chord and the arc itself. If
        /// that sliver point projects onto the top face, the sliver is real
        /// roof material — the arc bulges into the face (concave boundary),
        /// not away from it — so it's NOT an obstacle.
        /// </summary>
        private bool IsArcAnObstacle(Arc arc)
        {
            double lo = arc.GetEndParameter(0);
            double hi = arc.GetEndParameter(1);

            XYZ p0 = arc.Evaluate(lo, false);
            XYZ p1 = arc.Evaluate(hi, false);
            XYZ chordMid = (p0 + p1) * 0.5;

            XYZ arcMid = arc.Evaluate((lo + hi) * 0.5, false);

            XYZ dir = arcMid - chordMid;
            double sliverDepth = dir.GetLength();
            if (sliverDepth < 1e-6) return true; // degenerate arc — treat conservatively as an obstacle

            dir = dir.Normalize();

            double probeStep = Math.Min(Math.Max(_curveTolFt * 3.0, 0.02), sliverDepth * 0.5);
            XYZ probe = arcMid - dir * probeStep; // just inside the sliver, off the arc itself

            bool sliverIsOnFace = PointOnTopFace(probe);
            return !sliverIsOnFace;
        }

        // ────────────────────────────────────────────────────────────────
        // Closed-form tangent / bitangent geometry
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Exact tangent points from an external point to a circular arc's
        /// circle. Returns 0 (point inside/on the circle), 1, or 2 candidates
        /// (up to 2 — one per tangent side). Parameters/points falling outside
        /// the arc's actual bounded segment are clamped to the nearer arc
        /// endpoint (mandatory waypoint) rather than discarded.
        /// </summary>
        private List<(double param, XYZ point)> ComputeTangentPoints(XYZ p, Arc arc)
        {
            var results = new List<(double, XYZ)>();

            XYZ center = arc.Center;
            XYZ xdir = arc.XDirection;
            XYZ ydir = arc.YDirection;
            double r = arc.Radius;

            XYZ cp = p - center;
            double cpx = cp.DotProduct(xdir);
            double cpy = cp.DotProduct(ydir);
            double d = Math.Sqrt(cpx * cpx + cpy * cpy);

            if (d <= r + _curveTolFt) return results; // inside/on the circle — no external tangent

            double phi = Math.Atan2(cpy, cpx);
            double theta = Math.Acos(Math.Min(1.0, r / d));

            double lo = arc.GetEndParameter(0);
            double hi = arc.GetEndParameter(1);

            foreach (double sign in new[] { 1.0, -1.0 })
            {
                double rawParam = phi + sign * theta;
                double clamped = ClampParamToArc(rawParam, lo, hi);
                XYZ point = arc.Evaluate(clamped, false);
                results.Add((clamped, point));
            }

            return results;
        }

        /// <summary>
        /// Exact external (same-side) bitangent lines between two arcs' circles.
        /// Returns 0, 1, or 2 candidate (point-on-arc1, point-on-arc2) pairs.
        /// Assumes both arcs are coplanar (both on the same roof top face),
        /// using arc1's local (XDirection,YDirection) as the shared 2D basis.
        /// Out-of-bound parameters are clamped to the nearer arc endpoint.
        /// </summary>
        private List<(XYZ p1, double param1, XYZ p2, double param2)> ComputeExternalBitangents(Arc arc1, Arc arc2)
        {
            var results = new List<(XYZ, double, XYZ, double)>();

            XYZ c1 = arc1.Center, c2 = arc2.Center;
            double r1 = arc1.Radius, r2 = arc2.Radius;
            XYZ xdir = arc1.XDirection, ydir = arc1.YDirection;

            XYZ delta = c2 - c1;
            double dx = delta.DotProduct(xdir);
            double dy = delta.DotProduct(ydir);
            double d = Math.Sqrt(dx * dx + dy * dy);
            if (d < _curveTolFt) return results; // concentric — no external tangent

            double diff = r1 - r2;
            if (Math.Abs(diff) > d) return results; // one circle fully inside the other

            double baseAngle = Math.Atan2(dy, dx);
            double theta = Math.Acos(Math.Max(-1.0, Math.Min(1.0, diff / d)));

            double lo1 = arc1.GetEndParameter(0), hi1 = arc1.GetEndParameter(1);
            double lo2 = arc2.GetEndParameter(0), hi2 = arc2.GetEndParameter(1);

            foreach (double sign in new[] { 1.0, -1.0 })
            {
                double angle = baseAngle + sign * theta;
                XYZ t1 = c1 + r1 * (Math.Cos(angle) * xdir + Math.Sin(angle) * ydir);
                XYZ t2 = c2 + r2 * (Math.Cos(angle) * xdir + Math.Sin(angle) * ydir);

                // Re-derive each tangent point's own parameter on its own arc
                // (arc2 may have a different local frame than arc1).
                double param1 = ClampParamToArc(arc1.Project(t1)?.Parameter ?? 0, lo1, hi1);
                double param2 = ClampParamToArc(arc2.Project(t2)?.Parameter ?? 0, lo2, hi2);

                XYZ finalT1 = arc1.Evaluate(param1, false);
                XYZ finalT2 = arc2.Evaluate(param2, false);

                results.Add((finalT1, param1, finalT2, param2));
            }

            return results;
        }

        /// <summary>Clamps a raw arc parameter into [lo,hi], wrapping by ±2π first if that gets it closer.</summary>
        private static double ClampParamToArc(double param, double lo, double hi)
        {
            if (param < lo - Math.PI) param += 2 * Math.PI;
            if (param > hi + Math.PI) param -= 2 * Math.PI;

            if (param < lo) return lo;
            if (param > hi) return hi;
            return param;
        }

        // ────────────────────────────────────────────────────────────────
        // Shared validity check (unchanged from the ray-cast version)
        // ────────────────────────────────────────────────────────────────

        private bool CutsAcrossArc(XYZ a, XYZ b, out int blockingArcIndex)
        {
            blockingArcIndex = -1;
            if (_arcs.Count == 0) return false;

            Line line;
            try { line = Line.CreateBound(a, b); }
            catch { return false; }

            for (int k = 0; k < _arcs.Count; k++)
            {
                Arc arc = _arcs[k];
                if (!IsObstacleArc(arc)) continue; // concave boundary — crossing it is fine if still on the face

                IntersectionResultArray xsects;
                SetComparisonResult result;
                try { result = line.Intersect(arc, out xsects); }
                catch { continue; }

                if (result != SetComparisonResult.Overlap || xsects == null) continue;

                foreach (IntersectionResult ir in xsects)
                {
                    XYZ p = ir.XYZPoint;
                    if (p == null) continue;
                    if (p.DistanceTo(a) > _curveTolFt && p.DistanceTo(b) > _curveTolFt)
                    {
                        blockingArcIndex = k;
                        return true;
                    }
                }
            }
            return false;
        }

        private bool IsValidEdge(XYZ a, XYZ b)
        {
            Line ln;
            try { ln = Line.CreateBound(a, b); }
            catch { return false; }

            double len = a.DistanceTo(b);
            int samples = Math.Max(10, (int)(len * 4));
            double step = 1.0 / samples;

            for (double t = step; t < 1.0; t += step)
            {
                XYZ p = ln.Evaluate(t, true);
                if (!PointOnTopFace(p)) return false;
            }
            return true;
        }

        private bool PointOnTopFace(XYZ p)
        {
            IntersectionResult proj = _topFace.Project(p);
            if (proj == null)
            {
                proj = _topFace.Project(p + XYZ.BasisZ * PROJ_TOL)
                    ?? _topFace.Project(p - XYZ.BasisZ * PROJ_TOL);
                if (proj == null) return false;
            }

            try
            {
                return _topFace.IsInside(proj.UVPoint);
            }
            catch
            {
                BoundingBoxUV bb = _topFace.GetBoundingBox();
                UV uv = proj.UVPoint;
                return uv.U >= bb.Min.U && uv.U <= bb.Max.U &&
                       uv.V >= bb.Min.V && uv.V <= bb.Max.V;
            }
        }
    }
}
