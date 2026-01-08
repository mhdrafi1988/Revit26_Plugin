using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V11.Services
{
    public static class GeometryService
    {
        public static DetailLine CreateDetailLine(Document doc, View view, XYZ p1, XYZ p2)
        {
            Line ln = Line.CreateBound(p1, p2);
            return doc.Create.NewDetailCurve(view, ln) as DetailLine;
        }

        public static List<DetailLine> CreatePerpendicularLines(
            Document doc, View view, RoofBase roof, XYZ p1, XYZ p2)
        {
            var result = new List<DetailLine>();
            XYZ mid = (p1 + p2) / 2;
            XYZ dir = (p2 - p1).Normalize();
            XYZ perp = new XYZ(-dir.Y, dir.X, 0);

            double r = 50000;
            Line ray = Line.CreateBound(mid - perp * r, mid + perp * r);

            foreach (Curve c in GetRoofEdges(roof))
            {
                if (c.Intersect(ray, out IntersectionResultArray arr) == SetComparisonResult.Overlap)
                {
                    XYZ hit = arr.Cast<IntersectionResult>().First().XYZPoint;
                    result.Add(doc.Create.NewDetailCurve(view,
                        Line.CreateBound(mid, hit)) as DetailLine);
                }
            }
            return result;
        }

        public static int AddShapePoints(
            Document doc, RoofBase roof, List<DetailLine> lines, double meters)
        {
            if (!lines.Any()) return 0;
            var editor = roof.GetSlabShapeEditor();
            editor.Enable();

            double step = UnitUtils.ConvertToInternalUnits(meters, UnitTypeId.Meters);
            int count = 0;

            foreach (var dl in lines)
            {
                Line ln = dl.GeometryCurve as Line;
                int steps = (int)(ln.Length / step);

                for (int i = 0; i <= steps; i++)
                {
                    XYZ pt = ln.GetEndPoint(0) + ln.Direction * step * i;
                    editor.AddPoint(pt);
                    count++;
                }
            }
            return count;
        }

        private static IEnumerable<Curve> GetRoofEdges(RoofBase roof)
        {
            var geo = roof.get_Geometry(new Options());
            return geo.OfType<Solid>()
                .SelectMany(s => s.Edges.Cast<Edge>())
                .Select(e => e.AsCurve());
        }
    }
}
