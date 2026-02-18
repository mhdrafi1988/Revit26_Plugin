using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.AutoSlope.V5_00.Core.Engine
{
    public class PathSolverService
    {
        private readonly List<SlabShapeVertex> _vertices;
        private readonly Dictionary<int, List<int>> _graph;

        public PathSolverService(
            List<SlabShapeVertex> vertices,
            Dictionary<int, List<int>> graph)
        {
            _vertices = vertices;
            _graph = graph;
        }

        public double ComputeShortestPath(int startIndex, HashSet<int> drainIndices)
        {
            int n = _vertices.Count;
            var dist = new double[n];
            var visited = new bool[n];

            for (int i = 0; i < n; i++)
                dist[i] = double.PositiveInfinity;

            dist[startIndex] = 0;

            var pq = new SortedSet<(double, int)>(
                Comparer<(double, int)>.Create((a, b) =>
                {
                    int c = a.Item1.CompareTo(b.Item1);
                    return c != 0 ? c : a.Item2.CompareTo(b.Item2);
                }));

            pq.Add((0, startIndex));

            while (pq.Count > 0)
            {
                var (d, v) = pq.Min;
                pq.Remove(pq.Min);

                if (visited[v]) continue;
                visited[v] = true;

                if (drainIndices.Contains(v))
                    return d;

                if (!_graph.ContainsKey(v)) continue;

                foreach (int nb in _graph[v])
                {
                    if (visited[nb]) continue;

                    double nd = d + _vertices[v].Position.DistanceTo(_vertices[nb].Position);
                    if (nd < dist[nb])
                    {
                        dist[nb] = nd;
                        pq.Add((nd, nb));
                    }
                }
            }

            return double.PositiveInfinity;
        }

        public List<int> ReconstructPath(int startIndex, int endIndex)
        {
            int n = _vertices.Count;
            var dist = new double[n];
            var prev = new int[n];
            var visited = new bool[n];

            for (int i = 0; i < n; i++)
            {
                dist[i] = double.PositiveInfinity;
                prev[i] = -1;
            }

            dist[startIndex] = 0;

            var pq = new SortedSet<(double, int)>(
                Comparer<(double, int)>.Create((a, b) =>
                {
                    int c = a.Item1.CompareTo(b.Item1);
                    return c != 0 ? c : a.Item2.CompareTo(b.Item2);
                }));

            pq.Add((0, startIndex));

            while (pq.Count > 0)
            {
                var (d, v) = pq.Min;
                pq.Remove(pq.Min);

                if (visited[v]) continue;
                visited[v] = true;

                if (v == endIndex) break;

                if (!_graph.ContainsKey(v)) continue;

                foreach (int nb in _graph[v])
                {
                    if (visited[nb]) continue;

                    double nd = d + _vertices[v].Position.DistanceTo(_vertices[nb].Position);
                    if (nd < dist[nb])
                    {
                        dist[nb] = nd;
                        prev[nb] = v;
                        pq.Add((nd, nb));
                    }
                }
            }

            if (prev[endIndex] == -1)
                return null;

            var path = new List<int>();
            int current = endIndex;
            while (current != -1)
            {
                path.Insert(0, current);
                current = prev[current];
            }

            return path;
        }
    }
}