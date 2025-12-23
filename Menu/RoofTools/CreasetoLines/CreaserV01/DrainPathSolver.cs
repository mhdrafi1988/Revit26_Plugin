using System;
using System.Collections.Generic;

namespace Revit26_Plugin.Creaser_V01.Commands
{
    internal static class DrainPathSolver
    {
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

            dist[start] = 0;
            pq.Enqueue(start, 0);

            while (pq.Count > 0)
            {
                XYZKey current = pq.Dequeue();

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

        private static List<XYZKey> Reconstruct(
            Dictionary<XYZKey, XYZKey?> prev,
            XYZKey end)
        {
            List<XYZKey> path = new List<XYZKey>();
            XYZKey? cur = end;

            while (cur != null)
            {
                path.Add(cur.Value);
                cur = prev[cur.Value];
            }

            path.Reverse();
            return path;
        }
    }
}
