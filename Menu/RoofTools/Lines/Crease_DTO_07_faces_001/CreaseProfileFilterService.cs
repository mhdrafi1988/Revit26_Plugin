// ==================================
// File: CreaseProfileFilterService.cs
// ==================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    /// <summary>
    /// Removes ANY crease that overlaps a roof profile line
    /// (outer or inner), using true colinearity + interval overlap.
    /// This reliably removes ALL boundary creases.
    /// </summary>
    public class CreaseProfileFilterService
    {
        private readonly LoggingService _log;

        // 10 mm in feet
        private const double TOL = 0.0328084;

        public CreaseProfileFilterService(LoggingService log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public IList<Line> RemoveProfileOverlaps(
            IList<Line> creaseLines2d,
            IList<Line> profileLines2d)
        {
            var result = new List<Line>();

            foreach (Line crease in creaseLines2d)
            {
                bool overlapsProfile = profileLines2d.Any(profile =>
                    LinesOverlapColinear(crease, profile));

                if (!overlapsProfile)
                    result.Add(crease);
            }

            _log.Info(
                $"Creases after boundary removal: {result.Count} / {creaseLines2d.Count}");

            return result;
        }

        /// <summary>
        /// True if two lines are parallel, colinear, and overlap in projection.
        /// </summary>
        private bool LinesOverlapColinear(Line a, Line b)
        {
            XYZ a1 = a.GetEndPoint(0);
            XYZ a2 = a.GetEndPoint(1);
            XYZ b1 = b.GetEndPoint(0);
            XYZ b2 = b.GetEndPoint(1);

            // 1. Parallel
            XYZ va = (a2 - a1).Normalize();
            XYZ vb = (b2 - b1).Normalize();
            if (va.CrossProduct(vb).GetLength() > 1e-6)
                return false;

            // 2. Colinear (distance from one point to the other line)
            if (DistancePointToLine(a1, b1, b2) > TOL)
                return false;

            // 3. Project onto axis and test 1D overlap
            double[] aRange = ProjectOntoAxis(a1, a2, va);
            double[] bRange = ProjectOntoAxis(b1, b2, va);

            return RangesOverlap(aRange[0], aRange[1], bRange[0], bRange[1]);
        }

        private double DistancePointToLine(XYZ p, XYZ a, XYZ b)
        {
            XYZ ab = b - a;
            XYZ ap = p - a;
            return ap.CrossProduct(ab).GetLength() / ab.GetLength();
        }

        private double[] ProjectOntoAxis(XYZ p1, XYZ p2, XYZ axis)
        {
            double d1 = p1.DotProduct(axis);
            double d2 = p2.DotProduct(axis);
            return new[]
            {
                Math.Min(d1, d2),
                Math.Max(d1, d2)
            };
        }

        private bool RangesOverlap(double aMin, double aMax, double bMin, double bMax)
        {
            return aMax >= bMin - TOL && bMax >= aMin - TOL;
        }
    }
}
