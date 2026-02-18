using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByDrain_06_00.Core.Engine
{
    public class DijkstraPathEngine
    {
        private readonly List<SlabShapeVertex> _verts;
        private readonly Dictionary<int, List<int>> _adj = new();
        private readonly Face _topFace;
        private readonly double _edgeThresholdFt;
        private const double PROJ_TOL = 0.00328084;

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
                    double dist = a.DistanceTo(b);
                    if (dist < 0.5 || dist > _edgeThresholdFt) continue;
                    if (!IsValidEdge(a, b)) continue;
                    _adj[i].Add(j);
                    _adj[j].Add(i);
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
                    double nd = d + _verts[v].Position.DistanceTo(_verts[nb].Position);
                    if (nd < dist[nb])
                    {
                        dist[nb] = nd;
                        pq.Add((nd, nb));
                    }
                }
            }
            return double.PositiveInfinity;
        }
    }
}