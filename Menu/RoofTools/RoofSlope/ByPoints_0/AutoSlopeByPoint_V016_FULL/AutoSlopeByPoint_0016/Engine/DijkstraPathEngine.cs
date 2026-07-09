// =======================================================
// File: DijkstraPathEngine.cs
// Full corrected version with Fix #3 (node merging) and #4 (performance)
// =======================================================

using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlopeByPoint.V016.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByPoint.V016.Core.Engine
{
    public class DijkstraPathEngine
    {
        private readonly List<SlabShapeVertex> _verts;
        private readonly Face _topFace;
        private readonly double _edgeThresholdFt;
        private readonly List<Arc> _arcs;
        private readonly double _curveTolFt;
        private const double PROJ_TOL = 0.00328084;

        private readonly bool _enableArcTangents;
        private readonly MultiArcMode _multiArcMode;

        private readonly List<Arc> _obstacleArcs = new();
        public IReadOnlyDictionary<Arc, ArcConcavity> ArcConcavityMap => _arcConcavity;
        private readonly Dictionary<Arc, ArcConcavity> _arcConcavity = new();

        private readonly int _baseCount;

        private readonly List<XYZ> _nodes = new();
        private readonly Dictionary<int, VertexCurveInfo> _nodeCurve = new();
        private readonly Dictionary<int, List<int>> _adj = new();
        private readonly Dictionary<(int, int), double> _edgeWeight = new();

        private double[] _fullDist;
        private int[] _fullPred;

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

        public DijkstraPathEngine(
            List<SlabShapeVertex> vertices,
            Face topFace,
            double edgeThresholdFt,
            List<Arc> arcs = null,
            double curveTolFt = 0.0033,
            bool enableArcTangents = true,
            MultiArcMode multiArcMode = MultiArcMode.Sequential)
        {
            _verts = vertices;
            _topFace = topFace;
            _edgeThresholdFt = edgeThresholdFt;
            _arcs = arcs ?? new List<Arc>();
            _curveTolFt = curveTolFt;
            _baseCount = vertices.Count;
            _enableArcTangents = enableArcTangents;
            _multiArcMode = multiArcMode;

            foreach (var v in vertices) _nodes.Add(v.Position);
            for (int i = 0; i < _baseCount; i++) _adj[i] = new List<int>();

            foreach (Arc arc in _arcs)
            {
                ArcConcavity c = ArcClassifier.Classify(arc, _topFace);
                _arcConcavity[arc] = c;
                if (c == ArcConcavity.Convex) _obstacleArcs.Add(arc);
            }

            BuildVisibilityGraph();
        }

        public bool IsDirectlyVisible(XYZ a, XYZ b) => IsValidPath(a, b);

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

            var result = new double[_baseCount];
            Array.Copy(dist, result, _baseCount);
            return result;
        }

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

        private void BuildVisibilityGraph()
        {
            var positions = new List<XYZ>(_baseCount);
            for (int i = 0; i < _baseCount; i++) positions.Add(_nodes[i]);
            var baseCurveMap = CurveIntersectionHelper.MapVerticesOnCurves(positions, _arcs, _curveTolFt);
            foreach (var kv in baseCurveMap) _nodeCurve[kv.Key] = kv.Value;

            if (_enableArcTangents)
            {
                for (int i = 0; i < _baseCount; i++)
                {
                    if (_nodeCurve.ContainsKey(i)) continue;
                    XYZ from = _nodes[i];

                    foreach (Arc arc in _obstacleArcs)
                    {
                        foreach (var (param, point) in ComputeTangentPoints(from, arc))
                        {
                            int node = GetOrAddNode(point, arc, param);
                            TryAddStraightEdge(i, node);
                        }
                    }
                }

                var arcPairs = BuildObstacleArcPairs();
                foreach (var (arc1, arc2) in arcPairs)
                {
                    foreach (var (p1, param1, p2, param2) in ComputeExternalBitangents(arc1, arc2))
                    {
                        int n1 = GetOrAddNode(p1, arc1, param1);
                        int n2 = GetOrAddNode(p2, arc2, param2);
                        TryAddStraightEdge(n1, n2);
                    }
                }
            }

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

            for (int i = 0; i < _baseCount; i++)
            {
                for (int j = i + 1; j < _baseCount; j++)
                {
                    if (_nodeCurve.ContainsKey(i) && _nodeCurve.ContainsKey(j) &&
                        ReferenceEquals(_nodeCurve[i].Curve, _nodeCurve[j].Curve))
                        continue;

                    TryAddStraightEdge(i, j);
                }
            }
        }

        private List<(Arc, Arc)> BuildObstacleArcPairs()
        {
            var pairs = new List<(Arc, Arc)>();
            int n = _obstacleArcs.Count;
            if (n < 2) return pairs;

            if (n < 3 || _multiArcMode == MultiArcMode.PairwiseCombination)
            {
                for (int a1 = 0; a1 < n; a1++)
                    for (int a2 = a1 + 1; a2 < n; a2++)
                        pairs.Add((_obstacleArcs[a1], _obstacleArcs[a2]));
                return pairs;
            }

            var remaining = new List<Arc>(_obstacleArcs);
            Arc current = remaining[0];
            remaining.RemoveAt(0);
            while (remaining.Count > 0)
            {
                Arc nearest = null;
                double bestDist = double.MaxValue;
                foreach (Arc candidate in remaining)
                {
                    double d = current.Center.DistanceTo(candidate.Center);
                    if (d < bestDist) { bestDist = d; nearest = candidate; }
                }
                pairs.Add((current, nearest));
                remaining.Remove(nearest);
                current = nearest;
            }
            return pairs;
        }

        private void TryAddStraightEdge(int i, int j)
        {
            if (i == j) return;
            XYZ a = _nodes[i], b = _nodes[j];
            double chord = a.DistanceTo(b);
            if (chord < 0.033 || chord > _edgeThresholdFt) return;
            if (!IsValidPath(a, b)) return;
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

        // FIX #3: Correct node merging with arc identity check
        private int GetOrAddNode(XYZ pos, Arc onArc, double param)
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i].DistanceTo(pos) <= _curveTolFt)
                {
                    bool sameArc = (onArc == null && !_nodeCurve.ContainsKey(i)) ||
                                   (_nodeCurve.TryGetValue(i, out var info) && ReferenceEquals(info.Curve, onArc));
                    if (sameArc)
                    {
                        if (onArc != null && _nodeCurve.ContainsKey(i))
                            _nodeCurve[i].Parameter = param;
                        return i;
                    }
                }
            }

            _nodes.Add(pos);
            int idx = _nodes.Count - 1;
            _adj[idx] = new List<int>();
            if (onArc != null)
                _nodeCurve[idx] = new VertexCurveInfo { Curve = onArc, Parameter = param };
            return idx;
        }

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

            if (d <= r + _curveTolFt) return results;

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
            if (d < _curveTolFt) return results;

            double diff = r1 - r2;
            if (Math.Abs(diff) > d) return results;

            double baseAngle = Math.Atan2(dy, dx);
            double theta = Math.Acos(Math.Max(-1.0, Math.Min(1.0, diff / d)));

            double lo1 = arc1.GetEndParameter(0), hi1 = arc1.GetEndParameter(1);
            double lo2 = arc2.GetEndParameter(0), hi2 = arc2.GetEndParameter(1);

            foreach (double sign in new[] { 1.0, -1.0 })
            {
                double angle = baseAngle + sign * theta;
                XYZ t1 = c1 + r1 * (Math.Cos(angle) * xdir + Math.Sin(angle) * ydir);
                XYZ t2 = c2 + r2 * (Math.Cos(angle) * xdir + Math.Sin(angle) * ydir);

                double param1 = ClampParamToArc(arc1.Project(t1)?.Parameter ?? 0, lo1, hi1);
                double param2 = ClampParamToArc(arc2.Project(t2)?.Parameter ?? 0, lo2, hi2);

                XYZ finalT1 = arc1.Evaluate(param1, false);
                XYZ finalT2 = arc2.Evaluate(param2, false);

                results.Add((finalT1, param1, finalT2, param2));
            }

            return results;
        }

        private static double ClampParamToArc(double param, double lo, double hi)
        {
            if (param < lo - Math.PI) param += 2 * Math.PI;
            if (param > hi + Math.PI) param -= 2 * Math.PI;

            if (param < lo) return lo;
            if (param > hi) return hi;
            return param;
        }

        private bool IsValidPath(XYZ a, XYZ b)
        {
            if (!IsValidEdge(a, b)) return false;
            if (CutsAcrossArc(a, b)) return false;
            return true;
        }

        private bool CutsAcrossArc(XYZ a, XYZ b)
        {
            if (_arcs.Count == 0) return false;

            Line line;
            try { line = Line.CreateBound(a, b); }
            catch { return false; }

            foreach (Arc arc in _arcs)
            {
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
                        return true;
                }
            }
            return false;
        }

        // FIX #4: Performance – cap samples to 50
        private bool IsValidEdge(XYZ a, XYZ b)
        {
            Line ln;
            try { ln = Line.CreateBound(a, b); }
            catch { return false; }

            double len = a.DistanceTo(b);
            int samples = Math.Max(5, (int)(len / 0.5));
            samples = Math.Min(samples, 50);

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