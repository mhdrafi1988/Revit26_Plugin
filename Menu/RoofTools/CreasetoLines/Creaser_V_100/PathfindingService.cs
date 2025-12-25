using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.Creaser_V100.Models;

namespace Revit26_Plugin.Creaser_V100.Services
{
    public class PathfindingService
    {
        private readonly ILogService _log;

        public PathfindingService(ILogService log)
        {
            _log = log;
        }

        public IList<PathResult> Solve(
            IReadOnlyList<GraphNode> nodes,
            IReadOnlyList<GraphEdge> edges,
            IDictionary<XYZ, int> cornerNodeMap,
            IDictionary<XYZ, int> drainNodeMap)
        {
            using (_log.Scope(nameof(PathfindingService), "Solve"))
            {
                var adj = edges
                    .GroupBy(e => e.From)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var dist = nodes.ToDictionary(n => n.Id, _ => double.MaxValue);
                var prev = new Dictionary<int, int>();
                var nearestDrain = new Dictionary<int, int>();

                var queue = new SortedSet<(double d, int id)>();

                // Multi-source init
                foreach (var pair in drainNodeMap)
                {
                    dist[pair.Value] = 0;
                    nearestDrain[pair.Value] = pair.Value;
                    queue.Add((0, pair.Value));
                }

                while (queue.Any())
                {
                    var (d, u) = queue.First();
                    queue.Remove(queue.First());

                    if (!adj.ContainsKey(u)) continue;

                    foreach (GraphEdge edge in adj[u])
                    {
                        double alt = d + edge.Weight;
                        if (alt < dist[edge.To])
                        {
                            queue.Remove((dist[edge.To], edge.To));

                            dist[edge.To] = alt;
                            prev[edge.To] = u;
                            nearestDrain[edge.To] = nearestDrain[u];

                            queue.Add((alt, edge.To));
                        }
                    }
                }

                var results = new List<PathResult>();

                foreach (var corner in cornerNodeMap)
                {
                    int nodeId = corner.Value;
                    if (!nearestDrain.ContainsKey(nodeId))
                    {
                        _log.Warning(nameof(PathfindingService),
                            $"No path for corner {corner.Key}");
                        continue;
                    }

                    var path = new List<XYZ>();
                    int current = nodeId;

                    while (prev.ContainsKey(current))
                    {
                        path.Add(nodes[current].Point);
                        current = prev[current];
                    }

                    path.Add(nodes[current].Point);

                    path.Reverse();

                    results.Add(new PathResult(
                        corner.Key,
                        nodes[nearestDrain[nodeId]].Point,
                        path,
                        dist[nodeId]));

                    _log.Info(nameof(PathfindingService),
                        $"Path solved: Corner → Drain, Length={dist[nodeId]:F3}");
                }

                return results;
            }
        }
    }
}
