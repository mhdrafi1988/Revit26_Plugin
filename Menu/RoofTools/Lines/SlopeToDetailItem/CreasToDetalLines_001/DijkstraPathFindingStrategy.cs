using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.Creaser_adv_V001.Models;

namespace Revit26_Plugin.Creaser_adv_V001.Services
{
    public class DijkstraPathFindingStrategy
    {
        private const double PROJ_TOL = 0.00328084; // ~1mm
        private readonly double _edgeThresholdFt = 50.0; // Maximum edge length in feet
        private readonly Face _roofFace;

        public DijkstraPathFindingStrategy() : this(null) { }

        public DijkstraPathFindingStrategy(Face roofFace)
        {
            _roofFace = roofFace;
        }

        public PathResult FindPath(RoofGraph graph, GraphNode startNode)
        {
            var drainNodes = new HashSet<GraphNode>(graph.DrainNodes);

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

            Dictionary<GraphNode, List<GraphNode>> adjacency;
            if (_roofFace != null)
            {
                adjacency = BuildFaceValidatedAdjacency(graph.Nodes);
            }
            else
            {
                adjacency = graph.Nodes.ToDictionary(
                    n => n,
                    n => n.Neighbors.ToList()
                );
            }

            var distances = graph.Nodes.ToDictionary(n => n, _ => double.MaxValue);
            var previous = new Dictionary<GraphNode, GraphNode>();
            var unvisited = new HashSet<GraphNode>(graph.Nodes);

            distances[startNode] = 0;

            while (unvisited.Count > 0)
            {
                GraphNode current = unvisited.OrderBy(n => distances[n]).First();
                unvisited.Remove(current);

                if (drainNodes.Contains(current) && current != startNode)
                {
                    return BuildResult(startNode, current, previous);
                }

                if (!adjacency.TryGetValue(current, out var neighbors))
                    continue;

                foreach (GraphNode neighbor in neighbors)
                {
                    if (!unvisited.Contains(neighbor))
                        continue;

                    double cost = current.DistanceTo(neighbor);

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

        private Dictionary<GraphNode, List<GraphNode>> BuildFaceValidatedAdjacency(IList<GraphNode> nodes)
        {
            var adjacency = new Dictionary<GraphNode, List<GraphNode>>();
            int count = nodes.Count;

            foreach (var node in nodes)
            {
                adjacency[node] = new List<GraphNode>();
            }

            for (int i = 0; i < count; i++)
            {
                GraphNode a = nodes[i];
                XYZ pointA = a.Point;

                for (int j = i + 1; j < count; j++)
                {
                    GraphNode b = nodes[j];
                    XYZ pointB = b.Point;

                    double dist = pointA.DistanceTo(pointB);

                    if (dist < 0.5) continue;

                    if (dist > _edgeThresholdFt) continue;

                    if (_roofFace != null && !IsValidEdgeOnFace(pointA, pointB))
                        continue;

                    adjacency[a].Add(b);
                    adjacency[b].Add(a);
                }
            }

            return adjacency;
        }

        private bool IsValidEdgeOnFace(XYZ a, XYZ b)
        {
            try
            {
                Line edgeLine = Line.CreateBound(a, b);
                double dist = a.DistanceTo(b);

                int samples = System.Math.Max(10, (int)(dist * 4));
                double step = 1.0 / samples;

                for (double t = step; t < 1.0; t += step)
                {
                    XYZ samplePoint = edgeLine.Evaluate(t, true);
                    if (!IsPointOnRoofFace(samplePoint))
                        return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool IsPointOnRoofFace(XYZ point)
        {
            if (_roofFace == null) return true;

            IntersectionResult proj = _roofFace.Project(point);

            if (proj == null)
            {
                XYZ pUp = point + new XYZ(0, 0, PROJ_TOL);
                proj = _roofFace.Project(pUp);

                if (proj == null)
                {
                    XYZ pDown = point - new XYZ(0, 0, PROJ_TOL);
                    proj = _roofFace.Project(pDown);

                    if (proj == null)
                        return false;
                }
            }

            UV uv = proj.UVPoint;

            try
            {
                if (_roofFace.IsInside(uv))
                    return true;
            }
            catch { }

            BoundingBoxUV bb = _roofFace.GetBoundingBox();
            if (bb == null) return false;

            return (uv.U >= bb.Min.U &&
                    uv.U <= bb.Max.U &&
                    uv.V >= bb.Min.V &&
                    uv.V <= bb.Max.V);
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

            double totalLength = 0;
            for (int i = 0; i < path.Count - 1; i++)
            {
                totalLength += path[i].DistanceTo(path[i + 1]);
            }

            return new PathResult
            {
                PathFound = true,
                StartNode = start,
                EndNode = end,
                OrderedNodes = path,
                TotalLength = totalLength
            };
        }
    }
}