using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.RoofTag_V006.Helpers
{
    internal static partial class RoofTagGeometryHelper
    {
        /// <summary>
        /// Returns the unique slab shape editor vertices for the roof.
        /// Deduplication is by XY position (same tolerance as placement dedup:
        /// grouped at 1e-6 ft grid, highest Z kept per group).
        /// </summary>
        public static List<XYZ> GetExactShapeVertices(RoofBase roof)
        {
            if (roof == null)
                throw new ArgumentNullException(nameof(roof));

            SlabShapeEditor editor = roof.GetSlabShapeEditor();
            if (editor == null || !editor.IsEnabled)
                return new List<XYZ>();

            const double tol = 1e-6;

            return editor.SlabShapeVertices
                .Cast<SlabShapeVertex>()
                .GroupBy(v => (
                    Math.Round(v.Position.X / tol),
                    Math.Round(v.Position.Y / tol)))
                .Select(g => g.OrderByDescending(v => v.Position.Z).First().Position)
                .ToList();
        }

        /// <summary>
        /// Builds a flat XY boundary point list from the roof solid edges.
        /// Used for collision / outward direction checks.
        /// </summary>
        public static List<XYZ> BuildRoofBoundaryXY(RoofBase roof)
        {
            List<XYZ> pts = new();
            Options opt = new();
            GeometryElement geo = roof.get_Geometry(opt);

            foreach (GeometryObject obj in geo)
            {
                if (obj is Solid solid)
                {
                    foreach (Edge e in solid.Edges)
                    {
                        Curve c = e.AsCurve();
                        pts.Add(new XYZ(c.GetEndPoint(0).X, c.GetEndPoint(0).Y, 0));
                        pts.Add(new XYZ(c.GetEndPoint(1).X, c.GetEndPoint(1).Y, 0));
                    }
                }
            }
            return pts;
        }
    }
}
