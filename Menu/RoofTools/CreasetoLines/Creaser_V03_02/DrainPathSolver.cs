using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.Creaser_V03_02.Commands
{
    internal static class DrainPathSolver
    {
        public static List<XYZKey> FindShortestPathBFS(
            XYZKey start,
            HashSet<XYZKey> targets,
            Dictionary<XYZKey, List<XYZKey>> graph)
        {
            // Early exits
            if (targets.Contains(start))
                return new List<XYZKey> { start };

            if (!graph.ContainsKey(start))
                return new List<XYZKey>();

            if (targets.Count == 0)
                return new List<XYZKey>();

            // Simple BFS (Breadth-First Search)
            Queue<XYZKey> queue = new Queue<XYZKey>();
            Dictionary<XYZKey, XYZKey> parent = new Dictionary<XYZKey, XYZKey>();
            HashSet<XYZKey> visited = new HashSet<XYZKey>();

            queue.Enqueue(start);
            visited.Add(start);
            parent[start] = start;

            while (queue.Count > 0)
            {
                XYZKey current = queue.Dequeue();

                // Found target
                if (targets.Contains(current))
                {
                    return ReconstructPath(parent, start, current);
                }

                // Explore neighbors
                if (graph.ContainsKey(current))
                {
                    foreach (XYZKey neighbor in graph[current])
                    {
                        if (!visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            parent[neighbor] = current;
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }

            return new List<XYZKey>(); // No path found
        }

        private static List<XYZKey> ReconstructPath(
            Dictionary<XYZKey, XYZKey> parent,
            XYZKey start,
            XYZKey end)
        {
            List<XYZKey> path = new List<XYZKey>();
            XYZKey current = end;

            // Trace back from end to start
            while (!current.Equals(start))
            {
                path.Add(current);

                if (!parent.ContainsKey(current))
                {
                    return new List<XYZKey>(); // Broken path
                }

                current = parent[current];
            }

            path.Add(start);
            path.Reverse();
            return path;
        }

        // Dijkstra's algorithm for weighted paths (optional)
        public static List<XYZKey> FindShortestPathDijkstra(
            XYZKey start,
            HashSet<XYZKey> targets,
            Dictionary<XYZKey, List<XYZKey>> graph)
        {
            if (targets.Contains(start))
                return new List<XYZKey> { start };

            if (!graph.ContainsKey(start))
                return new List<XYZKey>();

            // Priority queue implementation
            var priorityQueue = new SortedDictionary<double, Queue<XYZKey>>();
            Dictionary<XYZKey, double> distance = new Dictionary<XYZKey, double>();
            Dictionary<XYZKey, XYZKey> previous = new Dictionary<XYZKey, XYZKey>();
            HashSet<XYZKey> visited = new HashSet<XYZKey>();

            // Initialize
            foreach (var node in graph.Keys)
            {
                distance[node] = double.MaxValue;
            }
            distance[start] = 0;

            Enqueue(priorityQueue, start, 0);

            while (priorityQueue.Count > 0)
            {
                var (current, currentDist) = Dequeue(priorityQueue);

                if (visited.Contains(current))
                    continue;

                visited.Add(current);

                if (targets.Contains(current))
                {
                    return ReconstructPath(previous, start, current);
                }

                if (graph.ContainsKey(current))
                {
                    foreach (var neighbor in graph[current])
                    {
                        if (!visited.Contains(neighbor))
                        {
                            double edgeWeight = current.DistanceTo(neighbor);
                            double newDist = currentDist + edgeWeight;

                            if (newDist < distance[neighbor])
                            {
                                distance[neighbor] = newDist;
                                previous[neighbor] = current;
                                Enqueue(priorityQueue, neighbor, newDist);
                            }
                        }
                    }
                }
            }

            return new List<XYZKey>();
        }

        // Helper methods for priority queue
        private static void Enqueue(SortedDictionary<double, Queue<XYZKey>> queue, XYZKey item, double priority)
        {
            if (!queue.ContainsKey(priority))
                queue[priority] = new Queue<XYZKey>();

            queue[priority].Enqueue(item);
        }

        private static (XYZKey, double) Dequeue(SortedDictionary<double, Queue<XYZKey>> queue)
        {
            var firstKey = queue.Keys.First();
            var itemQueue = queue[firstKey];
            var item = itemQueue.Dequeue();

            if (itemQueue.Count == 0)
                queue.Remove(firstKey);

            return (item, firstKey);
        }
    }
}