using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using Revit26_Plugin.AutoLiner_V01.Helpers;

namespace Revit26_Plugin.AutoLiner_V01.Services
{
    public class GraphBuilderService
    {
        public Dictionary<SlabShapeVertex, List<SlabShapeVertex>> BuildGraph(
            List<SlabShapeVertex> vertices,
            Face topFace)
        {
            var graph = new Dictionary<SlabShapeVertex, List<SlabShapeVertex>>();

            if (vertices == null || topFace == null || vertices.Count == 0)
                return graph;

            double minEdge = GeometryTolerance.MmToFt(100);   // 100 mm
            double maxEdge = GeometryTolerance.MmToFt(3000);  // 3 m (safe default)

            foreach (var v in vertices)
            {
                if (v?.Position == null)
                    continue;

                graph[v] = new List<SlabShapeVertex>();
            }

            // pairwise but filtered
            for (int i = 0; i < vertices.Count; i++)
            {
                var a = vertices[i];
                if (a?.Position == null) continue;

                for (int j = i + 1; j < vertices.Count; j++)
                {
                    var b = vertices[j];
                    if (b?.Position == null) continue;

                    double dist = a.Position.DistanceTo(b.Position);

                    if (dist < minEdge || dist > maxEdge)
                        continue;

                    if (!IsValidEdge(a.Position, b.Position, topFace))
                        continue;

                    graph[a].Add(b);
                    graph[b].Add(a);
                }
            }

            return graph;
        }

        // =====================================================
        // EDGE VALIDATION — MUST LIE ON TOP FACE
        // =====================================================
        private bool IsValidEdge(XYZ a, XYZ b, Face face)
        {
            try
            {
                Line line = Line.CreateBound(a, b);
                if (line == null) return false;

                int samples = 10;
                double step = 1.0 / samples;

                for (int i = 1; i < samples; i++)
                {
                    XYZ p = line.Evaluate(i * step, true);

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
                IntersectionResult ir = face.Project(point);
                if (ir == null) return false;

                UV uv = ir.UVPoint;
                BoundingBoxUV bb = face.GetBoundingBox();
                if (bb == null) return false;

                return
                    uv.U >= bb.Min.U && uv.U <= bb.Max.U &&
                    uv.V >= bb.Min.V && uv.V <= bb.Max.V;
            }
            catch
            {
                return false;
            }
        }
    }
}
