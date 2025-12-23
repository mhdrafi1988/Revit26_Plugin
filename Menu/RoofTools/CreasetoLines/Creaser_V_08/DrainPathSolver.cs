using System.Collections.Generic;
using Revit26_Plugin.Creaser_V08.Commands.Models;

namespace Revit26_Plugin.Creaser_V08.Commands
{
    internal static class DrainPathSolver
    {
        public static List<XYZKey> FindShortestPath(
            XYZKey start,
            HashSet<XYZKey> drains,
            Dictionary<XYZKey, List<XYZKey>> graph)
        {
            Dictionary<XYZKey, double> dist = new();
            Dictionary<XYZKey, XYZKey> prev = new();

            PriorityQueue<XYZKey, double> pq = new();

            foreach (XYZKey n in graph.Keys)
                dist[n] = double.PositiveInfinity;

            dist[start] = 0;
            pq.Enqueue(start, 0);

            while (pq.Count > 0)
            {
                XYZKey current = pq.Dequeue();

                if (drains.Contains(current))
                    return Reconstruct(prev, current);

                foreach (XYZKey next in graph[current])
                {
                    double alt = dist[current] + current.DistanceTo(next);

                    if (!dist.ContainsKey(next) || alt < dist[next])
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
            Dictionary<XYZKey, XYZKey> prev,
            XYZKey end)
        {
            List<XYZKey> path = new();
            XYZKey cur = end;

            while (prev.ContainsKey(cur))
            {
                path.Add(cur);
                cur = prev[cur];
            }

            path.Add(cur);
            path.Reverse();
            return path;
        }
    }
}
