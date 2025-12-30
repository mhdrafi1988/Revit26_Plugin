using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.Creaser_adv_V001.Models;

namespace Revit26_Plugin.Creaser_adv_V001.Services
{
    /// <summary>
    /// Dijkstra strategy that finds the SHORTEST path
    /// along graph edges to the NEAREST drain.
    /// No straight-line shortcuts.
    /// </summary>
    public class DijkstraPathFindingStrategy : IPathFindingStrategy
    {
        // Small bias to prefer downhill without distorting geometry
        private const double ElevationBias = 0.1;

        public PathResult FindPath(RoofGraph graph, GraphNode startNode)
        {
            var distances = new Dictionary<GraphNode, double>();
            var previous = new Dictionary<GraphNode, GraphNode>();
            var visited = new HashSet<GraphNode>();

            foreach (GraphNode n in graph.Nodes)
                distances[n] = double.MaxValue;

            distances[startNode] = 0.0;

            while (true)
            {
                GraphNode current =
                    distances
                        .Where(kv => !visited.Contains(kv.Key))
                        .OrderBy(kv => kv.Value)
                        .Select(kv => kv.Key)
                        .FirstOrDefault();

                if (current == null)
                    break;

                // ✅ FIRST drain reached is the nearest one
                if (graph.DrainNodes.Contains(current))
                {
                    return new PathResult(
                        BuildPath(current, previous),
                        current,
                        true,
                        null);
                }

                visited.Add(current);

                foreach (GraphNode neighbor in current.Neighbors)
                {
                    if (visited.Contains(neighbor))
                        continue;

                    // 🔑 TRUE edge length (graph-following)
                    double edgeLength =
                        current.Point.DistanceTo(neighbor.Point);

                    // 🔹 Small downhill preference (does NOT straighten path)
                    double dz = neighbor.Point.Z - current.Point.Z;
                    double elevationCost =
                        dz > 0 ? dz * ElevationBias : 0;

                    double newCost =
                        distances[current] +
                        edgeLength +
                        elevationCost;

                    if (newCost < distances[neighbor])
                    {
                        distances[neighbor] = newCost;
                        previous[neighbor] = current;
                    }
                }
            }

            return new PathResult(
                null,
                null,
                false,
                "No path to drain found.");
        }

        private IList<GraphNode> BuildPath(
            GraphNode end,
            Dictionary<GraphNode, GraphNode> previous)
        {
            var ordered = new List<GraphNode>();
            GraphNode current = end;

            while (current != null)
            {
                ordered.Add(current);
                if (!previous.TryGetValue(current, out current))
                    break;
            }

            ordered.Reverse();
            return ordered;
        }
    }
}
