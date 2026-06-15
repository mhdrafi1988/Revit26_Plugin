using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.DwgSymbolicConverter_V03.Helpers
{
    public static class CurveCleanupHelper
    {
        public static List<Curve> SnapEndpoints(
            IEnumerable<Curve> curves,
            double tolerance)
        {
            var endpoints = curves
                .SelectMany(c => new[] { c.GetEndPoint(0), c.GetEndPoint(1) })
                .ToList();

            XYZ Snap(XYZ p)
            {
                foreach (var q in endpoints)
                    if (p.DistanceTo(q) <= tolerance)
                        return q;
                return p;
            }

            var result = new List<Curve>();

            foreach (var c in curves)
            {
                XYZ p0 = Snap(c.GetEndPoint(0));
                XYZ p1 = Snap(c.GetEndPoint(1));

                if (!p0.IsAlmostEqualTo(p1))
                    result.Add(Line.CreateBound(p0, p1));
            }

            return result;
        }

        public static bool FormsClosedLoop(
            IEnumerable<Curve> curves,
            double tolerance)
        {
            var points = curves
                .SelectMany(c => new[] { c.GetEndPoint(0), c.GetEndPoint(1) })
                .ToList();

            return points.All(p =>
                points.Count(q => p.DistanceTo(q) <= tolerance) == 2);
        }
    }
}
