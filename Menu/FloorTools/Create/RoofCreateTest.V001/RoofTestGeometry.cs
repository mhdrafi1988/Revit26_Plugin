using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.RoofCreateTest.V001
{
    public static class RoofTestGeometry
    {
        public const double SideMm = 10000.0; // 10 m × 10 m

        public static CurveLoop BuildCurveLoop(double sideMm = SideMm)
        {
            double half = UnitUtils.ConvertToInternalUnits(sideMm / 2.0, UnitTypeId.Millimeters);
            XYZ[] pts = new XYZ[]
            {
                new XYZ(-half, -half, 0),
                new XYZ( half, -half, 0),
                new XYZ( half,  half, 0),
                new XYZ(-half,  half, 0)
            };
            var curves = new List<Curve>();
            for (int i = 0; i < 4; i++)
                curves.Add(Line.CreateBound(pts[i], pts[(i + 1) % 4]));
            return CurveLoop.Create(curves);
        }

        public static CurveArray BuildCurveArray(double sideMm = SideMm)
        {
            var loop = BuildCurveLoop(sideMm);
            var array = new CurveArray();
            foreach (Curve c in loop)
                array.Append(c);
            return array;
        }
    }
}