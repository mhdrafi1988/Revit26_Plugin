using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    public class CornerDetectionService
    {
        public IList<XYZ> DetectCorners(
            EdgeLoop2D outerLoop,
            LoggingService log)
        {
            if (outerLoop == null)
                throw new ArgumentNullException(nameof(outerLoop));

            var vertices = GetOrderedVertices(outerLoop);
            log.Info("Detecting corners from outer boundary.");

            var corners = new List<XYZ>();
            int n = vertices.Count;

            if (n < 3)
                return corners;

            for (int i = 0; i < n; i++)
            {
                XYZ prev = vertices[(i - 1 + n) % n];
                XYZ curr = vertices[i];
                XYZ next = vertices[(i + 1) % n];

                XYZ v1 = (prev - curr);
                XYZ v2 = (next - curr);

                if (v1.GetLength() < GeometryTolerance.Point || v2.GetLength() < GeometryTolerance.Point)
                    continue;

                v1 = v1.Normalize();
                v2 = v2.Normalize();

                double dot = v1.DotProduct(v2);

                if (Math.Abs(dot) < 0.999)
                    corners.Add(curr);
            }

            log.Info($"Corners detected: {corners.Count}");
            return corners;
        }

        private static IList<XYZ> GetOrderedVertices(EdgeLoop2D loop)
        {
            var vertices = new List<XYZ>();

            if (loop.Edges.Count == 0)
                return vertices;

            vertices.Add(loop.Edges[0].Start2D);

            foreach (var e in loop.Edges)
                vertices.Add(e.End2D);

            if (vertices.Count > 1)
                vertices.RemoveAt(vertices.Count - 1);

            return vertices;
        }
    }
}
