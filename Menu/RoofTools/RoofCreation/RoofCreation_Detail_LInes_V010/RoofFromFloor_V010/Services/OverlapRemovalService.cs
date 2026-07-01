// ==================================================
// File: OverlapRemovalService.cs
// ==================================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.RoofFromFloor.V010.Services
{
    public static class OverlapRemovalService
    {
        private const double Tolerance = 0.0328084; // 1cm in feet

        /// <summary>
        /// Removes overlapping curves keeping the longest.
        /// Lines use collinearity check. Arcs/splines/ellipses are compared
        /// by type and sampled midpoint — never reduced to a chord.
        /// </summary>
        public static List<Curve> RemoveOverlapsKeepLongest(List<Curve> curves)
        {
            if (curves == null || curves.Count == 0)
                return new List<Curve>();

            var groups    = new List<List<Curve>>();
            var processed = new bool[curves.Count];

            for (int i = 0; i < curves.Count; i++)
            {
                if (processed[i]) continue;

                var group = new List<Curve> { curves[i] };

                for (int j = i + 1; j < curves.Count; j++)
                {
                    if (processed[j]) continue;

                    if (AreOverlapping(curves[i], curves[j]))
                    {
                        group.Add(curves[j]);
                        processed[j] = true;
                    }
                }

                groups.Add(group);
                processed[i] = true;
            }

            var result = new List<Curve>();
            foreach (var group in groups)
            {
                result.Add(group.Count == 1
                    ? group[0]
                    : group.OrderByDescending(c => c.Length).First());
            }

            return result;
        }

        /// <summary>
        /// Dispatches to the correct overlap check based on curve type.
        /// Curves of different types are never considered overlapping.
        /// </summary>
        private static bool AreOverlapping(Curve a, Curve b)
        {
            if (a.GetType() != b.GetType())
                return false;

            if (a is Line)
                return AreCollinearAndOverlapping2D(a, b);

            if (a is Arc arcA && b is Arc arcB)
                return AreArcsOverlapping(arcA, arcB);

            // For splines and ellipses: use Revit's own intersection engine.
            // Overlap = Subset or Superset (one contains the other).
            var result = a.Intersect(b);
            return result == SetComparisonResult.Subset
                || result == SetComparisonResult.Superset
                || result == SetComparisonResult.Equal;
        }

        /// <summary>
        /// Two arcs truly overlap only when they share the same circle geometry
        /// AND their angular spans share more than a single endpoint.
        /// Adjacent semicircles (touching at exactly one point) are NOT overlaps.
        /// </summary>
        private static bool AreArcsOverlapping(Arc a, Arc b)
        {
            // Must be in the same plane (same Z within tolerance)
            if (Math.Abs(a.Center.Z - b.Center.Z) > Tolerance)
                return false;

            // Centers must match in XY
            XYZ ca = new XYZ(a.Center.X, a.Center.Y, 0);
            XYZ cb = new XYZ(b.Center.X, b.Center.Y, 0);
            if (ca.DistanceTo(cb) > Tolerance)
                return false;

            // Radii must match
            if (Math.Abs(a.Radius - b.Radius) > Tolerance)
                return false;

            // Angular spans must share more than a single point.
            // Use a positive overlap threshold so that two arcs that merely
            // touch at a shared endpoint (e.g. two semicircles of a circle)
            // are treated as adjacent segments, not duplicates.
            double a0 = a.GetEndParameter(0), a1 = a.GetEndParameter(1);
            double b0 = b.GetEndParameter(0), b1 = b.GetEndParameter(1);

            if (a0 > a1) (a0, a1) = (a1, a0);
            if (b0 > b1) (b0, b1) = (b1, b0);

            double overlapStart = Math.Max(a0, b0);
            double overlapEnd   = Math.Min(a1, b1);

            // Shared span must be greater than tolerance — a point of contact is not an overlap
            return (overlapEnd - overlapStart) > Tolerance;
        }

        private static bool AreCollinearAndOverlapping2D(Curve a, Curve b)
        {
            XYZ a0 = new XYZ(a.GetEndPoint(0).X, a.GetEndPoint(0).Y, 0);
            XYZ a1 = new XYZ(a.GetEndPoint(1).X, a.GetEndPoint(1).Y, 0);
            XYZ b0 = new XYZ(b.GetEndPoint(0).X, b.GetEndPoint(0).Y, 0);
            XYZ b1 = new XYZ(b.GetEndPoint(1).X, b.GetEndPoint(1).Y, 0);

            XYZ dirA = (a1 - a0).Normalize();
            XYZ dirB = (b1 - b0).Normalize();

            double dot = dirA.DotProduct(dirB);
            if (Math.Abs(Math.Abs(dot) - 1.0) > Tolerance)
                return false;

            if (!IsPointOnLine2D(b0, a0, dirA))
                return false;

            double t_a0 = 0, t_a1 = (a1 - a0).DotProduct(dirA);
            double t_b0 = (b0 - a0).DotProduct(dirA);
            double t_b1 = (b1 - a0).DotProduct(dirA);

            if (t_a1 < t_a0) (t_a0, t_a1) = (t_a1, t_a0);
            if (t_b1 < t_b0) (t_b0, t_b1) = (t_b1, t_b0);

            return !(t_a1 < t_b0 - Tolerance || t_b1 < t_a0 - Tolerance);
        }

        private static bool IsPointOnLine2D(XYZ point, XYZ lineStart, XYZ lineDir)
        {
            XYZ pointDir = point - lineStart;
            double cross  = Math.Abs(lineDir.X * pointDir.Y - lineDir.Y * pointDir.X);
            return cross < Tolerance;
        }
    }
}
