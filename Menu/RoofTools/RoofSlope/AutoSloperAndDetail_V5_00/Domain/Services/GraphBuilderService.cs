using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.V5_00.Domain.Services
{
    public class GraphBuilderService
    {
        public Dictionary<SlabShapeVertex, List<SlabShapeVertex>> BuildGraph(
            List<SlabShapeVertex> vertices,
            Face topFace)
        {
            var graph = new Dictionary<SlabShapeVertex, List<SlabShapeVertex>>();
            if (vertices == null || topFace == null)
                return graph;

            double threshold = 100.0; // feet (legacy logic)

            foreach (var v in vertices)
            {
                if (v?.Position == null) continue;
                graph[v] = new List<SlabShapeVertex>();

                foreach (var other in vertices)
                {
                    if (other == null || other == v || other.Position == null)
                        continue;

                    if (v.Position.DistanceTo(other.Position) > threshold)
                        continue;

                    if (IsValidConnection(v.Position, other.Position, topFace))
                        graph[v].Add(other);
                }
            }

            return graph;
        }

        private bool IsValidConnection(XYZ a, XYZ b, Face face)
        {
            try
            {
                Line line = Line.CreateBound(a, b);
                for (double t = 0.1; t < 1.0; t += 0.2)
                {
                    XYZ p = line.Evaluate(t, true);
                    if (!IsPointOnFace(p, face))
                        return false;
                }
                return true;
            }
            catch { return false; }
        }

        private bool IsPointOnFace(XYZ p, Face face)
        {
            IntersectionResult res = face.Project(p);
            if (res == null) return false;

            UV uv = res.UVPoint;
            BoundingBoxUV bb = face.GetBoundingBox();

            return uv.U >= bb.Min.U && uv.U <= bb.Max.U &&
                   uv.V >= bb.Min.V && uv.V <= bb.Max.V;
        }
    }
}
