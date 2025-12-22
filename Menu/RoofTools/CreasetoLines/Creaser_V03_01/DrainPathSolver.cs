using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.Creaser_V03_01.Commands
{
    internal static class DrainPathSolver
    {
        // Simple priority queue implementation for Dijkstra's algorithm
        private class PriorityQueue<T>
        {
            private readonly SortedDictionary<double, Queue<T>> _sortedDict = new SortedDictionary<double, Queue<T>>();
            private int _count = 0;

            public int Count => _count;

            public void Enqueue(T item, double priority)
            {
                if (!_sortedDict.ContainsKey(priority))
                    _sortedDict[priority] = new Queue<T>();

                _sortedDict[priority].Enqueue(item);
                _count++;
            }

            public T Dequeue()
            {
                if (_count == 0)
                    throw new InvalidOperationException("Queue is empty");

                var firstKey = _sortedDict.Keys.First();
                var queue = _sortedDict[firstKey];
                var item = queue.Dequeue();

                if (queue.Count == 0)
                    _sortedDict.Remove(firstKey);

                _count--;
                return item;
            }

            public bool IsEmpty => _count == 0;
        }

        public static List<XYZKey> FindShortestPath(
            XYZKey start,
            HashSet<XYZKey> targetDrains,
            Dictionary<XYZKey, List<XYZKey>> graph)
        {
            // Early exit if start is already a target drain
            if (targetDrains.Contains(start))
                return new List<XYZKey> { start };

            // Early exit if start is not in the graph
            if (!graph.ContainsKey(start))
                return new List<XYZKey>();

            // Early exit if no target drains
            if (targetDrains.Count == 0)
                return new List<XYZKey>();

            // Dijkstra's algorithm for shortest path to any target drain
            Dictionary<XYZKey, double> distance = new Dictionary<XYZKey, double>();
            Dictionary<XYZKey, XYZKey> previous = new Dictionary<XYZKey, XYZKey>();
            HashSet<XYZKey> visited = new HashSet<XYZKey>();

            // Initialize distances for reachable nodes
            distance[start] = 0;

            // Priority queue for nodes to visit
            PriorityQueue<XYZKey> priorityQueue = new PriorityQueue<XYZKey>();
            priorityQueue.Enqueue(start, 0);

            XYZKey? foundTarget = null;

            while (!priorityQueue.IsEmpty)
            {
                XYZKey current = priorityQueue.Dequeue();

                // Skip if already visited
                if (visited.Contains(current))
                    continue;

                visited.Add(current);

                // If we reached a target drain, we found our path
                if (targetDrains.Contains(current))
                {
                    foundTarget = current;
                    break;
                }

                // If current has no neighbors, skip
                if (!graph.ContainsKey(current) || graph[current].Count == 0)
                    continue;

                // Explore neighbors
                foreach (XYZKey neighbor in graph[current])
                {
                    if (visited.Contains(neighbor))
                        continue;

                    // Calculate distance to neighbor (3D distance)
                    double edgeWeight = current.DistanceTo(neighbor);
                    double currentDist = distance.ContainsKey(current) ? distance[current] : double.PositiveInfinity;
                    double newDistance = currentDist + edgeWeight;

                    double existingDist = distance.ContainsKey(neighbor) ? distance[neighbor] : double.PositiveInfinity;

                    // Update if we found a shorter path
                    if (newDistance < existingDist)
                    {
                        distance[neighbor] = newDistance;
                        previous[neighbor] = current;
                        priorityQueue.Enqueue(neighbor, newDistance);
                    }
                }
            }

            // If no target was reached, return empty path
            if (foundTarget == null)
                return new List<XYZKey>();

            // Reconstruct the path from target back to start
            return ReconstructPath(previous, start, foundTarget.Value);
        }

        private static List<XYZKey> ReconstructPath(
            Dictionary<XYZKey, XYZKey> previous,
            XYZKey start,
            XYZKey target)
        {
            List<XYZKey> path = new List<XYZKey>();
            XYZKey current = target;

            // Work backwards from target to start
            while (!current.Equals(start))
            {
                path.Add(current);

                if (!previous.ContainsKey(current))
                {
                    // Path reconstruction failed
                    return new List<XYZKey>();
                }

                current = previous[current];
            }

            // Add the start node
            path.Add(start);

            // Reverse to get correct order (start -> target)
            path.Reverse();

            return path;
        }

        // Alternative BFS implementation (unweighted graph)
        public static List<XYZKey> FindShortestPathBFS(
            XYZKey start,
            HashSet<XYZKey> targetDrains,
            Dictionary<XYZKey, List<XYZKey>> graph)
        {
            if (targetDrains.Contains(start))
                return new List<XYZKey> { start };

            if (!graph.ContainsKey(start))
                return new List<XYZKey>();

            if (targetDrains.Count == 0)
                return new List<XYZKey>();

            // BFS implementation for unweighted graph
            Queue<XYZKey> queue = new Queue<XYZKey>();
            Dictionary<XYZKey, XYZKey> parent = new Dictionary<XYZKey, XYZKey>();
            HashSet<XYZKey> visited = new HashSet<XYZKey>();

            queue.Enqueue(start);
            visited.Add(start);
            parent[start] = start; // Mark start as its own parent

            XYZKey? foundTarget = null;

            while (queue.Count > 0)
            {
                XYZKey current = queue.Dequeue();

                // Found a target drain
                if (targetDrains.Contains(current))
                {
                    foundTarget = current;
                    break;
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

            // Reconstruct path if target was found
            if (foundTarget != null)
            {
                return ReconstructPath(parent, start, foundTarget.Value);
            }

            return new List<XYZKey>();
        }

        // Find multiple shortest paths from a start to different targets
        public static Dictionary<XYZKey, List<XYZKey>> FindMultipleShortestPaths(
            XYZKey start,
            HashSet<XYZKey> targetDrains,
            Dictionary<XYZKey, List<XYZKey>> graph)
        {
            Dictionary<XYZKey, List<XYZKey>> paths = new Dictionary<XYZKey, List<XYZKey>>();

            foreach (XYZKey target in targetDrains)
            {
                List<XYZKey> path = FindShortestPathToSpecificTarget(start, target, graph);
                if (path.Count > 0)
                {
                    paths[target] = path;
                }
            }

            return paths;
        }

        // Find shortest path to a specific target (not just any drain)
        private static List<XYZKey> FindShortestPathToSpecificTarget(
            XYZKey start,
            XYZKey target,
            Dictionary<XYZKey, List<XYZKey>> graph)
        {
            if (start.Equals(target))
                return new List<XYZKey> { start };

            if (!graph.ContainsKey(start) || !graph.ContainsKey(target))
                return new List<XYZKey>();

            HashSet<XYZKey> singleTarget = new HashSet<XYZKey> { target };
            return FindShortestPath(start, singleTarget, graph);
        }
    }
}