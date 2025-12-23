using System;
using System.Collections.Generic;

// Ensure the namespace or assembly containing XYZKey is included
//using Revit26_Plugin.Models; // Updated to the correct namespace for XYZKey

namespace Revit26_Plugin.Creaser_V03.Commands
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

                foreach (XYZKey next in graph[current])
                {
                    double alt =
                        dist[current] + current.DistanceTo(next);

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
    }
}
