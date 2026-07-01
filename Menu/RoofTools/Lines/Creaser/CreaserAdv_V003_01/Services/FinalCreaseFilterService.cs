// ==================================
// File: FinalCreaseFilterService.cs
// Namespace: Revit26_Plugin.CreaserAdv_V003_01
// ==================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CreaserAdv_V003_01.Services
{
    /// <summary>
    /// Removes any 2-D crease line that geometrically overlaps a roof profile
    /// (boundary) line, using collinearity + 1-D interval overlap.
    /// Call this as a post-processing step after plan projection when you need
    /// to strip perimeter edges that survived solid-topology filtering.
    /// </summary>
    public class FinalCreaseFilterService
    {
        private readonly LoggingService _log;

        // 10 mm expressed in Revit internal units (feet)
        private const double Tol = 0.0328084;

        public FinalCreaseFilterService(LoggingService log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>
        /// Returns only those creases that do NOT overlap any profile line.
        /// </summary>
        public IList<Line> Filter(
            IList<Line> creaseLines2d,
            IList<Line> profileLines2d)
        {
            var result = new List<Line>();

            foreach (Line crease in creaseLines2d)
            {
                bool matchesProfile = profileLines2d.Any(p => OverlapsProfile(crease, p));

                if (!matchesProfile)
                    result.Add(crease);
            }

            _log.Info($"Final creases kept: {result.Count} / {creaseLines2d.Count}");
            return result;
        }

        // -------------------------------------------------
        // Geometry helpers
        // -------------------------------------------------

        private bool OverlapsProfile(Line crease, Line profile)
        {
            XYZ c1 = crease.GetEndPoint(0),  c2 = crease.GetEndPoint(1);
            XYZ p1 = profile.GetEndPoint(0), p2 = profile.GetEndPoint(1);

            XYZ vc = (c2 - c1).Normalize();
            XYZ vp = (p2 - p1).Normalize();

            // 1. Must be parallel
            if (vc.CrossProduct(vp).GetLength() > 1e-6)
                return false;

            // 2. Must be collinear (c1 lies on the infinite line through profile)
            if (DistancePointToLine(c1, p1, p2) > Tol)
                return false;

            // 3. Projected intervals must overlap
            double[] cRange = Project(c1, c2, vp);
            double[] pRange = Project(p1, p2, vp);

            return RangesOverlap(cRange[0], cRange[1], pRange[0], pRange[1]);
        }

        private static double DistancePointToLine(XYZ p, XYZ a, XYZ b)
        {
            XYZ ab = b - a;
            XYZ ap = p - a;
            return ap.CrossProduct(ab).GetLength() / ab.GetLength();
        }

        private static double[] Project(XYZ p1, XYZ p2, XYZ axis)
        {
            double d1 = p1.DotProduct(axis);
            double d2 = p2.DotProduct(axis);
            return new[] { Math.Min(d1, d2), Math.Max(d1, d2) };
        }

        private static bool RangesOverlap(double aMin, double aMax, double bMin, double bMax)
            => aMax >= bMin - Tol && bMax >= aMin - Tol;
    }
}
