using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByPoint.V011.Core.Engine
{
    public class DijkstraPathEngine
    {
        private readonly List<SlabShapeVertex> _verts;
        private readonly Dictionary<int, List<int>> _adj = new();
        private readonly Dictionary<(int, int), double> _edgeWeight = new();
        private readonly Face _topFace;
        private readonly double _edgeThresholdFt;
        private readonly List<Arc> _arcs;
        private readonly double _curveTolFt;
        private readonly Dictionary<int, VertexCurveInfo> _curveMap;
        private const double PROJ_TOL = 0.00328084;

        /// <param name="vertices">All roof shape vertices (including any inserted curve-intersection points).</param>
        /// <param name="topFace">Roof top face.</param>
        /// <param name="edgeThresholdFt">Max candidate edge length.</param>
        /// <param name="arcs">Boundary/opening arcs (outer + inner loops). Pass null/empty to disable arc-length handling.</param>
        /// <param name="curveTolFt">Tolerance for treating a vertex as lying "on" an arc.</param>
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

            var positions = new List<XYZ>(_verts.Count);
            foreach (var v in _verts) positions.Add(v.Position);
            _curveMap = CurveIntersectionHelper.MapVerticesOnCurves(positions, _arcs, _curveTolFt);

            BuildGraph();
        }

        private void BuildGraph()
        {
            int n = _verts.Count;
            for (int i = 0; i < n; i++)
                _adj[i] = new List<int>();

            for (int i = 0; i < n; i++)
            {
                XYZ a = _verts[i].Position;
                for (int j = i + 1; j < n; j++)
                {
                    XYZ b = _verts[j].Position;
                    double chord = a.DistanceTo(b);
                    if (chord < 0.033 || chord > _edgeThresholdFt) continue;
                    if (!IsValidEdge(a, b)) continue;

                    // Reject "shortcut" chords that cut across an arc mid-span
                    // (both intersection points strictly interior to the segment,
                    // i.e. neither endpoint is the arc entry/exit point itself).
                    if (CutsAcrossArc(a, b)) continue;

                    double weight = chord;

                    // If both endpoints of this edge sit on the SAME arc, use the
                    // true arc length between them instead of the straight chord.
                    if (_curveMap.TryGetValue(i, out var ci) &&
                        _curveMap.TryGetValue(j, out var cj) &&
                        ReferenceEquals(ci.Curve, cj.Curve))
                    {
                        weight = CurveIntersectionHelper.ArcLengthBetween(ci.Curve, ci.Parameter, cj.Parameter);
                    }

                    _adj[i].Add(j);
                    _adj[j].Add(i);
                    _edgeWeight[(i, j)] = weight;
                    _edgeWeight[(j, i)] = weight;
                }
            }
        }

        /// <summary>
        /// True if the straight segment a→b crosses an arc at a point that is
        /// strictly interior to the segment (not at/near a or b) — meaning this
        /// chord would shortcut across the curve instead of following it.
        /// </summary>
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

        private double Weight(int i, int j)
        {
            return _edgeWeight.TryGetValue((i, j), out double w)
                ? w
                : _verts[i].Position.DistanceTo(_verts[j].Position);
        }

        private bool IsValidEdge(XYZ a, XYZ b)
        {
            Line ln = Line.CreateBound(a, b);
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

        /// <summary>
        /// Legacy per‑vertex Dijkstra (kept for reference, but not used in main engine)
        /// </summary>
        public double ComputeShortestPath(int start, HashSet<int> drains)
        {
            int n = _verts.Count;
            var dist = new double[n];
            var visited = new bool[n];

            for (int i = 0; i < n; i++)
                dist[i] = double.PositiveInfinity;

            dist[start] = 0;
            var pq = new SortedSet<(double, int)>(
                Comparer<(double, int)>.Create((a, b) =>
                {
                    int c = a.Item1.CompareTo(b.Item1);
                    return c != 0 ? c : a.Item2.CompareTo(b.Item2);
                }));

            pq.Add((0, start));

            while (pq.Count > 0)
            {
                var (d, v) = pq.Min;
                pq.Remove(pq.Min);
                if (visited[v]) continue;
                visited[v] = true;
                if (drains.Contains(v)) return d;

                foreach (int nb in _adj[v])
                {
                    double nd = d + Weight(v, nb);
                    if (nd < dist[nb])
                    {
                        dist[nb] = nd;
                        pq.Add((nd, nb));
                    }
                }
            }
            return double.PositiveInfinity;
        }

        /// <summary>
        /// Multi‑source reverse Dijkstra – computes shortest distance from every vertex
        /// to the nearest drain in a single pass.
        /// </summary>
        /// <param name="drains">Set of vertex indices that are drains.</param>
        /// <returns>Array of shortest path lengths (in internal feet) for each vertex.</returns>
        public double[] ComputeAllDistances(HashSet<int> drains)
        {
            int n = _verts.Count;
            var dist = new double[n];
            for (int i = 0; i < n; i++)
                dist[i] = double.PositiveInfinity;

            var pq = new SortedSet<(double, int)>(
                Comparer<(double, int)>.Create((a, b) =>
                {
                    int cmp = a.Item1.CompareTo(b.Item1);
                    return cmp != 0 ? cmp : a.Item2.CompareTo(b.Item2);
                }));

            // Initialise all drains with distance 0
            foreach (int drain in drains)
            {
                dist[drain] = 0;
                pq.Add((0, drain));
            }

            while (pq.Count > 0)
            {
                var (d, v) = pq.Min;
                pq.Remove(pq.Min);
                if (d > dist[v]) continue; // stale entry

                foreach (int nb in _adj[v])
                {
                    double nd = d + Weight(v, nb);
                    if (nd < dist[nb])
                    {
                        dist[nb] = nd;
                        pq.Add((nd, nb));
                    }
                }
            }
            return dist;
        }
    }
}