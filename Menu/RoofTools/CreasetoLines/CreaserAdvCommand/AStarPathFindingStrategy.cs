using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.Creaser_adv_V001.Models;

namespace Revit26_Plugin.Creaser_adv_V001.Services
{
    public class AStarPathFindingStrategy : IPathFindingStrategy
    {
        private const double UphillPenaltyFactor = 10.0;

        public PathResult FindPath(RoofGraph graph, GraphNode startNode)
        {
            var openSet = new HashSet<GraphNode> { startNode };
            var cameFrom = new Dictionary<GraphNode, GraphNode>();

            var gScore = graph.Nodes.ToDictionary(n => n, _ => double.MaxValue);
            gScore[startNode] = 0;

            var fScore = graph.Nodes.ToDictionary(n => n, _ => double.MaxValue);
            fScore[startNode] = Heuristic(startNode, graph);

            while (openSet.Any())
            {
                GraphNode current =
                    openSet.OrderBy(n => fScore[n]).First();

                if (graph.DrainNodes.Contains(current))
                {
                    IList<GraphNode> ordered = BuildPath(current, cameFrom);
                    return new PathResult(
                        ordered,
                        current,
                        true,
                        null);
                }

                openSet.Remove(current);

                foreach (GraphNode neighbor in current.Neighbors)
                {
                    double dz = neighbor.Point.Z - current.Point.Z;
                    double elevationPenalty =
                        dz > 0 ? dz * UphillPenaltyFactor : 0;

                    double tentativeG =
                        gScore[current] +
                        current.Point.DistanceTo(neighbor.Point) +
                        elevationPenalty;

                    if (tentativeG < gScore[neighbor])
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeG;
                        fScore[neighbor] =
                            tentativeG + Heuristic(neighbor, graph);

                        openSet.Add(neighbor);
                    }
                }
            }

            return new PathResult(
                null,
                null,
                false,
                "No path to drain found (forced corner mode).");
        }

        private double Heuristic(GraphNode node, RoofGraph graph)
        {
            return graph.DrainNodes
                .Min(d => node.Point.DistanceTo(d.Point));
        }

        private IList<GraphNode> BuildPath(
            GraphNode end,
            Dictionary<GraphNode, GraphNode> cameFrom)
        {
            var ordered = new List<GraphNode> { end };
            while (cameFrom.TryGetValue(end, out GraphNode prev))
            {
                ordered.Add(prev);
                end = prev;
            }
            ordered.Reverse();
            return ordered;
        }
    }
}
