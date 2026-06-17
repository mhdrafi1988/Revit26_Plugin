using System.Collections.Generic;
using System.Linq;
using Revit26_Plugin.Creaser_adv_V001.Models;

namespace Revit26_Plugin.Creaser_adv_V001.Services
{
    /// <summary>
    /// Full Dijkstra traversal. Predictable and robust.
    /// Uses drainNodes set instead of relying on GraphNode flags.
    /// </summary>
    public class DijkstraPathFindingStrategy : IPathFindingStrategy
    {
        public PathResult FindPath(RoofGraph graph, GraphNode startNode)
        {
            // Use the graph's DrainNodes property for default interface method
            return FindPath(graph, startNode, new HashSet<GraphNode>(graph.DrainNodes));
        }

        public PathResult FindPath(RoofGraph graph, GraphNode startNode, HashSet<GraphNode> drainNodes)
        {
            if (drainNodes == null || drainNodes.Count == 0)
            {
                return new PathResult
                {
                    PathFound = false,
                    StartNode = startNode,
                    FailureReason = "No drain nodes provided."
                };
            }

            // Corner must not be treated as a drain
            if (drainNodes.Contains(startNode))
            {
                return new PathResult
                {
                    PathFound = false,
                    StartNode = startNode,
                    FailureReason = "Start node is also classified as a drain (invalid)."
                };
            }

            var distances = graph.Nodes.ToDictionary(n => n, _ => double.MaxValue);
            var previous = new Dictionary<GraphNode, GraphNode>();
            var unvisited = new HashSet<GraphNode>(graph.Nodes);

            distances[startNode] = 0;

            while (unvisited.Count > 0)
            {
                // Pick node with smallest tentative distance
                GraphNode current = unvisited.OrderBy(n => distances[n]).First();
                unvisited.Remove(current);

                // Early exit ONLY when we pop a drain (Dijkstra correctness)
                if (drainNodes.Contains(current) && current != startNode)
                {
                    return BuildResult(startNode, current, previous);
                }

                foreach (GraphNode neighbor in current.Neighbors)
                {
                    if (!unvisited.Contains(neighbor))
                        continue;

                    // Base distance
                    double cost = current.DistanceTo(neighbor);

                    // Optional uphill penalty (keeps solutions realistic but still reachable)
                    double dz = neighbor.Z - current.Z;
                    if (dz > 0)
                        cost += dz * 100.0;

                    double alt = distances[current] + cost;

                    if (alt < distances[neighbor])
                    {
                        distances[neighbor] = alt;
                        previous[neighbor] = current;
                    }
                }
            }

            return new PathResult
            {
                PathFound = false,
                StartNode = startNode,
                FailureReason = "No reachable drain found."
            };
        }

        private static PathResult BuildResult(
            GraphNode start,
            GraphNode end,
            Dictionary<GraphNode, GraphNode> previous)
        {
            var path = new List<GraphNode>();
            GraphNode current = end;

            while (true)
            {
                path.Add(current);

                if (current == start)
                    break;

                if (!previous.TryGetValue(current, out current))
                {
                    return new PathResult
                    {
                        PathFound = false,
                        StartNode = start,
                        FailureReason = "Path reconstruction failed."
                    };
                }
            }

            path.Reverse();

            if (path.Count < 2)
            {
                return new PathResult
                {
                    PathFound = false,
                    StartNode = start,
                    FailureReason = "Zero-length path."
                };
            }

            return new PathResult
            {
                PathFound = true,
                StartNode = start,
                EndNode = end,
                OrderedNodes = path
            };
        }
    }
}
