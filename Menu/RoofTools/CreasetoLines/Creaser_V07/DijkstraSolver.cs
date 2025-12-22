// ============================================================
// File: DijkstraSolver.cs
// Namespace: Revit26_Plugin.Creaser_V07.Commands
// ============================================================

using System.Collections.Generic;

namespace Revit26_Plugin.Creaser_V07.Commands
{
    internal static class DijkstraSolver
    {
        /// <summary>
        /// Dijkstra shortest path from start to target on an undirected graph.
        /// Cost = 3D distance + gravity bias (uphill penalized, downhill slightly rewarded).
        /// This encourages realistic downhill routing while remaining robust on flat/curved geometry.
        /// </summary>
        public static List<XYZKey> FindShortestPath(
            XYZKey start,
            XYZKey target,
            Dictionary<XYZKey, List<XYZKey>> graph)
        {
            if (start.Equals(target))
                return new List<XYZKey> { start };

            Dictionary<XYZKey, double> dist = new();
            Dictionary<XYZKey, XYZKey?> prev = new();
            PriorityQueue<XYZKey, double> pq = new();

            // Initialize only nodes that appear as keys (adjacency list roots)
            foreach (var n in graph.Keys)
            {
                dist[n] = double.PositiveInfinity;
                prev[n] = null;
            }

            // Ensure start/target exist
            if (!dist.ContainsKey(start))
            {
                dist[start] = 0;
                prev[start] = null;
            }

            if (!dist.ContainsKey(target))
            {
                // if target isn't a root key, it might still be reachable as neighbor,
                // but safest is to allow it.
                dist[target] = double.PositiveInfinity;
                prev[target] = null;
            }

            dist[start] = 0;
            pq.Enqueue(start, 0);

            HashSet<XYZKey> visited = new();

            while (pq.Count > 0)
            {
                XYZKey c = pq.Dequeue();
                if (!visited.Add(c))
                    continue;

                if (c.Equals(target))
                    return Reconstruct(prev, target);

                if (!graph.TryGetValue(c, out var nbrs))
                    continue;

                foreach (var x in nbrs)
                {
                    // Gravity bias:
                    // Uphill: penalize strongly.
                    // Downhill: tiny reward.
                    double dz = x.Z - c.Z;
                    double gravityPenalty = dz > 0 ? dz * 10.0 : dz * 0.1;

                    double alt = dist[c] + c.DistanceTo(x) + gravityPenalty;

                    if (!dist.TryGetValue(x, out double existing))
                    {
                        dist[x] = alt;
                        prev[x] = c;
                        pq.Enqueue(x, alt);
                        continue;
                    }

                    if (alt < existing)
                    {
                        dist[x] = alt;
                        prev[x] = c;
                        pq.Enqueue(x, alt);
                    }
                }
            }

            return new List<XYZKey>();
        }

        private static List<XYZKey> Reconstruct(Dictionary<XYZKey, XYZKey?> prev, XYZKey end)
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
