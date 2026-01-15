using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.RoofTag_V73.Helpers
{
    internal static partial class GeometryHelper
    {
        public static List<XYZ> BuildRoofBoundaryXY(RoofBase roof)
        {
            List<XYZ> pts = new();
            GeometryElement geo = roof.get_Geometry(new Options());

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

        public static XYZ AdjustForBoundaryCollisions(
            XYZ start,
            XYZ end,
            List<XYZ> boundary)
        {
            return end;
        }
    }
}
