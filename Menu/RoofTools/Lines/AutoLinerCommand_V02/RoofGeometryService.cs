// ============================================================
// File: RoofGeometryService.cs
// Namespace: Revit26_Plugin.AutoLiner_V02.Services
// ============================================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.AutoLiner_V02.Services
{
    public static class RoofGeometryService
    {
        public static List<XYZ> GetCornerPoints(Element roof)
        {
            var corners = new List<XYZ>();
            Solid solid = GetRoofSolid(roof);
            if (solid == null) return corners;

            foreach (Edge edge in solid.Edges)
            {
                Curve c = edge.AsCurve();
                XYZ p0 = c.GetEndPoint(0);
                XYZ p1 = c.GetEndPoint(1);

                if (Math.Abs(p0.X - p1.X) < 0.001 &&
                    Math.Abs(p0.Y - p1.Y) < 0.001 &&
                    Math.Abs(p0.Z - p1.Z) > 0.001)
                {
                    AddUnique(corners, p0);
                }
            }

            return corners;
        }

        public static List<XYZ> GetDrainPoints(Element roof)
        {
            var drains = new List<XYZ>();
            Solid solid = GetRoofSolid(roof);
            if (solid == null) return drains;

            PlanarFace topFace = solid.Faces
                .OfType<PlanarFace>()
                .FirstOrDefault(f => f.FaceNormal.IsAlmostEqualTo(XYZ.BasisZ));

            if (topFace == null) return drains;

            Mesh mesh = topFace.Triangulate();
            IList<XYZ> vertices = mesh.Vertices;

            double minZ = vertices.Min(v => v.Z);
            const double tol = 0.01;

            foreach (XYZ v in vertices)
            {
                if (Math.Abs(v.Z - minZ) < tol)
                    AddUnique(drains, v);
            }

            return drains;
        }

        private static Solid GetRoofSolid(Element roof)
        {
            Options opt = new() { DetailLevel = ViewDetailLevel.Fine };
            GeometryElement geo = roof.get_Geometry(opt);

            return geo?
                .OfType<Solid>()
                .FirstOrDefault(s => s.Volume > 0);
        }

        private static void AddUnique(List<XYZ> list, XYZ p)
        {
            if (!list.Any(x => x.IsAlmostEqualTo(p)))
                list.Add(p);
        }
    }
}
