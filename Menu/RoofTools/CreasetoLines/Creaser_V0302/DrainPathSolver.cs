using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.Creaser_V03_02.Commands
{
    internal static class DrainPathSolver
    {
        public static List<XYZKey> FindShortestPathBFS(
            XYZKey start,
            HashSet<XYZKey> targets,
            Dictionary<XYZKey, List<XYZKey>> graph)
        {
            // Early exits
            if (targets.Contains(start))
                return new List<XYZKey> { start };

            if (!graph.ContainsKey(start))
                return new List<XYZKey>();

            if (targets.Count == 0)
                return new List<XYZKey>();

            // Simple BFS
            Queue<XYZKey> queue = new Queue<XYZKey>();
            Dictionary<XYZKey, XYZKey> parent = new Dictionary<XYZKey, XYZKey>();
            HashSet<XYZKey> visited = new HashSet<XYZKey>();

            queue.Enqueue(start);
            visited.Add(start);
            parent[start] = start;

            while (queue.Count > 0)
            {
                XYZKey current = queue.Dequeue();

                // Found target
                if (targets.Contains(current))
                {
                    return ReconstructPath(parent, start, current);
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

            return new List<XYZKey>();
        }

        private static List<XYZKey> ReconstructPath(
            Dictionary<XYZKey, XYZKey> parent,
            XYZKey start,
            XYZKey end)
        {
            List<XYZKey> path = new List<XYZKey>();
            XYZKey current = end;

            while (!current.Equals(start))
            {
                path.Add(current);

                if (!parent.ContainsKey(current))
                    return new List<XYZKey>();

                current = parent[current];
            }

            path.Add(start);
            path.Reverse();
            return path;
        }
    }
}