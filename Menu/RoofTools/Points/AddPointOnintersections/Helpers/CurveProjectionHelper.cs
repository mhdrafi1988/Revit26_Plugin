using System;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.AddPointOnintersections.Helpers
{
    public static class CurveProjectionHelper
    {
        private const double Epsilon = 1e-9;

        public static Curve CreateFlattenedCurve(Curve source)
        {
            if (source is Line line)
            {
                XYZ p0 = line.GetEndPoint(0);
                XYZ p1 = line.GetEndPoint(1);

                return Line.CreateBound(
                    new XYZ(p0.X, p0.Y, 0.0),
                    new XYZ(p1.X, p1.Y, 0.0));
            }

            if (source is Arc arc)
            {
                XYZ start = arc.GetEndPoint(0);
                XYZ end = arc.GetEndPoint(1);
                XYZ mid = arc.Evaluate(0.5, true);

                return Arc.Create(
                    new XYZ(start.X, start.Y, 0.0),
                    new XYZ(end.X, end.Y, 0.0),
                    new XYZ(mid.X, mid.Y, 0.0));
            }

            throw new NotSupportedException(
                $"Unsupported curve type '{source.GetType().Name}'. Only Line and Arc are supported.");
        }

        public static bool IsAlmostEqualXY(XYZ a, XYZ b, double tolerance)
        {
            return Math.Abs(a.X - b.X) <= tolerance &&
                   Math.Abs(a.Y - b.Y) <= tolerance;
        }

        public static string ToReadable(XYZ p)
        {
            return $"({p.X:0.###}, {p.Y:0.###}, {p.Z:0.###})";
        }
    }
}