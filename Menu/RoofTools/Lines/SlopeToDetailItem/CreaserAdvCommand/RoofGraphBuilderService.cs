using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.Creaser_adv_V001.Models;
using Revit26_Plugin.Creaser_adv_V001.Helpers;

namespace Revit26_Plugin.Creaser_adv_V001.Services
{
    /// <summary>
    /// FINAL graph builder with roof face validation.
    /// Fully connected graph with slope-aware pathfinding and geometry validation.
    /// </summary>
    public class RoofGraphBuilderService
    {
        private const double ZTolerance = 0.05; // feet
        private const double PROJ_TOL = 0.00328084; // ~1mm for validation
        private readonly double _edgeThresholdFt = 50.0; // Maximum edge length in feet

        /// <summary>
        /// Extended RoofGraph that includes the roof face for geometry validation
        /// </summary>
        public class EnhancedRoofGraph : RoofGraph
        {
            public Face RoofFace { get; set; }
            public Dictionary<GraphNode, List<GraphNode>> ValidatedAdjacency { get; set; }
        }

        public EnhancedRoofGraph Build(RoofBase roof)
        {
            if (roof == null)
                throw new ArgumentNullException(nameof(roof));

            SlabShapeEditor editor = roof.GetSlabShapeEditor();
            if (editor == null)
                throw new InvalidOperationException("Roof does not support shape editing.");

            IList<SlabShapeVertex> vertices = editor.SlabShapeVertices?.Cast<SlabShapeVertex>().ToList();
            if (vertices == null || vertices.Count == 0)
                throw new InvalidOperationException("No shape vertices found.");

            // Get the top face of the roof for geometry validation
            Face roofFace = GetRoofTopFace(roof);

            EnhancedRoofGraph graph = new EnhancedRoofGraph
            {
                RoofFace = roofFace
            };

            int id = 0;

            // ------------------------------------------------
            // 1. Create nodes from slab shape vertices
            // ------------------------------------------------
            foreach (SlabShapeVertex v in vertices)
            {
                graph.Nodes.Add(new GraphNode(id++, v.Position));
            }

            // ------------------------------------------------
            // 2. Build validated adjacency (instead of full connectivity)
            // ------------------------------------------------
            graph.ValidatedAdjacency = BuildValidatedAdjacency(graph.Nodes, roofFace);

            // Also populate neighbor lists for backward compatibility
            foreach (var kvp in graph.ValidatedAdjacency)
            {
                var node = kvp.Key;
                var neighbors = kvp.Value;

                // Clear existing neighbors and add validated ones
                ((List<GraphNode>)node.Neighbors).Clear();
                foreach (var neighbor in neighbors)
                {
                    node.Neighbors.Add(neighbor);
                }
            }

            // ------------------------------------------------
            // 3. Corners = highest Z
            // ------------------------------------------------
            double maxZ = graph.Nodes.Max(n => n.Z);

            foreach (GraphNode node in graph.Nodes)
            {
                if (Math.Abs(node.Z - maxZ) <= ZTolerance)
                {
                    node.IsCorner = true;
                    graph.CornerNodes.Add(node);
                }
            }

            // ------------------------------------------------
            // 4. Drains = lowest Z (excluding corners)
            // ------------------------------------------------
            double minZ = graph.Nodes.Min(n => n.Z);

            foreach (GraphNode node in graph.Nodes)
            {
                if (node.IsCorner) continue;

                if (Math.Abs(node.Z - minZ) <= ZTolerance)
                {
                    node.IsDrain = true;
                    graph.DrainNodes.Add(node);
                }
            }

            return graph;
        }

        /// <summary>
        /// Builds adjacency list using roof face validation for edge connectivity
        /// </summary>
        private Dictionary<GraphNode, List<GraphNode>> BuildValidatedAdjacency(
            IList<GraphNode> nodes, Face roofFace)
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

