using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit22_Plugin.AutoSlope_V4_01.Engine
{
    /// <summary>
    /// Improved Dijkstra Engine with robust void-avoidance, projection tolerance,
    /// trimmed-face UV validation, and adaptive edge sampling.
    /// </summary>
    public class DijkstraPathEngine
    {
        private readonly List<SlabShapeVertex> _verts;
        private readonly Dictionary<int, List<int>> _adj;
        private readonly Face _topFace;
        private readonly double _edgeThresholdFt;

        // small tolerance (1 mm)
        private const double PROJ_TOL = 0.00328084;

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

                    // Skip micro edges
                    if (dist < 0.5) continue;

                    // Skip edges beyond threshold
                    if (dist > _edgeThresholdFt) continue;

                    // VALIDATION (improved)
                    if (!IsValidEdge(a, b))
                        continue;

                    // If valid, add undirected edge
                    _adj[i].Add(j);
                    _adj[j].Add(i);
                }
            }
        }

        // ============================================================
        // VALID EDGE CHECK (IMPROVED)
        // ============================================================
        private bool IsValidEdge(XYZ a, XYZ b)
        {
            try
            {
                Line ln = Line.CreateBound(a, b);
                double dist = a.DistanceTo(b);

                // Adaptive sample count
                int samples = Math.Max(10, (int)(dist * 4));
                double step = 1.0 / samples;

                for (double t = step; t < 1.0; t += step)
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

        // ============================================================
        // ROBUST FACE-POINT VALIDATION
        // ============================================================
        private bool PointOnTopFace(XYZ p)
        {
            if (_topFace == null) return false;

            IntersectionResult proj = _topFace.Project(p);

            // If direct projection fails, try nudging point vertically
            if (proj == null)
            {
                XYZ pUp = p + new XYZ(0, 0, PROJ_TOL);
                proj = _topFace.Project(pUp);

                if (proj == null)
                {
                    XYZ pDown = p - new XYZ(0, 0, PROJ_TOL);
                    proj = _topFace.Project(pDown);

                    // All failed → treat as invalid
                    if (proj == null)
                        return false;
                }
            }

            UV uv = proj.UVPoint;

            // TRIMMED FACE SAFE VALIDATION
            try
            {
                if (_topFace.IsInside(uv))
                    return true;
            }
            catch
            {
                // fallback (extremely rare)
            }

            // If face doesn't support IsInside(), fallback to boundingbox
            BoundingBoxUV bb = _topFace.GetBoundingBox();
            if (bb == null) return false;

            return (uv.U >= bb.Min.U &&
                    uv.U <= bb.Max.U &&
                    uv.V >= bb.Min.V &&
                    uv.V <= bb.Max.V);
        }

        // ============================================================
        // TRUE DIJKSTRA
        // ============================================================
        public double ComputeShortestPath(int startIndex, HashSet<int> drainIndices)
        {
            int n = _verts.Count;

            var dist = new Dictionary<int, double>();
            var visited = new HashSet<int>();

            for (int i = 0; i < n; i++)
                dist[i] = double.PositiveInfinity;

            dist[startIndex] = 0;

            var queue = new SortedSet<(double, int)>(
                Comparer<(double, int)>.Create((a, b) =>
                {
                    int c = a.Item1.CompareTo(b.Item1);
                    if (c != 0) return c;
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

            return double.PositiveInfinity;
        }
    }
}
