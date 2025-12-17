using Autodesk.Revit.DB;
using Revit22_Plugin.Asd.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit22_Plugin.Asd.Services
{
    public class GraphBuilderService
    {
        public Dictionary<SlabShapeVertex, List<SlabShapeVertex>> BuildGraph(List<SlabShapeVertex> vertices, Face topFace)
        {
            var graph = new Dictionary<SlabShapeVertex, List<SlabShapeVertex>>();

            try
            {
                if (vertices == null || topFace == null) return graph;

                double connectionThreshold = 100.0; // Adjust based on your roof scale

                foreach (var vertex in vertices)
                {
                    if (vertex?.Position == null) continue;

                    graph[vertex] = new List<SlabShapeVertex>();

                    foreach (var other in vertices)
                    {
                        if (other?.Position == null || vertex == other) continue;

                        // Check distance threshold
                        if (vertex.Position.DistanceTo(other.Position) > connectionThreshold) continue;

                        // Check if connection is valid (lies on face)
                        if (IsValidConnection(vertex.Position, other.Position, topFace))
                        {
                            graph[vertex].Add(other);
                        }
                    }
                }

                return graph;
            }
            catch (Exception ex)
            {
                throw new Exception($"Graph construction failed: {ex.Message}");
            }
        }

        private bool IsValidConnection(XYZ start, XYZ end, Face face)
        {
            try
            {
                if (start == null || end == null || face == null) return false;

                Line line = Line.CreateBound(start, end);
                if (line == null) return false;

                // Sample points along the line to ensure it lies on the face
                for (double t = 0.1; t < 1.0; t += 0.2)
                {
                    XYZ testPoint = line.Evaluate(t, true);
                    if (!IsPointOnFace(testPoint, face))
                        return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool IsPointOnFace(XYZ point, Face face)
        {
            try
            {
                if (point == null || face == null) return false;

                IntersectionResult result = face.Project(point);
                if (result == null) return false;

                UV uv = result.UVPoint;
                BoundingBoxUV bb = face.GetBoundingBox();
                return bb != null &&
                       bb.Min.U <= uv.U && uv.U <= bb.Max.U &&
                       bb.Min.V <= uv.V && uv.V <= bb.Max.V;
            }
            catch
            {
                return false;
            }
        }
    }
}