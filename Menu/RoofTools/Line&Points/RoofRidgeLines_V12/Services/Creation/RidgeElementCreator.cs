using Autodesk.Revit.DB;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V12.Models;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V12.Services.Geometry;
using System.Collections.Generic;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V12.Services.Creation
{
    public class RidgeElementCreator : IRidgeElementCreator
    {
        private readonly IRidgeGeometryCalculator _calc;
        private readonly IRidgeIntersectionFinder _finder;

        public RidgeElementCreator(
            IRidgeGeometryCalculator calc,
            IRidgeIntersectionFinder finder)
        {
            _calc = calc;
            _finder = finder;
        }

        public DetailLine CreateBaseLine(RoofRidgeContext c)
        {
            return c.Document.Create.NewDetailCurve(
                c.View,
                Line.CreateBound(c.StartPoint, c.EndPoint)) as DetailLine;
        }

        public IList<DetailLine> CreatePerpendicularLines(RoofRidgeContext c)
        {
            var lines = new List<DetailLine>();
            XYZ dir = _calc.GetDirection(c.StartPoint, c.EndPoint);
            XYZ perp = _calc.GetPerpendicular(dir);
            XYZ mid = (c.StartPoint + c.EndPoint) / 2;

            Line ray = Line.CreateBound(
                mid - perp * 50000,
                mid + perp * 50000);

            foreach (XYZ hit in _finder.FindIntersections(c.Roof, ray))
            {
                lines.Add(
                    c.Document.Create.NewDetailCurve(
                        c.View,
                        Line.CreateBound(mid, hit)) as DetailLine);
            }

            return lines;
        }

        public int AddShapePoints(
            RoofRidgeContext c,
            IList<DetailLine> lines)
        {
            var editor = c.Roof.GetSlabShapeEditor();
            editor.Enable();

            double step =
                UnitUtils.ConvertToInternalUnits(
                    c.PointIntervalMeters,
                    UnitTypeId.Meters);

            int count = 0;
            foreach (var dl in lines)
            {
                var ln = dl.GeometryCurve as Line;
                int steps = (int)(ln.Length / step);

                for (int i = 0; i <= steps; i++)
                {
                    editor.AddPoint(
                        ln.GetEndPoint(0) + ln.Direction * step * i);
                    count++;
                }
            }
            return count;
        }
    }
}