                    // Validate edge against roof face if available
                    if (roofFace != null && !IsValidEdgeOnFace(pointA, pointB, roofFace))
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
        private bool IsValidEdgeOnFace(XYZ a, XYZ b, Face roofFace)
        {
            try
            {
                Line edgeLine = Line.CreateBound(a, b);
                double dist = a.DistanceTo(b);

                // Adaptive sample count based on edge length
                int samples = Math.Max(10, (int)(dist * 4));
                double step = 1.0 / samples;

                // Sample points along the edge
                for (double t = step; t < 1.0; t += step)
                {
                    XYZ samplePoint = edgeLine.Evaluate(t, true);
                    if (!IsPointOnRoofFace(samplePoint, roofFace))
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
        private bool IsPointOnRoofFace(XYZ point, Face roofFace)
        {
            if (roofFace == null) return true; // Fallback if no face available

            // Try direct projection
            IntersectionResult proj = roofFace.Project(point);

            // If direct projection fails, try nudging point vertically
            if (proj == null)
            {
                XYZ pUp = point + new XYZ(0, 0, PROJ_TOL);
                proj = roofFace.Project(pUp);

                if (proj == null)
                {
                    XYZ pDown = point - new XYZ(0, 0, PROJ_TOL);
                    proj = roofFace.Project(pDown);

                    // All failed → treat as invalid
                    if (proj == null)
                        return false;
                }
            }

            UV uv = proj.UVPoint;

            // Check if UV point is inside the face (handles trimmed faces)
            try
            {
                if (roofFace.IsInside(uv))
                    return true;
            }
            catch
            {
                // Fallback to bounding box check
            }

            // Bounding box fallback
            BoundingBoxUV bb = roofFace.GetBoundingBox();
            if (bb == null) return false;

            return (uv.U >= bb.Min.U &&
                    uv.U <= bb.Max.U &&
                    uv.V >= bb.Min.V &&
                    uv.V <= bb.Max.V);
        }

        /// <summary>
        /// Extracts the top face from the roof geometry
        /// </summary>
        private Face GetRoofTopFace(RoofBase roof)
        {
            try
            {
                Options options = new Options
                {
                    ComputeReferences = true,
                    IncludeNonVisibleObjects = true,
                    DetailLevel = ViewDetailLevel.Fine
                };

                GeometryElement geomElem = roof.get_Geometry(options);
                if (geomElem == null)
                    return null;

                // Find the solid representing the roof
                Solid roofSolid = null;
                foreach (GeometryObject geomObj in geomElem)
                {
                    if (geomObj is Solid solid && solid.Faces.Size > 0 && solid.Volume > 0)
                    {
                        roofSolid = solid;
                        break;
                    }
                }

                if (roofSolid == null)
                    return null;

                // Find the top-facing face (with highest average Z)
                Face topFace = null;
                double maxAvgZ = double.MinValue;

                foreach (Face face in roofSolid.Faces)
                {
                    // Get face normal
                    XYZ normal = face.ComputeNormal(new UV(0.5, 0.5));
                    if (normal == null) continue;

                    // Check if face is roughly horizontal and facing up
                    if (Math.Abs(normal.Z) > 0.8) // Mostly vertical faces are not top faces
                    {
                        // Sample multiple points on the face to get average Z
                        BoundingBoxUV bb = face.GetBoundingBox();
                        if (bb == null) continue;

                        double sumZ = 0;
                        int sampleCount = 0;

                        // Sample 4 corners and center
                        UV[] sampleUVs = new UV[]
                        {
                            new UV(bb.Min.U, bb.Min.V),
                            new UV(bb.Max.U, bb.Min.V),
                            new UV(bb.Max.U, bb.Max.V),
                            new UV(bb.Min.U, bb.Max.V),
                            new UV((bb.Min.U + bb.Max.U) / 2, (bb.Min.V + bb.Max.V) / 2)
                        };

                        foreach (UV uv in sampleUVs)
                        {
                            try
                            {
                                XYZ point = face.Evaluate(uv);
                                sumZ += point.Z;
                                sampleCount++;
                            }
                            catch
                            {
                                // Skip invalid UV
                            }
                        }

                        if (sampleCount > 0)
                        {
                            double avgZ = sumZ / sampleCount;
                            if (avgZ > maxAvgZ)
                            {
                                maxAvgZ = avgZ;
                                topFace = face;
                            }
                        }
                    }
                }

                return topFace;
            }
            catch (Exception ex)
            {
                // Log error if needed
                System.Diagnostics.Debug.WriteLine($"Error extracting roof face: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Legacy method for backward compatibility (without face validation)
        /// </summary>
        public RoofGraph BuildLegacy(RoofBase roof)
        {
            if (roof == null)
                throw new ArgumentNullException(nameof(roof));

            SlabShapeEditor editor = roof.GetSlabShapeEditor();
            if (editor == null)
                throw new InvalidOperationException("Roof does not support shape editing.");

            IList<SlabShapeVertex> vertices = editor.SlabShapeVertices?.Cast<SlabShapeVertex>().ToList();
            if (vertices == null || vertices.Count == 0)
                throw new InvalidOperationException("No shape vertices found.");

            RoofGraph graph = new RoofGraph();

            int id = 0;

            // 1. Create nodes
            foreach (SlabShapeVertex v in vertices)
            {
                graph.Nodes.Add(new GraphNode(id++, v.Position));
            }

            // 2. FULL CONNECTIVITY (for backward compatibility)
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                for (int j = 0; j < graph.Nodes.Count; j++)
                {
                    if (i == j) continue;
                    Connect(graph.Nodes[i], graph.Nodes[j]);
                }
            }

            // 3. Corners = highest Z
            double maxZ = graph.Nodes.Max(n => n.Z);

            foreach (GraphNode node in graph.Nodes)
            {
                if (Math.Abs(node.Z - maxZ) <= ZTolerance)
                {
                    node.IsCorner = true;
                    graph.CornerNodes.Add(node);
                }
            }

            // 4. Drains = lowest Z (excluding corners)
            double minZ = graph.Nodes.Min(n => n.Z);

            foreach (GraphNode node in graph.Nodes)
            {
                if (node.IsCorner) continue;

                if (Math.Abs(node.Z - minZ) <= ZTolerance)
                {
                    node.IsDrain = true;
                    graph.DrainNodes.Add(node);
                }
            }

            return graph;
        }

        private static void Connect(GraphNode from, GraphNode to)
        {
            if (!from.Neighbors.Contains(to))
                from.Neighbors.Add(to);
        }
    }
}