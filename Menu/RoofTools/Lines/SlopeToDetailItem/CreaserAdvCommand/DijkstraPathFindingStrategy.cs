using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.Creaser_adv_V001.Models;

namespace Revit26_Plugin.Creaser_adv_V001.Services
{
    /// <summary>
    /// Enhanced Dijkstra with roof face validation for edge connectivity.
    /// Uses actual roof geometry to ensure valid drainage paths.
    /// </summary>
    public class DijkstraPathFindingStrategy : IPathFindingStrategy
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

            // Build adjacency based on roof face validation if available
            Dictionary<GraphNode, List<GraphNode>> adjacency;
            if (_roofFace != null)
            {
                adjacency = BuildFaceValidatedAdjacency(graph.Nodes);
            }
            else
            {
                // Fallback to existing neighbor connections
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
                // Pick node with smallest tentative distance
                GraphNode current = unvisited.OrderBy(n => distances[n]).First();
                unvisited.Remove(current);

                // Early exit ONLY when we pop a drain (Dijkstra correctness)
                if (drainNodes.Contains(current) && current != startNode)
                {
                    return BuildResult(startNode, current, previous);
                }

                // Get valid neighbors from adjacency dictionary
                if (!adjacency.TryGetValue(current, out var neighbors))
                    continue;

                foreach (GraphNode neighbor in neighbors)
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

        /// <summary>
        /// Builds adjacency list using roof face validation for edge connectivity
        /// </summary>
        private Dictionary<GraphNode, List<GraphNode>> BuildFaceValidatedAdjacency(IList<GraphNode> nodes)
        {
            var adjacency = new Dictionary<GraphNode, List<GraphNode>>();
            int count = nodes.Count;

            // Initialize adjacency lists
            foreach (var node in nodes)
            {
                adjacency[node] = new List<GraphNode>();
            }

            // Check all possible edges
            for (int i = 0; i < count; i++)
            {
                GraphNode a = nodes[i];
                XYZ pointA = a.Point;

                for (int j = i + 1; j < count; j++)
                {
                    GraphNode b = nodes[j];
                    XYZ pointB = b.Point;

                    double dist = pointA.DistanceTo(pointB);

                    // Skip micro edges (less than 0.5 feet)
                    if (dist < 0.5) continue;

                    // Skip edges beyond threshold
                    if (dist > _edgeThresholdFt) continue;

                    // Validate edge against roof face
                    if (_roofFace != null && !IsValidEdgeOnFace(pointA, pointB))
                        continue;

                    // If valid, add undirected edge
                    adjacency[a].Add(b);
                    adjacency[b].Add(a);
                }
            }

            return adjacency;
        }

        /// <summary>
        /// Checks if the edge between two points lies entirely on the roof face
        /// </summary>
        private bool IsValidEdgeOnFace(XYZ a, XYZ b)
        {
            try
            {
                Line edgeLine = Line.CreateBound(a, b);
                double dist = a.DistanceTo(b);

                // Adaptive sample count based on edge length
                int samples = System.Math.Max(10, (int)(dist * 4));
                double step = 1.0 / samples;

                // Sample points along the edge
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

        /// <summary>
        /// Checks if a point lies on the roof face with projection tolerance
        /// </summary>
        private bool IsPointOnRoofFace(XYZ point)
        {
            if (_roofFace == null) return true; // Fallback if no face available

            // Try direct projection
            IntersectionResult proj = _roofFace.Project(point);

            // If direct projection fails, try nudging point vertically
            if (proj == null)
            {
                XYZ pUp = point + new XYZ(0, 0, PROJ_TOL);
                proj = _roofFace.Project(pUp);

                if (proj == null)
                {
                    XYZ pDown = point - new XYZ(0, 0, PROJ_TOL);
                    proj = _roofFace.Project(pDown);

                    // All failed → treat as invalid
                    if (proj == null)
                        return false;
                }
            }

            UV uv = proj.UVPoint;

            // Check if UV point is inside the face (handles trimmed faces)
            try
            {
                if (_roofFace.IsInside(uv))
                    return true;
            }
            catch
            {
                // Fallback to bounding box check
            }

            // Bounding box fallback
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

            // Calculate total path length
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