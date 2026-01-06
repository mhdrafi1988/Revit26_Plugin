using Autodesk.Revit.DB;
using Revit26_Plugin.CreaserAdv_V002.Models;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    public class WeightedShortestPathService
    {
        public PathResult FindShortestPath(
            XYZ start,
            IList<XYZ> targets,
            Dictionary<XYZ, List<XYZ>> graph,
            ISet<FlattenedEdge2D> creaseEdges)
        {
            var comparer = new Point2DComparer();

            var dist = new Dictionary<XYZ, double>(comparer);
            var prev = new Dictionary<XYZ, XYZ>(comparer);
            var visited = new HashSet<XYZ>(comparer);

            foreach (var n in graph.Keys)
                dist[n] = double.PositiveInfinity;

            if (!dist.ContainsKey(start))
                return null;

            dist[start] = 0;

            while (visited.Count < graph.Count)
            {
                XYZ u =
                    dist
                        .Where(k => !visited.Contains(k.Key))
                        .OrderBy(k => k.Value)
                        .Select(k => k.Key)
                        .FirstOrDefault();

                if (u == null)
                    break;

                visited.Add(u);

                if (targets.Any(t => comparer.Equals(t, u)))
                    return BuildResult(start, u, prev, dist[u]);

                foreach (var v in graph[u])
                {
                    if (visited.Contains(v))
                        continue;

                    double weight =
                        IsCrease(u, v, creaseEdges)
                            ? 1.0
                            : 5.0;

                    double alt = dist[u] + u.DistanceTo(v) * weight;

                    if (alt < dist[v])
                    {
                        dist[v] = alt;
                        prev[v] = u;
                    }
                }
            }

            return null;
        }

        private static bool IsCrease(
            XYZ a,
            XYZ b,
            ISet<FlattenedEdge2D> creases)
        {
            foreach (var e in creases)
            {
                if ((Same(a, e.Start2D) && Same(b, e.End2D)) ||
                    (Same(a, e.End2D) && Same(b, e.Start2D)))
                    return true;
            }

            return false;
        }

        private static bool Same(XYZ a, XYZ b)
        {
            return a.DistanceTo(b) < GeometryTolerance.Point;
        }

        private static PathResult BuildResult(
            XYZ start,
            XYZ end,
            Dictionary<XYZ, XYZ> prev,
            double length)
        {
            var nodes = new List<XYZ> { end };
            var current = end;

            while (prev.ContainsKey(current))
            {
                current = prev[current];
                nodes.Add(current);
            }

            nodes.Reverse();

            return new PathResult
            {
                Start = start,
                End = end,
                OrderedNodes = nodes,
                Length = length
            };
        }
    }
}
