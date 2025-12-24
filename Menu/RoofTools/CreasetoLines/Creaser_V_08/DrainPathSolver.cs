using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.Creaser_V08.Commands.Services
{
    /// <summary>
    /// Dijkstra-based shortest path solver using XYZ nodes.
    /// </summary>
    internal static class DrainPathSolver
    {
        public static List<XYZ> FindShortestPath(
            XYZ start,
            IList<XYZ> drains,
            IDictionary<XYZ, List<XYZ>> graph)
        {
            Dictionary<XYZ, double> dist = new();
            Dictionary<XYZ, XYZ> prev = new();
            PriorityQueue<XYZ, double> pq = new();

            foreach (XYZ node in graph.Keys)
            {
                dist[node] = double.PositiveInfinity;
                prev[node] = null;
            }

            dist[start] = 0.0;
            pq.Enqueue(start, 0.0);

            while (pq.Count > 0)
            {
                XYZ current = pq.Dequeue();

                // Stop when reaching any drain
                foreach (XYZ drain in drains)
                {
                    if (current.IsAlmostEqualTo(drain))
                        return Reconstruct(prev, current);
                }

                foreach (XYZ next in graph[current])
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

            return new List<XYZ>();
        }

        private static List<XYZ> Reconstruct(
            Dictionary<XYZ, XYZ> prev,
            XYZ end)
        {
            List<XYZ> path = new();
            XYZ current = end;

            while (current != null)
            {
                path.Add(current);
                prev.TryGetValue(current, out current);
            }

            path.Reverse();
            return path;
        }
    }
}
