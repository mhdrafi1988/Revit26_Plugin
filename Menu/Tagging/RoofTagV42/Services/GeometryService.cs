using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26.RoofTagV42.Services
{
    public static class GeometryService
    {
        private const double Tolerance = 0.001;

        public static List<XYZ> GetRoofVertices(RoofBase roof)
        {
            if (roof == null)
                throw new ArgumentNullException(nameof(roof));

            var vertices = new List<XYZ>();
            var editor = roof.GetSlabShapeEditor();

            if (editor != null && editor.IsEnabled)
            {
                vertices = editor.SlabShapeVertices
                    .Cast<SlabShapeVertex>()
                    .Select(v => v.Position)
                    .GroupBy(p => new
                    {
                        X = Math.Round(p.X / Tolerance) * Tolerance,
                        Y = Math.Round(p.Y / Tolerance) * Tolerance
                    })
                    .Select(g => g.First())
                    .ToList();
            }

            return vertices;
        }

        public static XYZ CalculateXYCentroid(IEnumerable<XYZ> points)
        {
            if (points == null || !points.Any())
                return XYZ.Zero;

            double sumX = 0, sumY = 0;
            int count = 0;

            foreach (var point in points)
            {
                sumX += point.X;
                sumY += point.Y;
                count++;
            }

            return new XYZ(sumX / count, sumY / count, 0);
        }

        public static List<XYZ> GetRoofBoundaryXY(RoofBase roof)
        {
            var boundaryPoints = new List<XYZ>();
            var options = new Options { DetailLevel = ViewDetailLevel.Fine };

            var geometry = roof.get_Geometry(options);
            if (geometry == null)
                return boundaryPoints;

            foreach (var geomObj in geometry)
            {
                if (geomObj is Solid solid)
                {
                    foreach (Edge edge in solid.Edges)
                    {
                        var curve = edge.AsCurve();
                        if (curve != null)
                        {
                            boundaryPoints.Add(new XYZ(curve.GetEndPoint(0).X, curve.GetEndPoint(0).Y, 0));
                            boundaryPoints.Add(new XYZ(curve.GetEndPoint(1).X, curve.GetEndPoint(1).Y, 0));
                        }
                    }
                }
            }

            return boundaryPoints.Distinct(new XYZComparer()).ToList();
        }

        private class XYZComparer : IEqualityComparer<XYZ>
        {
            public bool Equals(XYZ x, XYZ y) => x.DistanceTo(y) < Tolerance;

            public int GetHashCode(XYZ obj) =>
                obj.X.GetHashCode() ^ obj.Y.GetHashCode() ^ obj.Z.GetHashCode();
        }
    }
}