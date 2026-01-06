using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit22_Plugin.V4_02.Domain.Services
{
    public class PathSolverService
    {
        public Dictionary<SlabShapeVertex, (SlabShapeVertex drain, double dist)>
            ComputePathsToDrains(
                Dictionary<SlabShapeVertex, List<SlabShapeVertex>> graph,
                List<SlabShapeVertex> drains)
        {
            var result = new Dictionary<SlabShapeVertex, (SlabShapeVertex, double)>();

            foreach (var v in graph.Keys)
            {
                if (drains.Contains(v))
                {
                    result[v] = (v, 0);
                    continue;
                }

                double min = double.MaxValue;
                SlabShapeVertex nearest = null;

                foreach (var d in drains)
                {
                    double dist = Dijkstra(v, d, graph);
                    if (dist < min)
                    {
                        min = dist;
                        nearest = d;
                    }
                }

                if (nearest != null)
                    result[v] = (nearest, min);
            }

            return result;
        }

        private double Dijkstra(
            SlabShapeVertex start,
            SlabShapeVertex end,
            Dictionary<SlabShapeVertex, List<SlabShapeVertex>> graph)
        {
            var dist = graph.Keys.ToDictionary(v => v, v => double.MaxValue);
            dist[start] = 0;

            var queue = new List<SlabShapeVertex> { start };

            while (queue.Count > 0)
            {
                queue.Sort((a, b) => dist[a].CompareTo(dist[b]));
                var current = queue[0];
                queue.RemoveAt(0);

                if (current == end)
                    return dist[current];

                foreach (var n in graph[current])
                {
                    double alt = dist[current] +
                                 current.Position.DistanceTo(n.Position);

                    if (alt < dist[n])
                    {
                        dist[n] = alt;
                        if (!queue.Contains(n))
                            queue.Add(n);
                    }
                }
            }

            return double.MaxValue;
        }
    }
}
