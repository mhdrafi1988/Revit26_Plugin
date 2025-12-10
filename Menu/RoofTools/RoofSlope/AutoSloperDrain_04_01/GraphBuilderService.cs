using Autodesk.Revit.DB;
using Revit22_Plugin.Asd_V4_01.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit22_Plugin.Asd_V4_01.Services
{
    public class GraphBuilderService
    {
        public Dictionary<SlabShapeVertex, List<SlabShapeVertex>> BuildGraph(
            List<SlabShapeVertex> vertices,
            Face topFace)
        {
            var graph = new Dictionary<SlabShapeVertex, List<SlabShapeVertex>>();

            try
            {
                if (vertices == null || topFace == null)
                    return graph;

                double threshold = 100.0; // mm or ft? matching original logic (feet representation)

                foreach (var v in vertices)
                {
                    if (v?.Position == null) continue;

                    graph[v] = new List<SlabShapeVertex>();

                    foreach (var other in vertices)
                    {
                        if (other == null ||
                            other == v ||
                            other.Position == null)
                            continue;

                        if (v.Position.DistanceTo(other.Position) > threshold)
                            continue;

                        if (IsValidConnection(v.Position, other.Position, topFace))
                        {
                            graph[v].Add(other);
                        }
                    }
                }

                return graph;
            }
            catch (Exception ex)
            {
                throw new Exception($"Graph build failed: {ex.Message}");
            }
        }

        private bool IsValidConnection(XYZ start, XYZ end, Face face)
        {
            try
            {
                if (start == null || end == null || face == null)
                    return false;

                Line line = Line.CreateBound(start, end);
                if (line == null) return false;

                for (double t = 0.1; t < 1.0; t += 0.2)
                {
                    XYZ p = line.Evaluate(t, true);

                    if (!IsPointOnFace(p, face))
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
                if (point == null || face == null)
                    return false;

                IntersectionResult result = face.Project(point);
                if (result == null) return false;

                UV uv = result.UVPoint;
                BoundingBoxUV bb = face.GetBoundingBox();
                if (bb == null) return false;

                return
                    uv.U >= bb.Min.U &&
                    uv.U <= bb.Max.U &&
                    uv.V >= bb.Min.V &&
                    uv.V <= bb.Max.V;
            }
            catch
            {
                return false;
            }
        }
    }
}
