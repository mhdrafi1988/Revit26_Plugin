// ==================================
// File: FinalCreaseFilterService.cs
// Namespace: Revit26_Plugin.CreaserAdv_V004_00
// ==================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CreaserAdv_V004_00.Services
{
    /// <summary>
    /// Removes any 2-D crease line that geometrically overlaps a roof profile
    /// (boundary) line, using collinearity + 1-D interval overlap.
    /// </summary>
    public class FinalCreaseFilterService
    {
        private readonly LoggingService _log;

        // 10 mm in Revit internal units (feet)
        private const double Tol = 0.0328084;

        public FinalCreaseFilterService(LoggingService log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public IList<Line> Filter(
            IList<Line> creaseLines2d,
            IList<Line> profileLines2d)
        {
            var result = new List<Line>();

            foreach (Line crease in creaseLines2d)
            {
                if (!profileLines2d.Any(p => OverlapsProfile(crease, p)))
                    result.Add(crease);
            }

            _log.Info($"Final creases kept: {result.Count} / {creaseLines2d.Count}");
            return result;
        }

        private bool OverlapsProfile(Line crease, Line profile)
        {
            XYZ c1 = crease.GetEndPoint(0),  c2 = crease.GetEndPoint(1);
            XYZ p1 = profile.GetEndPoint(0), p2 = profile.GetEndPoint(1);

            XYZ vc = (c2 - c1).Normalize();
            XYZ vp = (p2 - p1).Normalize();

            if (vc.CrossProduct(vp).GetLength() > 1e-6) return false;
            if (DistancePointToLine(c1, p1, p2) > Tol)  return false;

            double[] cRange = Project(c1, c2, vp);
            double[] pRange = Project(p1, p2, vp);

            return RangesOverlap(cRange[0], cRange[1], pRange[0], pRange[1]);
        }

        private static double DistancePointToLine(XYZ p, XYZ a, XYZ b)
        {
            XYZ ab = b - a, ap = p - a;
            return ap.CrossProduct(ab).GetLength() / ab.GetLength();
        }

        private static double[] Project(XYZ p1, XYZ p2, XYZ axis)
        {
            double d1 = p1.DotProduct(axis), d2 = p2.DotProduct(axis);
            return new[] { Math.Min(d1, d2), Math.Max(d1, d2) };
        }

        private static bool RangesOverlap(double aMin, double aMax, double bMin, double bMax)
            => aMax >= bMin - Tol && bMax >= aMin - Tol;
    }
}
