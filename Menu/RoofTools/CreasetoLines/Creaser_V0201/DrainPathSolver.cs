using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.Creaser_V02010.Commands
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
            HashSet<XYZKey> drains,
            Dictionary<XYZKey, List<XYZKey>> graph)
        {
            // Early exit if start is already a drain
            if (drains.Contains(start))
                return new List<XYZKey> { start };

            // Early exit if start is not in the graph
            if (!graph.ContainsKey(start))
                return new List<XYZKey>();

            // Dijkstra's algorithm for shortest path
            Dictionary<XYZKey, double> distance = new Dictionary<XYZKey, double>();
            Dictionary<XYZKey, XYZKey> previous = new Dictionary<XYZKey, XYZKey>();
            HashSet<XYZKey> visited = new HashSet<XYZKey>();

            // Initialize distances
            foreach (var node in graph.Keys)
            {
                distance[node] = double.PositiveInfinity;
            }
            distance[start] = 0;

            // Priority queue for nodes to visit
            PriorityQueue<XYZKey> priorityQueue = new PriorityQueue<XYZKey>();
            priorityQueue.Enqueue(start, 0);

            XYZKey? targetDrain = null;

            while (!priorityQueue.IsEmpty)
            {
                XYZKey current = priorityQueue.Dequeue();

                // Skip if already visited
                if (visited.Contains(current))
                    continue;

                visited.Add(current);

                // If we reached a drain, we found our target
                if (drains.Contains(current))
                {
                    targetDrain = current;
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

                    // Calculate distance to neighbor
                    double edgeWeight = current.DistanceTo(neighbor);
                    double newDistance = distance[current] + edgeWeight;

                    // Update if we found a shorter path
                    if (newDistance < distance[neighbor])
                    {
                        distance[neighbor] = newDistance;
                        previous[neighbor] = current;
                        priorityQueue.Enqueue(neighbor, newDistance);
                    }
                }
            }

            // If no drain was reached, return empty path
            if (targetDrain == null)
                return new List<XYZKey>();

            // Reconstruct the path from target back to start
            return ReconstructPath(previous, start, targetDrain.Value);
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

        // Alternative implementation using BFS (if all edges have equal weight)
        public static List<XYZKey> FindShortestPathBFS(
            XYZKey start,
            HashSet<XYZKey> drains,
            Dictionary<XYZKey, List<XYZKey>> graph)
        {
            if (drains.Contains(start))
                return new List<XYZKey> { start };

            if (!graph.ContainsKey(start))
                return new List<XYZKey>();

            // BFS implementation
            Queue<XYZKey> queue = new Queue<XYZKey>();
            Dictionary<XYZKey, XYZKey> parent = new Dictionary<XYZKey, XYZKey>();
            HashSet<XYZKey> visited = new HashSet<XYZKey>();

            queue.Enqueue(start);
            visited.Add(start);
            parent[start] = start; // Mark start as its own parent

            XYZKey? target = null;

            while (queue.Count > 0)
            {
                XYZKey current = queue.Dequeue();

                // Found a drain
                if (drains.Contains(current))
                {
                    target = current;
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
            if (target != null)
            {
                return ReconstructPath(parent, start, target.Value);
            }

            return new List<XYZKey>();
        }
    }
}