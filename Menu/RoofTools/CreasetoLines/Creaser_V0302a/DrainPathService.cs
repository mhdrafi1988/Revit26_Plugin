using Autodesk.Revit.DB;
using Revit26_Plugin.Creaser_V03_03.Helpers;
using Revit26_Plugin.Creaser_V03_03.Models;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.Creaser_V03_03.Services
{
    public static class DrainPathService
    {
        public static List<DrainPath> ComputeDrainPaths(
            Document doc,
            RoofBase roof,
            UiLogService log)
        {
            log.Log("DrainPathService START");

            SlabShapeEditor editor = roof.GetSlabShapeEditor();
            SlabShapeCreaseArray creases = editor.SlabShapeCreases;

            // ================== BUILD GRAPH ==================
            Dictionary<XYZKey, List<XYZKey>> graph = new();
            HashSet<XYZKey> allNodes = new();

            foreach (SlabShapeCrease crease in creases)
            {
                Curve c = crease.Curve;
                XYZKey a = new XYZKey(c.GetEndPoint(0));
                XYZKey b = new XYZKey(c.GetEndPoint(1));

                AddEdge(graph, a, b);
                AddEdge(graph, b, a);

                allNodes.Add(a);
                allNodes.Add(b);
            }

            log.Log($"Graph nodes: {allNodes.Count}");

            // ================== FIND DRAINS ==================
            double minZ = allNodes.Min(p => p.Z);
            HashSet<XYZKey> drainNodes =
                allNodes.Where(p => p.Z <= minZ + 0.001).ToHashSet();

            log.Log($"Drain nodes: {drainNodes.Count}");

            // ================== FIND CORNERS ==================
            HashSet<XYZKey> cornerNodes = new();

            foreach (var kv in graph)
            {
                if (kv.Value.Count == 1)
                {
                    cornerNodes.Add(kv.Key);
                }
            }

            log.Log($"Corner nodes: {cornerNodes.Count}");

            // ================== BFS PER CORNER ==================
            List<DrainPath> results = new();

            foreach (XYZKey corner in cornerNodes)
            {
                List<XYZKey> path =
                    FindShortestPathBFS(corner, drainNodes, graph);

                if (path.Count < 2)
                {
                    log.Log($"No path found for corner {corner}");
                    continue;
                }

                List<Line> lines = ConvertToOrderedLines(path, log);

                if (lines.Count == 0)
                    continue;

                results.Add(new DrainPath(
                    path.First().ToXYZ(),
                    path.Last().ToXYZ(),
                    lines));

                log.Log($"Path created with {lines.Count} segments");
            }

            log.Log($"Total paths created: {results.Count}");
            return results;
        }

        // ================== BFS ==================
        private static List<XYZKey> FindShortestPathBFS(
            XYZKey start,
            HashSet<XYZKey> targets,
            Dictionary<XYZKey, List<XYZKey>> graph)
        {
            Queue<XYZKey> q = new();
            Dictionary<XYZKey, XYZKey> parent = new();
            HashSet<XYZKey> visited = new();

            q.Enqueue(start);
            visited.Add(start);
            parent[start] = start;

            while (q.Count > 0)
            {
                XYZKey current = q.Dequeue();

                if (targets.Contains(current))
                    return ReconstructPath(parent, start, current);

                foreach (var n in graph[current])
                {
                    if (!visited.Contains(n))
                    {
                        visited.Add(n);
                        parent[n] = current;
                        q.Enqueue(n);
                    }
                }
            }

            return new();
        }

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

        // ================== HELPERS ==================
        private static void AddEdge(
            Dictionary<XYZKey, List<XYZKey>> g,
            XYZKey a,
            XYZKey b)
        {
            if (!g.ContainsKey(a))
                g[a] = new List<XYZKey>();

            if (!g[a].Contains(b))
                g[a].Add(b);
        }

        private static List<Line> ConvertToOrderedLines(
            List<XYZKey> path,
            UiLogService log)
        {
            List<Line> lines = new();

            for (int i = 0; i < path.Count - 1; i++)
            {
                XYZ p1 = path[i].ToXYZ();
                XYZ p2 = path[i + 1].ToXYZ();

                // Ensure HIGH → LOW
                XYZ high = p1.Z >= p2.Z ? p1 : p2;
                XYZ low = p1.Z < p2.Z ? p1 : p2;

                if (high.DistanceTo(low) <
                    1e-6)
                {
                    continue;
                }

                lines.Add(Line.CreateBound(high, low));
            }

            return lines;
        }
    }
}
