// ============================================================
// File: DrainPathSolver.cs
// ============================================================

using System.Collections.Generic;

namespace Revit26_Plugin.Creaser_V07.Commands
{
    internal static class DrainPathSolver
    {
        public static bool HasPathToAnyDrain(
            XYZKey start,
            HashSet<XYZKey> drains,
            Dictionary<XYZKey, List<XYZKey>> graph)
        {
            Queue<XYZKey> q = new();
            HashSet<XYZKey> visited = new();

            q.Enqueue(start);
            visited.Add(start);

            while (q.Count > 0)
            {
                XYZKey c = q.Dequeue();
                if (drains.Contains(c))
                    return true;

                if (!graph.TryGetValue(c, out var n))
                    continue;

                foreach (var x in n)
                    if (visited.Add(x))
                        q.Enqueue(x);
            }

            return false;
        }

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
                XYZKey c = pq.Dequeue();

                if (drains.Contains(c))
                    return Reconstruct(prev, c);

                if (!graph.TryGetValue(c, out var n))
                    continue;

                foreach (var x in n)
                {
                    double dz = x.Z - c.Z;

                    // Gravity bias:
                    // downhill rewarded, uphill penalized
                    double gravityPenalty = dz > 0 ? dz * 10.0 : dz;

                    double alt =
                        dist[c] +
                        c.DistanceTo(x) +
                        gravityPenalty;

                    if (alt < dist[x])
                    {
                        dist[x] = alt;
                        prev[x] = c;
                        pq.Enqueue(x, alt);
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
            XYZKey cur = end;

            while (true)
            {
                path.Add(cur);
                if (!prev.TryGetValue(cur, out var p) || p == null)
                    break;

                cur = p.Value;
            }

            path.Reverse();
            return path;
        }
    }
}
