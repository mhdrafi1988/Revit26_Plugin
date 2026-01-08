using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.RRLPV4.Services
{
    public static class GeometryDetailService // Renamed to avoid conflict
    {
        public static DetailCurve CreateDetailLine(
            Document doc,
            View view,
            XYZ p1,
            XYZ p2,
            GraphicsStyle style)
        {
            Line line = Line.CreateBound(p1, p2);
            DetailCurve curve = doc.Create.NewDetailCurve(view, line);
            if (style != null)
                curve.LineStyle = style;
            return curve;
        }

        public static IList<DetailCurve> CreatePerpendicularLines(
            Document doc,
            View view,
            XYZ p1,
            XYZ p2,
            int divisions,
            GraphicsStyle style)
        {
            List<DetailCurve> curves = new();

            XYZ dir = (p2 - p1).Normalize();
            XYZ perp = new XYZ(-dir.Y, dir.X, 0);

            double spacing = p1.DistanceTo(p2) / divisions;

            for (int i = 1; i < divisions; i++)
            {
                XYZ mid = p1 + dir * spacing * i;
                XYZ a = mid - perp * 5;
                XYZ b = mid + perp * 5;

                curves.Add(CreateDetailLine(doc, view, a, b, style));
            }

            return curves;
        }
    }
}
