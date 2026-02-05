using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit22_Plugin.Asd.Services
{
    public class PathSolverService
    {
        public List<XYZ> DijkstraPath(SlabShapeVertex start, SlabShapeVertex end,
            Dictionary<SlabShapeVertex, List<SlabShapeVertex>> graph)
        {
            try
            {
                if (start == null || end == null || graph == null) return null;
                if (start == end) return new List<XYZ> { start.Position }; // Same vertex

                var distances = new Dictionary<SlabShapeVertex, double>();
                var previous = new Dictionary<SlabShapeVertex, SlabShapeVertex>();
                var queue = new List<Tuple<double, SlabShapeVertex>>();

                // Initialize
                foreach (var vertex in graph.Keys)
                {
                    if (vertex == null) continue;
                    distances[vertex] = double.MaxValue;
                    previous[vertex] = null;
                }

                distances[start] = 0;
                queue.Add(Tuple.Create(0.0, start));

                while (queue.Count > 0)
                {
                    // Get vertex with minimum distance
                    queue.Sort((x, y) => x.Item1.CompareTo(y.Item1));
                    var currentTuple = queue[0];
                    double currentDist = currentTuple.Item1;
                    SlabShapeVertex current = currentTuple.Item2;
                    queue.RemoveAt(0);

                    // Early exit if we reached the target
                    if (current == end) break;

                    if (!graph.ContainsKey(current)) continue;

                    // Process neighbors
                    foreach (var neighbor in graph[current])
                    {
                        if (neighbor == null || current.Position == null || neighbor.Position == null)
                            continue;

                        double edgeWeight = current.Position.DistanceTo(neighbor.Position);
                        double altDistance = distances[current] + edgeWeight;

                        if (altDistance < distances[neighbor])
                        {
                            distances[neighbor] = altDistance;
                            previous[neighbor] = current;

                            // Update queue
                            queue.RemoveAll(x => x.Item2 == neighbor);
                            queue.Add(Tuple.Create(altDistance, neighbor));
                        }
                    }
                }

                // Reconstruct path
                return ReconstructPath(previous, end);
            }
            catch
            {
                return null;
            }
        }

        private List<XYZ> ReconstructPath(Dictionary<SlabShapeVertex, SlabShapeVertex> previous, SlabShapeVertex end)
        {
            var path = new List<XYZ>();
            var current = end;

            // Check if path exists
            if (!previous.ContainsKey(end) || previous[end] == null)
                return null;

            while (current != null)
            {
                path.Insert(0, current.Position);
                current = previous.ContainsKey(current) ? previous[current] : null;
            }

            return path.Count > 0 ? path : null;
        }

        // ... rest of the existing methods remain the same ...
        public Dictionary<SlabShapeVertex, (SlabShapeVertex drain, double distance, List<XYZ> path)>
            ComputePathsToDrains(Dictionary<SlabShapeVertex, List<SlabShapeVertex>> graph,
                               List<SlabShapeVertex> drainPoints)
        {
            var results = new Dictionary<SlabShapeVertex, (SlabShapeVertex, double, List<XYZ>)>();

            foreach (var vertex in graph.Keys)
            {
                if (vertex == null) continue;

                // If vertex is itself a drain point
                if (drainPoints.Contains(vertex))
                {
                    results[vertex] = (vertex, 0, new List<XYZ> { vertex.Position });
                    continue;
                }

                double minDistance = double.MaxValue;
                SlabShapeVertex nearestDrain = null;
                List<XYZ> shortestPath = null;

                foreach (var drain in drainPoints)
                {
                    if (drain == null) continue;

                    var path = DijkstraPath(vertex, drain, graph);
                    if (path != null && path.Count >= 2)
                    {
                        double pathLength = CalculatePathLength(path);

                        if (pathLength < minDistance)
                        {
                            minDistance = pathLength;
                            nearestDrain = drain;
                            shortestPath = path;
                        }
                    }
                }

                if (nearestDrain != null)
                {
                    results[vertex] = (nearestDrain, minDistance, shortestPath);
                }
            }

            return results;
        }

        private double CalculatePathLength(List<XYZ> path)
        {
            double length = 0;
            for (int i = 0; i < path.Count - 1; i++)
            {
                if (path[i] != null && path[i + 1] != null)
                {
                    length += path[i].DistanceTo(path[i + 1]);
                }
            }
            return length;
        }
    }
}