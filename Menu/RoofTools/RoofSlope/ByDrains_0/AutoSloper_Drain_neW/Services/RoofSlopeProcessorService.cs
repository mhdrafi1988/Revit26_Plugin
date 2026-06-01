// Services/Implementations/RoofSlopeProcessorService.cs
using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlopeByDrain_21.Models;
using Revit26_Plugin.AutoSlopeByDrain_21.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByDrain_21.Services.Implementations
{
    public class RoofSlopeProcessorService : IRoofSlopeProcessorService
    {
        private readonly IUnitConversionService _unitService;

        public RoofSlopeProcessorService(IUnitConversionService unitService = null)
        {
            _unitService = unitService ?? new UnitConversionService();
        }

        public SlopeResult ProcessRoofSlopes(
            RoofBase roof,
            List<DrainItem> selectedDrains,
            double slopePercent,
            double thresholdMeters,
            Action<string> logAction = null)
        {
            var result = new SlopeResult
            {
                SlopePercent1 = slopePercent,
                SlopePercent2 = slopePercent,
                Success = false
            };
            var startTime = DateTime.Now;

            try
            {
                logAction?.Invoke($"Starting slope processing at {slopePercent}%...");

                // 1. Enable shape editing if not already
                var shapeEditor = roof.GetSlabShapeEditor();
                if (shapeEditor == null)
                    throw new Exception("Roof does not support shape editing.");

                if (!shapeEditor.IsEnabled)
                {
                    logAction?.Invoke("Enabling shape editing...");
                    shapeEditor.Enable();
                }

                // 2. Get all shape vertices
                var vertices = GetSlabVertices(shapeEditor);
                logAction?.Invoke($"Found {vertices.Count} shape vertices.");

                // 3. Map selected drains to actual slab vertices (by proximity)
                var drainVertices = MapDrainVertices(selectedDrains, vertices, _unitService);
                logAction?.Invoke($"Matched {drainVertices.Count} drain vertices.");

                // 4. Set drain vertices to zero elevation
                foreach (var v in drainVertices)
                    shapeEditor.ModifySubElement(v, 0.0);
                logAction?.Invoke($"Set {drainVertices.Count} drain vertices to zero.");

                // 5. Build adjacency graph from top face
                var graph = BuildGraph(vertices, roof);
                logAction?.Invoke($"Graph built with {graph.Count} nodes.");

                // 6. Compute shortest distances from every vertex to nearest drain vertex
                var distances = ComputeShortestDistances(vertices, drainVertices, graph);
                logAction?.Invoke($"Distances computed for {distances.Count} vertices.");

                // 7. Apply slope elevations based on distance
                double slopeRatio = slopePercent / 100.0;
                int modified = 0;
                double maxElevMm = 0;
                double longestPathMeters = 0;

                foreach (var vertex in vertices)
                {
                    if (drainVertices.Contains(vertex)) continue;
                    if (!distances.TryGetValue(vertex, out double distFeet)) continue;

                    double distMeters = distFeet * 0.3048;
                    if (distMeters > longestPathMeters) longestPathMeters = distMeters;

                    // Elevation in feet = slopeRatio * distance (feet)
                    double elevFeet = slopeRatio * distFeet;
                    shapeEditor.ModifySubElement(vertex, elevFeet);
                    modified++;

                    double elevMm = elevFeet * 304.8;
                    if (elevMm > maxElevMm) maxElevMm = elevMm;
                }

                // 8. Update result
                result.Success = true;
                result.VerticesProcessed = modified;
                result.VerticesSkipped = vertices.Count - modified - drainVertices.Count;
                result.HighestElevationMm = maxElevMm;
                result.LongestPathMeters = longestPathMeters;
                result.AvgSlopePercent = slopePercent;
                result.RunDuration_sec = (DateTime.Now - startTime).TotalSeconds;

                logAction?.Invoke($"✅ Slope applied: {modified} vertices modified, longest path {longestPathMeters:F2}m, max elev {maxElevMm:F0}mm");

                return result;
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error: {ex.Message}");
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        // Helper: Get all SlabShapeVertex from editor (using reflection or proper method)
        private List<SlabShapeVertex> GetSlabVertices(SlabShapeEditor editor)
        {
            // Use the public SlabShapeVertices property of SlabShapeEditor
            var vertices = new List<SlabShapeVertex>();
            var slabShapeVertices = editor.SlabShapeVertices;
            if (slabShapeVertices != null)
            {
                foreach (SlabShapeVertex v in slabShapeVertices)
                {
                    vertices.Add(v);
                }
            }
            return vertices;
        }

        private HashSet<SlabShapeVertex> MapDrainVertices(List<DrainItem> drains, List<SlabShapeVertex> allVertices, IUnitConversionService unitService)
        {
            var matched = new HashSet<SlabShapeVertex>();
            double toleranceFeet = 0.005; // ~1.5mm

            foreach (var drain in drains)
            {
                foreach (var pt in drain.DrainVertices) // Point3D in mm
                {
                    var xyzFeet = new XYZ(pt.X / 304.8, pt.Y / 304.8, pt.Z / 304.8);
                    var closest = allVertices.FirstOrDefault(v => v.Position.DistanceTo(xyzFeet) < toleranceFeet);
                    if (closest != null)
                        matched.Add(closest);
                }
            }
            return matched;
        }

        private Dictionary<SlabShapeVertex, List<SlabShapeVertex>> BuildGraph(List<SlabShapeVertex> vertices, RoofBase roof)
        {
            // Build adjacency: two vertices are adjacent if the edge between them lies on the top face.
            var graph = new Dictionary<SlabShapeVertex, List<SlabShapeVertex>>();
            var topFace = GetTopFace(roof);

            foreach (var v in vertices)
                graph[v] = new List<SlabShapeVertex>();

            // For simplicity, connect every vertex to its nearest neighbors (Delaunay-like)
            // Real implementation should use the face edge loops. Here we do a basic proximity + edge existence test.
            double maxDistFeet = 1.0; // 1 foot maximum distance for connectivity

            for (int i = 0; i < vertices.Count; i++)
            {
                for (int j = i + 1; j < vertices.Count; j++)
                {
                    double dist = vertices[i].Position.DistanceTo(vertices[j].Position);
                    if (dist < maxDistFeet)
                    {
                        // Check if the segment lies on the top face (optional)
                        graph[vertices[i]].Add(vertices[j]);
                        graph[vertices[j]].Add(vertices[i]);
                    }
                }
            }
            return graph;
        }

        private Face GetTopFace(RoofBase roof)
        {
            var opt = new Options { ComputeReferences = true, DetailLevel = ViewDetailLevel.Medium };
            var geomElem = roof.get_Geometry(opt);
            foreach (GeometryObject obj in geomElem)
            {
                if (obj is Solid solid)
                {
                    foreach (Face face in solid.Faces)
                    {
                        var bbox = face.GetBoundingBox();
                        var uvCenter = new UV((bbox.Min.U + bbox.Max.U) / 2, (bbox.Min.V + bbox.Max.V) / 2);
                        var normal = face.ComputeNormal(uvCenter);
                        if (normal.Z > 0.5)
                            return face;
                    }
                }
            }
            return null;
        }

        private Dictionary<SlabShapeVertex, double> ComputeShortestDistances(
            List<SlabShapeVertex> vertices,
            HashSet<SlabShapeVertex> sources,
            Dictionary<SlabShapeVertex, List<SlabShapeVertex>> graph)
        {
            var distances = new Dictionary<SlabShapeVertex, double>();
            var pq = new SortedSet<(double, SlabShapeVertex)>(Comparer<(double, SlabShapeVertex)>.Create((a, b) =>
                a.Item1.CompareTo(b.Item1) != 0 ? a.Item1.CompareTo(b.Item1) : a.Item2.GetHashCode().CompareTo(b.Item2.GetHashCode())));

            foreach (var v in vertices)
            {
                if (sources.Contains(v))
                {
                    distances[v] = 0;
                    pq.Add((0, v));
                }
                else
                    distances[v] = double.MaxValue;
            }

            while (pq.Count > 0)
            {
                var (curDist, u) = pq.Min;
                pq.Remove(pq.Min);
                if (curDist > distances[u]) continue;

                if (!graph.ContainsKey(u)) continue;
                foreach (var v in graph[u])
                {
                    double edgeLen = u.Position.DistanceTo(v.Position);
                    double newDist = curDist + edgeLen;
                    if (newDist < distances[v])
                    {
                        distances[v] = newDist;
                        pq.Add((newDist, v));
                    }
                }
            }
            return distances;
        }
    }
}