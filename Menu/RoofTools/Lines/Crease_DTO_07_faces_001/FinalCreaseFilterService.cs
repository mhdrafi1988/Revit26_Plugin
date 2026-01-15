// ==================================
// File: FinalCreaseFilterService.cs
// ==================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    /// <summary>
    /// FINAL authority filter.
    /// Removes ANY crease (regardless of origin)
    /// if it geometrically overlaps ANY roof profile line in plan.
    /// </summary>
    public class FinalCreaseFilterService
    {
        private readonly LoggingService _log;

        // 10 mm in feet
        private const double TOL = 0.0328084;

        public FinalCreaseFilterService(LoggingService log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>
        /// Returns only creases that do NOT match roof profiles.
        /// </summary>
        public IList<Line> FilterFinalCreases(
            IList<Line> creaseLines2d,
            IList<Line> profileLines2d)
        {
            var finalResult = new List<Line>();

            foreach (Line crease in creaseLines2d)
            {
                bool matchesProfile = profileLines2d.Any(profile =>
                    OverlapsProfile(crease, profile));

                if (!matchesProfile)
                    finalResult.Add(crease);
            }

            _log.Info(
                $"Final creases kept: {finalResult.Count} / {creaseLines2d.Count}");

            return finalResult;
        }

        /// <summary>
        /// True if crease and profile are parallel, colinear, and overlap in projection.
        /// </summary>
        private bool OverlapsProfile(Line crease, Line profile)
        {
            XYZ c1 = crease.GetEndPoint(0);
            XYZ c2 = crease.GetEndPoint(1);
            XYZ p1 = profile.GetEndPoint(0);
            XYZ p2 = profile.GetEndPoint(1);

            // 1. Parallel check
            XYZ vc = (c2 - c1).Normalize();
            XYZ vp = (p2 - p1).Normalize();

            if (vc.CrossProduct(vp).GetLength() > 1e-6)
                return false;

            // 2. Colinear check (distance to infinite line)
            if (DistancePointToLine(c1, p1, p2) > TOL)
                return false;

            // 3. 1D overlap check (projection onto profile axis)
            double[] cRange = Project(c1, c2, vp);
            double[] pRange = Project(p1, p2, vp);

            return RangesOverlap(cRange[0], cRange[1], pRange[0], pRange[1]);
        }

        private double DistancePointToLine(XYZ p, XYZ a, XYZ b)
        {
            XYZ ab = b - a;
            XYZ ap = p - a;
            return ap.CrossProduct(ab).GetLength() / ab.GetLength();
        }

        private double[] Project(XYZ p1, XYZ p2, XYZ axis)
        {
            double d1 = p1.DotProduct(axis);
            double d2 = p2.DotProduct(axis);
            return new[]
            {
                Math.Min(d1, d2),
                Math.Max(d1, d2)
            };
        }

        private bool RangesOverlap(
            double aMin, double aMax,
            double bMin, double bMax)
        {
            return aMax >= bMin - TOL && bMax >= aMin - TOL;
        }
    }
}
