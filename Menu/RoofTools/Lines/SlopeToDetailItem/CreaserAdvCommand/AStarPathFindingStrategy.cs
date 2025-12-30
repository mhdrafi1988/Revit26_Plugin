using System.Collections.Generic;
using System.Linq;
using Revit26_Plugin.Creaser_adv_V001.Models;

namespace Revit26_Plugin.Creaser_adv_V001.Services
{
    /// <summary>
    /// A* search toward the nearest drain by heuristic = 2D distance to closest drain.
    /// Uses drainNodes set (no reliance on flags).
    /// </summary>
    public class AStarPathFindingStrategy : IPathFindingStrategy
    {
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

            if (drainNodes.Contains(startNode))
            {
                return new PathResult
                {
                    PathFound = false,
                    StartNode = startNode,
                    FailureReason = "Start node is also classified as a drain (invalid)."
                };
            }

            var open = new HashSet<GraphNode> { startNode };
            var cameFrom = new Dictionary<GraphNode, GraphNode>();

            var gScore = graph.Nodes.ToDictionary(n => n, _ => double.MaxValue);
            var fScore = graph.Nodes.ToDictionary(n => n, _ => double.MaxValue);

            gScore[startNode] = 0;
            fScore[startNode] = HeuristicToClosestDrain(startNode, drainNodes);

            while (open.Count > 0)
            {
                GraphNode current = open.OrderBy(n => fScore[n]).First();

                if (drainNodes.Contains(current))
                {
                    return BuildResult(startNode, current, cameFrom);
                }

                open.Remove(current);

                foreach (GraphNode neighbor in current.Neighbors)
                {
                    double cost = current.DistanceTo(neighbor);

                    // Optional uphill penalty
                    double dz = neighbor.Z - current.Z;
                    if (dz > 0)
                        cost += dz * 100.0;

                    double tentativeG = gScore[current] + cost;

                    if (tentativeG < gScore[neighbor])
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeG;
                        fScore[neighbor] = tentativeG + HeuristicToClosestDrain(neighbor, drainNodes);
                        open.Add(neighbor);
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

        // Implementation for IPathFindingStrategy.FindPath(RoofGraph, GraphNode)
        public PathResult FindPath(RoofGraph graph, GraphNode startNode)
        {
            // Use graph.DrainNodes as the drain set
            var drainNodes = graph.DrainNodes != null
                ? new HashSet<GraphNode>(graph.DrainNodes)
                : new HashSet<GraphNode>();
            return FindPath(graph, startNode, drainNodes);
        }

        private static double HeuristicToClosestDrain(GraphNode node, HashSet<GraphNode> drains)
        {
            // 2D distance heuristic (fast + stable)
            return drains.Min(d => node.Distance2DTo(d));
        }

        private static PathResult BuildResult(GraphNode start, GraphNode end, Dictionary<GraphNode, GraphNode> cameFrom)
        {
            var path = new List<GraphNode>();
            GraphNode current = end;

            while (true)
            {
                path.Add(current);

                if (current == start)
                    break;

                if (!cameFrom.TryGetValue(current, out current))
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
