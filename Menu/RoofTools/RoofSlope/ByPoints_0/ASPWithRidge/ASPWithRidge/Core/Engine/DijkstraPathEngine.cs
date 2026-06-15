// =======================================================
// File: DijkstraPathEngine.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.WithRidge
// Changes (performance + ridge support):
//
//   OLD approach: ComputeShortestPath(vertexIndex, drains)
//     — one full Dijkstra per vertex, early exit on first drain.
//     — 500 vertices = 500 Dijkstra runs.  Too slow.
//
//   NEW approach: BuildDrainDistanceTable()
//     — one Dijkstra per DRAIN, run outward from each drain.
//     — 5 drains = 5 Dijkstra runs, covers all 500 vertices.
//     — Result: _distTable[drainIndex][vertexIndex] = path ft.
//     — Any vertex query is then an O(1) table lookup.
//
//   Public API for engine:
//     GetPathToAllDrains(vertexIndex)
//       → Dictionary<int, double>  (drainIndex → pathFt)
//         used by ridge detection and normal elevation.
//
//   ComputeShortestPath kept for backward compatibility
//   but now delegates to the table (no re-computation).
// =======================================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByPoint.WithRidge.Core.Engine
{
    public class DijkstraPathEngine
    {
        private readonly List<SlabShapeVertex> _verts;
        private readonly Dictionary<int, List<(int neighbour, double dist)>> _adj = new();
        private readonly Face _topFace;
        private readonly double _edgeThresholdFt;
        private const double PROJ_TOL = 0.00328084;

        // _distTable[drainIndex][vertexIndex] = shortest path in feet from that drain.
        // Populated once in BuildDrainDistanceTable().
        private Dictionary<int, double[]> _distTable;
        private HashSet<int> _drainIndices;

        public DijkstraPathEngine(
            List<SlabShapeVertex> vertices,
            Face topFace,
            double edgeThresholdFt)
        {
            _verts = vertices;
            _topFace = topFace;
            _edgeThresholdFt = edgeThresholdFt;
            BuildGraph();
        }

        // ── Graph construction ───────────────────────────────────────────────

        private void BuildGraph()
        {
            int n = _verts.Count;
            for (int i = 0; i < n; i++)
                _adj[i] = new List<(int, double)>();

            for (int i = 0; i < n; i++)
            {
                XYZ a = _verts[i].Position;
                for (int j = i + 1; j < n; j++)
                {
                    XYZ b = _verts[j].Position;
                    double dist = a.DistanceTo(b);
                    if (dist < 0.033 || dist > _edgeThresholdFt) continue;
                    if (!IsValidEdge(a, b)) continue;
                    _adj[i].Add((j, dist));
                    _adj[j].Add((i, dist));
                }
            }
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

        // ── Distance table (one Dijkstra per drain) ──────────────────────────

        /// <summary>
        /// Must be called once after construction, before any query.
        /// Runs one Dijkstra outward from each drain and populates
        /// the full distance table. O(D × V log V) where D = drain count.
        /// </summary>
        public void BuildDrainDistanceTable(HashSet<int> drainIndices)
        {
            _drainIndices = drainIndices;
            _distTable = new Dictionary<int, double[]>();

            foreach (int drainIdx in drainIndices)
            {
                _distTable[drainIdx] = RunDijkstraFrom(drainIdx);
            }
        }

        private double[] RunDijkstraFrom(int source)
        {
            int n = _verts.Count;
            double[] dist = new double[n];
            bool[] visited = new bool[n];

            for (int i = 0; i < n; i++)
                dist[i] = double.PositiveInfinity;

            dist[source] = 0;

            var pq = new SortedSet<(double d, int v)>(
                Comparer<(double, int)>.Create((a, b) =>
                {
                    int c = a.Item1.CompareTo(b.Item1);
                    return c != 0 ? c : a.Item2.CompareTo(b.Item2);
                }));

            pq.Add((0, source));

            while (pq.Count > 0)
            {
                var (d, v) = pq.Min;
                pq.Remove(pq.Min);
                if (visited[v]) continue;
                visited[v] = true;

                foreach (var (nb, edgeDist) in _adj[v])
                {
                    double nd = d + edgeDist;
                    if (nd < dist[nb])
                    {
                        dist[nb] = nd;
                        pq.Add((nd, nb));
                    }
                }
            }

            return dist;
        }

        // ── Public query API ─────────────────────────────────────────────────

        /// <summary>
        /// Returns path length in feet from each drain to the given vertex.
        /// Key = drainIndex, Value = path in feet (Infinity if unreachable).
        /// O(1) — just reads from the pre-built table.
        /// Requires BuildDrainDistanceTable() to have been called first.
        /// </summary>
        public Dictionary<int, double> GetPathToAllDrains(int vertexIndex)
        {
            var result = new Dictionary<int, double>();
            foreach (var kvp in _distTable)
                result[kvp.Key] = kvp.Value[vertexIndex];
            return result;
        }

        /// <summary>
        /// Backward-compatible: returns shortest path in feet to any drain.
        /// Delegates to table — no re-computation.
        /// </summary>
        public double GetShortestPathToDrain(int vertexIndex)
        {
            double min = double.PositiveInfinity;
            foreach (var kvp in _distTable)
            {
                double d = kvp.Value[vertexIndex];
                if (d < min) min = d;
            }
            return min;
        }
    }
}
