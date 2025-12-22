// ============================================================
// File: DrainPathSolver.cs
// Namespace: Revit26_Plugin.Creaser_V06.Commands
// ============================================================

using System.Collections.Generic;

namespace Revit26_Plugin.Creaser_V06.Commands
{
    internal static class DrainPathSolver
    {
        public static List<XYZKey> FindShortestPath(
            XYZKey start,
            HashSet<XYZKey> drains,
            Dictionary<XYZKey, List<XYZKey>> graph)
        {
            Dictionary<XYZKey, double> dist = new();
            Dictionary<XYZKey, XYZKey?> prev = new();
            PriorityQueue<XYZKey, double> pq = new();

            foreach (var n in graph.Keys)
            {
                dist[n] = double.PositiveInfinity;
                prev[n] = null;
            }

            dist[start] = 0;
            pq.Enqueue(start, 0);

            while (pq.Count > 0)
            {
                XYZKey current = pq.Dequeue();

                if (drains.Contains(current))
                    return Reconstruct(prev, current);

                if (!graph.TryGetValue(current, out var neighbors))
                    continue;

                foreach (XYZKey next in neighbors)
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

        private static List<XYZKey> Reconstruct(
            Dictionary<XYZKey, XYZKey?> prev,
            XYZKey end)
        {
            List<XYZKey> path = new();
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
