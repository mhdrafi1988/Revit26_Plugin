using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit22_Plugin.AutoSlopeV3.Engine
{
    /// <summary>
    /// TRUE DIJKSTRA ENGINE with VOID AVOIDANCE.
    /// Builds a walkable graph of slab-shape vertices by validating edges
    /// against the roof TopFace. Any edge crossing a void/opening is rejected.
    /// </summary>
    public class DijkstraPathEngine
    {
        private readonly List<SlabShapeVertex> _verts;
        private readonly Dictionary<int, List<int>> _adj;
        private readonly Face _topFace;
        private readonly double _edgeThresholdFt;

        public DijkstraPathEngine(
            List<SlabShapeVertex> vertices,
            Face topFace,
            double edgeThresholdFt)
        {
            _verts = vertices;
            _topFace = topFace;
            _edgeThresholdFt = edgeThresholdFt;
            _adj = new Dictionary<int, List<int>>();

            BuildGraph();
        }

        // ============================================================
        // GRAPH BUILDING
        // ============================================================
        private void BuildGraph()
        {
            int count = _verts.Count;

            for (int i = 0; i < count; i++)
                _adj[i] = new List<int>();

            for (int i = 0; i < count; i++)
            {
                XYZ a = _verts[i].Position;
                if (a == null) continue;

                for (int j = i + 1; j < count; j++)
                {
                    XYZ b = _verts[j].Position;
                    if (b == null) continue;

                    double dist = a.DistanceTo(b);

                    // ignore micro edges
                    if (dist < 0.5) continue;

                    // ignore long edges
                    if (dist > _edgeThresholdFt) continue;

                    // VOID AVOIDANCE CHECK
                    if (!IsValidEdge(a, b))
                        continue;

                    // valid walkable edge
                    _adj[i].Add(j);
                    _adj[j].Add(i);
                }
            }
        }

        // ============================================================
        // VALID EDGE TEST – rejects edges crossing openings/voids
        // ============================================================
        private bool IsValidEdge(XYZ a, XYZ b)
        {
            try
            {
                Line ln = Line.CreateBound(a, b);

                // sample 10 points on segment
                for (double t = 0.1; t < 1.0; t += 0.1)
                {
                    XYZ p = ln.Evaluate(t, true);
                    if (!PointOnTopFace(p))
                        return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool PointOnTopFace(XYZ p)
        {
            if (_topFace == null) return false;

            IntersectionResult res = _topFace.Project(p);
            if (res == null) return false;

            UV uv = res.UVPoint;
            BoundingBoxUV bb = _topFace.GetBoundingBox();
            if (bb == null) return false;

            return (uv.U >= bb.Min.U &&
                    uv.U <= bb.Max.U &&
                    uv.V >= bb.Min.V &&
                    uv.V <= bb.Max.V);
        }

        // ============================================================
        // TRUE DIJKSTRA SHORTEST PATH
        // Returns shortest walkable distance (feet)
        // ============================================================
        public double ComputeShortestPath(int startIndex, HashSet<int> drainIndices)
        {
            int n = _verts.Count;

            var dist = new Dictionary<int, double>();
            var visited = new HashSet<int>();

            // initialize all distances
            for (int i = 0; i < n; i++)
                dist[i] = double.PositiveInfinity;

            dist[startIndex] = 0;

            // priority queue using SortedSet
            var queue = new SortedSet<(double, int)>(
                Comparer<(double, int)>.Create((a, b) =>
                {
                    // compare by distance
                    int c = a.Item1.CompareTo(b.Item1);
                    if (c != 0) return c;

                    // tie-break by vertex index
                    return a.Item2.CompareTo(b.Item2);
                })
            );

            queue.Add((0, startIndex));

            while (queue.Count > 0)
            {
                var current = queue.Min;
                queue.Remove(current);

                double curDist = current.Item1;
                int v = current.Item2;

                if (visited.Contains(v))
                    continue;

                visited.Add(v);

                // if this vertex is a drain → shortest path found
                if (drainIndices.Contains(v))
                    return curDist;

                foreach (int nb in _adj[v])
                {
                    double nd = curDist +
                                _verts[v].Position.DistanceTo(_verts[nb].Position);

                    if (nd < dist[nb])
                    {
                        dist[nb] = nd;
                        queue.Add((nd, nb));
                    }
                }
            }

            return double.PositiveInfinity; // unreachable
        }
    }
}
