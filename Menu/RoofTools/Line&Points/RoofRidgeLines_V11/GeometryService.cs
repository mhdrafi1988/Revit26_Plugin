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

            double r = 50000; // Large enough to intersect roof boundaries
            Line ray = Line.CreateBound(mid - perp * r, mid + perp * r);

            foreach (Curve c in GetRoofEdges(roof))
            {
                if (c.Intersect(ray, out IntersectionResultArray arr) == SetComparisonResult.Overlap)
                {
                    // Get ALL intersection points (not just the first one)
                    foreach (IntersectionResult ir in arr)
                    {
                        XYZ hit = ir.XYZPoint;
                        result.Add(doc.Create.NewDetailCurve(view,
                            Line.CreateBound(mid, hit)) as DetailLine);
                    }
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

            int count = 0;

            // Add points ONLY at the intersection points with roof profiles
            foreach (var dl in lines)
            {
                Line ln = dl.GeometryCurve as Line;
                if (ln == null) continue;

                // The end point of each perpendicular line is on the roof edge (intersection point)
                XYZ intersectionPoint = ln.GetEndPoint(1); // The far end point (on roof edge)
                editor.AddPoint(intersectionPoint);
                count++;
            }

            return count;
        }

        private static IEnumerable<Curve> GetRoofEdges(RoofBase roof)
        {
            var geo = roof.get_Geometry(new Options());
            if (geo == null) yield break;

            foreach (var obj in geo)
            {
                if (obj is Solid solid)
                {
                    foreach (Edge edge in solid.Edges)
                    {
                        yield return edge.AsCurve();
                    }
                }
                else if (obj is GeometryInstance inst)
                {
                    var instGeo = inst.GetInstanceGeometry();
                    foreach (var instSolid in instGeo.OfType<Solid>())
                    {
                        foreach (Edge edge in instSolid.Edges)
                        {
                            yield return edge.AsCurve();
                        }
                    }
                }
            }
        }
    }
}