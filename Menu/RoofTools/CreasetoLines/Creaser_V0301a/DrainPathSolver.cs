using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.Creaser_V03_01.Commands
{
    internal static class DrainPathSolver
    {
        private class PriorityQueue<T>
        {
            private readonly SortedDictionary<double, Queue<T>> _dict = new();
            private int _count;

            public void Enqueue(T item, double priority)
            {
                if (!_dict.ContainsKey(priority))
                    _dict[priority] = new Queue<T>();

                _dict[priority].Enqueue(item);
                _count++;
            }

            public T Dequeue()
            {
                var first = _dict.First();
                var item = first.Value.Dequeue();
                if (first.Value.Count == 0)
                    _dict.Remove(first.Key);
                _count--;
                return item;
            }

            public bool IsEmpty => _count == 0;
        }

        public static List<XYZKey> FindShortestPath(
            XYZKey start,
            HashSet<XYZKey> targets,
            Dictionary<XYZKey, List<XYZKey>> graph)
        {
            Dictionary<XYZKey, double> dist = new();
            Dictionary<XYZKey, XYZKey> prev = new();
            HashSet<XYZKey> visited = new();

            PriorityQueue<XYZKey> pq = new();
            dist[start] = 0;
            pq.Enqueue(start, 0);

            XYZKey? found = null;

            while (!pq.IsEmpty)
            {
                XYZKey current = pq.Dequeue();
                if (visited.Contains(current)) continue;
                visited.Add(current);

                if (targets.Contains(current))
                {
                    found = current;
                    break;
                }

                if (!graph.ContainsKey(current)) continue;

                foreach (XYZKey n in graph[current])
                {
                    if (visited.Contains(n)) continue;

                    // 🔥 FIX: XY COST ONLY
                    double cost = current.DistanceTo2D(n);
                    double nd = dist[current] + cost;

                    if (!dist.ContainsKey(n) || nd < dist[n])
                    {
                        dist[n] = nd;
                        prev[n] = current;
                        pq.Enqueue(n, nd);
                    }
                }
            }

            if (found == null) return new List<XYZKey>();

            List<XYZKey> path = new();
            XYZKey cur = found.Value;
            while (!cur.Equals(start))
            {
                path.Add(cur);
                cur = prev[cur];
            }
            path.Add(start);
            path.Reverse();
            return path;
        }
    }
}
