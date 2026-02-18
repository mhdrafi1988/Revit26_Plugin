using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit22_Plugin.Asd_V4_01.Services
{
    public class PathSolverService
    {
        public List<XYZ> DijkstraPath(
            SlabShapeVertex start,
            SlabShapeVertex end,
            Dictionary<SlabShapeVertex, List<SlabShapeVertex>> graph)
        {
            try
            {
                if (start == null || end == null || graph == null)
                    return null;

                if (start == end)
                    return new List<XYZ> { start.Position };

                var dist = new Dictionary<SlabShapeVertex, double>();
                var prev = new Dictionary<SlabShapeVertex, SlabShapeVertex>();
                var queue = new List<Tuple<double, SlabShapeVertex>>();

                foreach (var v in graph.Keys)
                {
                    dist[v] = double.MaxValue;
                    prev[v] = null;
                }

                dist[start] = 0;
                queue.Add(Tuple.Create(0.0, start));

                while (queue.Count > 0)
                {
                    queue.Sort((a, b) => a.Item1.CompareTo(b.Item1));
                    var item = queue[0];
                    queue.RemoveAt(0);

                    var current = item.Item2;

                    if (current == end)
                        break;

                    if (!graph.ContainsKey(current))
                        continue;

                    foreach (var n in graph[current])
                    {
                        double step = current.Position.DistanceTo(n.Position);
                        double alt = dist[current] + step;

                        if (alt < dist[n])
                        {
                            dist[n] = alt;
                            prev[n] = current;

                            queue.RemoveAll(x => x.Item2 == n);
                            queue.Add(Tuple.Create(alt, n));
                        }
                    }
                }

                return ReconstructPath(prev, end);
            }
            catch
            {
                return null;
            }
        }

        private List<XYZ> ReconstructPath(
            Dictionary<SlabShapeVertex, SlabShapeVertex> prev,
            SlabShapeVertex end)
        {
            if (!prev.ContainsKey(end))
                return null;

            var path = new List<XYZ>();
            var current = end;

            while (current != null)
            {
                path.Insert(0, current.Position);
                current = prev[current];
            }

            return path.Count > 0 ? path : null;
        }

        public Dictionary<SlabShapeVertex, (SlabShapeVertex drain, double dist, List<XYZ> path)>
            ComputePathsToDrains(
                Dictionary<SlabShapeVertex, List<SlabShapeVertex>> graph,
                List<SlabShapeVertex> drainPoints)
        {
            var result = new Dictionary<SlabShapeVertex, (SlabShapeVertex, double, List<XYZ>)>();

            foreach (var v in graph.Keys)
            {
                if (drainPoints.Contains(v))
                {
                    result[v] = (v, 0, new List<XYZ> { v.Position });
                    continue;
                }

                double minDist = double.MaxValue;
                SlabShapeVertex nearest = null;
                List<XYZ> bestPath = null;

                foreach (var d in drainPoints)
                {
                    var path = DijkstraPath(v, d, graph);
                    if (path == null || path.Count < 2) continue;

                    double length = CalculatePathLength(path);

                    if (length < minDist)
                    {
                        minDist = length;
                        nearest = d;
                        bestPath = path;
                    }
                }

                if (nearest != null)
                {
                    result[v] = (nearest, minDist, bestPath);
                }
            }

            return result;
        }

        private double CalculatePathLength(List<XYZ> path)
        {
            double len = 0;
            for (int i = 0; i < path.Count - 1; i++)
            {
                len += path[i].DistanceTo(path[i + 1]);
            }
            return len;
        }
    }
}
