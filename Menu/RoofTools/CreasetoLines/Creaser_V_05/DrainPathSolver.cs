using Revit26_Plugin.Creaser_V05.Commands;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.Creaser_V05.Commands
{
    internal static class DrainPathSolver
    {
        // Dijkstra: shortest downhill path from start to ANY drain
        public static List<XYZKey> FindShortestPath(
            XYZKey start,
            HashSet<XYZKey> drains,
            Dictionary<XYZKey, List<XYZKey>> graph)
        {
            Dictionary<XYZKey, double> dist =
                new Dictionary<XYZKey, double>();
            Dictionary<XYZKey, XYZKey?> prev =
                new Dictionary<XYZKey, XYZKey?>();
            PriorityQueue<XYZKey, double> pq =
                new PriorityQueue<XYZKey, double>();

            foreach (XYZKey n in graph.Keys)
            {
                dist[n] = double.PositiveInfinity;
                prev[n] = null;
            }

            dist[start] = 0.0;
            pq.Enqueue(start, 0.0);

            while (pq.Count > 0)
            {
                XYZKey current = pq.Dequeue();

                // STOP as soon as the nearest drain is reached
                if (drains.Contains(current))
                    return Reconstruct(prev, current);

                if (!graph.ContainsKey(current))
                    continue;

                foreach (XYZKey next in graph[current])
                {
                    double alt = dist[current] + current.DistanceTo(next);

                    if (alt < dist[next])
                    {
                        dist[next] = alt;
                        prev[next] = current;
                        pq.Enqueue(next, alt);
                    }
                }
            }

            return new List<XYZKey>();
        }

        // Find path to specific drain (not just nearest)
        public static List<XYZKey> FindPathToSpecificDrain(
            XYZKey start,
            XYZKey drain,
            Dictionary<XYZKey, List<XYZKey>> graph)
        {
            if (!graph.ContainsKey(start) || !graph.ContainsKey(drain))
                return new List<XYZKey>();

            Dictionary<XYZKey, double> dist = new Dictionary<XYZKey, double>();
            Dictionary<XYZKey, XYZKey?> prev = new Dictionary<XYZKey, XYZKey?>();
            PriorityQueue<XYZKey, double> pq = new PriorityQueue<XYZKey, double>();

            foreach (XYZKey n in graph.Keys)
            {
                dist[n] = double.PositiveInfinity;
                prev[n] = null;
            }

            dist[start] = 0.0;
            pq.Enqueue(start, 0.0);

            while (pq.Count > 0)
            {
                XYZKey current = pq.Dequeue();

                if (current.Equals(drain))
                    return Reconstruct(prev, current);

                if (!graph.ContainsKey(current))
                    continue;

                foreach (XYZKey next in graph[current])
                {
                    double alt = dist[current] + current.DistanceTo(next);

                    if (alt < dist[next])
                    {
                        dist[next] = alt;
                        prev[next] = current;
                        pq.Enqueue(next, alt);
                    }
                }
            }

            return new List<XYZKey>();
        }

        // Safe reconstruction for struct-based nodes
        private static List<XYZKey> Reconstruct(
            Dictionary<XYZKey, XYZKey?> prev,
            XYZKey end)
        {
            List<XYZKey> path = new List<XYZKey>();
            XYZKey current = end;

            while (true)
            {
                path.Add(current);

                if (!prev.TryGetValue(current, out XYZKey? p) || p == null)
                    break;

                current = p.Value;
            }

            path.Reverse();
            return path;
        }
        // Add this method to DrainPathSolver class
        public static bool HasPathToAnyDrain(
            XYZKey start,
            HashSet<XYZKey> drains,
            Dictionary<XYZKey, List<XYZKey>> graph)
        {
            if (!graph.ContainsKey(start))
                return false;

            HashSet<XYZKey> visited = new HashSet<XYZKey>();
            Queue<XYZKey> queue = new Queue<XYZKey>();

            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                XYZKey current = queue.Dequeue();

                if (drains.Contains(current))
                    return true;

                if (graph.TryGetValue(current, out var neighbors))
                {
                    foreach (XYZKey neighbor in neighbors)
                    {
                        if (!visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }

            return false;
        }
        }
    }
