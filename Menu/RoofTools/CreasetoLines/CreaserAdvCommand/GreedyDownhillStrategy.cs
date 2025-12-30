using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Revit26_Plugin.Creaser_adv_V001.Models;

namespace Revit26_Plugin.Creaser_adv_V001.Services
{
    public class GreedyDownhillStrategy : IPathFindingStrategy
    {
        private const double ElevationTolerance = 1e-6;

        public PathResult FindPath(RoofGraph graph, GraphNode startNode)
        {
            var visited = new HashSet<GraphNode>();
            var ordered = new List<GraphNode>();

            GraphNode current = startNode;
            ordered.Add(current);
            visited.Add(current);

            bool isFirstStep = true;

            while (true)
            {
                if (graph.DrainNodes.Contains(current))
                {
                    return new PathResult(
                        ordered,
                        current,
                        true,
                        null);
                }

                GraphNode next = null;
                double bestScore = double.MaxValue;

                foreach (GraphNode neighbor in current.Neighbors)
                {
                    if (visited.Contains(neighbor))
                        continue;

                    double dz = neighbor.Point.Z - current.Point.Z;

                    if (!isFirstStep && dz > ElevationTolerance)
                        continue;

                    double score =
                        Math.Max(0, dz) * 10.0 +
                        current.Point.DistanceTo(neighbor.Point);

                    if (score < bestScore)
                    {
                        bestScore = score;
                        next = neighbor;
                    }
                }

                if (next == null)
                {
                    return new PathResult(
                        null,
                        null,
                        false,
                        "No valid downhill neighbor (forced corner mode).");
                }

                current = next;
                ordered.Add(current);
                visited.Add(current);
                isFirstStep = false;
            }
        }
    }
}
