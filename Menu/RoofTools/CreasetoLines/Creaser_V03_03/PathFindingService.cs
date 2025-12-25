using Revit26_Plugin.Creaser_V03_03.Models;
using System.Collections.Generic;

namespace Revit26_Plugin.Creaser_V03_03.Services
{
    /// <summary>
    /// Fast, bounded BFS path finder (Revit-safe)
    /// </summary>
    public static class PathFindingService
    {
        public static List<XYZKey> FindPathBFS(
            XYZKey start,
            HashSet<XYZKey> targets,
            Dictionary<XYZKey, List<XYZKey>> graph)
        {
            Queue<XYZKey> queue = new();
            Dictionary<XYZKey, XYZKey> parent = new();
            HashSet<XYZKey> visited = new();

            queue.Enqueue(start);
            visited.Add(start);
            parent[start] = start;

            while (queue.Count > 0)
            {
                XYZKey current = queue.Dequeue();

                // Stop at first drain found
                if (targets.Contains(current))
                    return ReconstructPath(parent, start, current);

                if (!graph.ContainsKey(current))
                    continue;

                foreach (XYZKey neighbor in graph[current])
                {
                    if (visited.Add(neighbor))
                    {
                        parent[neighbor] = current;
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return new List<XYZKey>();
        }

        // 🔒 MUST be a CLASS-LEVEL method (not nested)
        private static List<XYZKey> ReconstructPath(
            Dictionary<XYZKey, XYZKey> parent,
            XYZKey start,
            XYZKey end)
        {
            List<XYZKey> path = new();
            XYZKey cur = end;

            while (!cur.Equals(start))
            {
                path.Add(cur);
                cur = parent[cur];
            }

            path.Add(start);
            path.Reverse();
            return path;
        }
    }
}
