using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CreaserAdv_V00.Services.Geometry
{
    /// <summary>
    /// Static geometry helper.
    /// Removes crease lines that overlap roof profile lines in plan.
    /// Pure math – no logging, no state.
    /// </summary>
    public static class CreaseProfileFilterService
    {
        // 10 mm in feet
        private const double TOL = 0.0328084;

        public static IList<Line> RemoveProfileOverlaps(
            IList<Line> creaseLines2d,
            IList<Line> profileLines2d)
        {
            var result = new List<Line>();

            foreach (Line crease in creaseLines2d)
            {
                bool overlaps =
                    profileLines2d.Any(profile =>
                        LinesOverlapColinear(crease, profile));

                if (!overlaps)
                    result.Add(crease);
            }

            return result;
        }

        // =======================
        // Geometry helpers
        // =======================

        private static bool LinesOverlapColinear(Line a, Line b)
        {
            XYZ a1 = a.GetEndPoint(0);
            XYZ a2 = a.GetEndPoint(1);
            XYZ b1 = b.GetEndPoint(0);
            XYZ b2 = b.GetEndPoint(1);

            // Parallel check
            XYZ va = (a2 - a1).Normalize();
            XYZ vb = (b2 - b1).Normalize();
            if (va.CrossProduct(vb).GetLength() > 1e-6)
                return false;

            // Colinearity check
            if (DistancePointToLine(a1, b1, b2) > TOL)
                return false;

            // Interval overlap check (1D projection)
            double[] ar = ProjectOntoAxis(a1, a2, va);
            double[] br = ProjectOntoAxis(b1, b2, va);

            return ar[1] >= br[0] - TOL &&
                   br[1] >= ar[0] - TOL;
        }

        private static double DistancePointToLine(XYZ p, XYZ a, XYZ b)
        {
            XYZ ab = b - a;
            XYZ ap = p - a;
            return ap.CrossProduct(ab).GetLength() / ab.GetLength();
        }

        private static double[] ProjectOntoAxis(XYZ p1, XYZ p2, XYZ axis)
        {
            double d1 = p1.DotProduct(axis);
            double d2 = p2.DotProduct(axis);

            return new[]
            {
                Math.Min(d1, d2),
                Math.Max(d1, d2)
            };
        }
    }
}
