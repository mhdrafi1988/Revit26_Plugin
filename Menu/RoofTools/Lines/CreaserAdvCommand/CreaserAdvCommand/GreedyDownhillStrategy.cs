using System.Collections.Generic;
using System.Linq;
using Revit26_Plugin.Creaser_adv_V001.Models;

namespace Revit26_Plugin.Creaser_adv_V001.Services
{
    /// <summary>
    /// Fast greedy downhill pathfinding.
    /// Always chooses the lowest adjacent node.
    /// Fastest, but not guaranteed to succeed.
    /// </summary>
    public class GreedyDownhillStrategy : IPathFindingStrategy
    {
        public PathResult FindPath(RoofGraph graph, GraphNode startNode)
        {
            var visited = new HashSet<GraphNode>();
            var path = new List<GraphNode>();

            GraphNode current = startNode;
            path.Add(current);

            while (true)
            {
                if (current.IsDrain)
                {
                    return new PathResult
                    {
                        PathFound = true,
                        StartNode = startNode,
                        EndNode = current,
                        OrderedNodes = path
                    };
                }

                visited.Add(current);

                GraphNode next = current.Neighbors
                    .Where(n => n.Z < current.Z && !visited.Contains(n))
                    .OrderBy(n => n.Z)
                    .FirstOrDefault();

                if (next == null)
                {
                    return new PathResult
                    {
                        PathFound = false,
                        StartNode = startNode,
                        FailureReason = "No lower adjacent node found (local minimum)."
                    };
                }

                path.Add(next);
                current = next;
            }
        }
    }
}
