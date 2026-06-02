using Autodesk.Revit.DB;
using Revit26_Plugin.Asd_19.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.Asd_19.Services
{
    public class GraphBuilderService
    {
        /// <summary>
        /// Builds the vertex connectivity graph.
        /// </summary>
        /// <param name="vertices">All slab shape vertices on the roof.</param>
        /// <param name="topFace">The top face of the roof.</param>
        /// <param name="connectionThresholdMeters">
        ///     Maximum distance in METERS between two vertices before they are
        ///     considered disconnected. Comes from the user's UI input.
        ///     Internally converted to Revit internal units (feet).
        /// </param>
        /// <param name="pathSampleCount">
        ///     Number of points sampled along the straight line between two vertices
        ///     to verify the edge lies on the roof face.
        ///     Higher = stricter check, catches edges crossing holes or voids.
        ///     Lower = faster but may allow invalid edges on complex roofs.
        ///     Minimum clamped to 2. Recommended range: 5-20.
        /// </param>
        public Dictionary<SlabShapeVertex, List<SlabShapeVertex>> BuildGraph(
            List<SlabShapeVertex> vertices,
            Face topFace,
            double connectionThresholdMeters = 30.0,
            int pathSampleCount = 5)
        {
            var graph = new Dictionary<SlabShapeVertex, List<SlabShapeVertex>>();

            try
            {
                if (vertices == null || topFace == null) return graph;

                // Convert user-supplied meters to Revit internal units (feet)
                double connectionThresholdFeet = connectionThresholdMeters / 0.3048;

                // Clamp sample count to a safe minimum
                int clampedSamples = Math.Max(2, pathSampleCount);

                foreach (var vertex in vertices)
                {
                    if (vertex?.Position == null) continue;

                    graph[vertex] = new List<SlabShapeVertex>();

                    foreach (var other in vertices)
                    {
                        if (other?.Position == null || vertex == other) continue;

                        // Check distance threshold (Revit coordinates are in feet)
                        if (vertex.Position.DistanceTo(other.Position) > connectionThresholdFeet) continue;

                        // Check if connection is valid (lies on face)
                        if (IsValidConnection(vertex.Position, other.Position, topFace, clampedSamples))
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

        /// <summary>
        /// Samples sampleCount evenly-spaced interior points along the line A->B
        /// and rejects the edge if any point falls outside the roof face.
        /// Points are placed at t = i / (sampleCount + 1) so they are always
        /// strictly inside the segment, never at the endpoints themselves.
        /// </summary>
        private bool IsValidConnection(XYZ start, XYZ end, Face face, int sampleCount)
        {
            try
            {
                if (start == null || end == null || face == null) return false;

                Line line = Line.CreateBound(start, end);
                if (line == null) return false;

                // Distribute sample points evenly across the interior of the segment.
                double step = 1.0 / (sampleCount + 1);
                for (int i = 1; i <= sampleCount; i++)
                {
                    double t = step * i;
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