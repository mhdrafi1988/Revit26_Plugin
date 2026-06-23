// ==================================================
// File: OverlapRemovalService.cs
// ==================================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.RoofFromFloor.V008.V007.Services
{
    public static class OverlapRemovalService
    {
        private const double Tolerance = 0.0328084; // 1cm in feet

        /// <summary>
        /// Removes overlapping curves in 2D (XY plane), keeping the longest curve when overlaps occur
        /// </summary>
        public static List<Curve> RemoveOverlapsKeepLongest(List<Curve> curves)
        {
            if (curves == null || curves.Count == 0)
                return new List<Curve>();

            // Group curves by their direction (parallel or collinear)
            var groups = new List<List<Curve>>();
            var processed = new bool[curves.Count];

            for (int i = 0; i < curves.Count; i++)
            {
                if (processed[i]) continue;

                var group = new List<Curve> { curves[i] };

                for (int j = i + 1; j < curves.Count; j++)
                {
                    if (processed[j]) continue;

                    if (AreCollinearAndOverlapping2D(curves[i], curves[j]))
                    {
                        group.Add(curves[j]);
                        processed[j] = true;
                    }
                }

                groups.Add(group);
                processed[i] = true;
            }

            // Process each group to keep only the longest curve
            var result = new List<Curve>();
            foreach (var group in groups)
            {
                if (group.Count == 1)
                {
                    result.Add(group[0]);
                }
                else
                {
                    // Keep the longest curve from overlapping group
                    var longest = group.OrderByDescending(c => c.Length).First();
                    result.Add(longest);
                }
            }

            return result;
        }

        /// <summary>
        /// Checks if two curves are collinear and overlapping in 2D (XY plane)
        /// </summary>
        private static bool AreCollinearAndOverlapping2D(Curve a, Curve b)
        {
            // Get 2D endpoints (ignore Z)
            XYZ a0 = new XYZ(a.GetEndPoint(0).X, a.GetEndPoint(0).Y, 0);
            XYZ a1 = new XYZ(a.GetEndPoint(1).X, a.GetEndPoint(1).Y, 0);
            XYZ b0 = new XYZ(b.GetEndPoint(0).X, b.GetEndPoint(0).Y, 0);
            XYZ b1 = new XYZ(b.GetEndPoint(1).X, b.GetEndPoint(1).Y, 0);

            // Check if curves are collinear (parallel and on same line)
            XYZ dirA = (a1 - a0).Normalize();
            XYZ dirB = (b1 - b0).Normalize();

            // Check if directions are parallel (dot product close to 1 or -1)
            double dot = dirA.DotProduct(dirB);
            if (Math.Abs(Math.Abs(dot) - 1.0) > Tolerance)
                return false; // Not parallel

            // Check if curves are on the same line by testing if b0 is on line A
            if (!IsPointOnLine2D(b0, a0, dirA))
                return false; // Not collinear

            // Check for overlap using 1D projection on the direction vector
            double t_a0 = 0;
            double t_a1 = (a1 - a0).DotProduct(dirA);
            double t_b0 = (b0 - a0).DotProduct(dirA);
            double t_b1 = (b1 - a0).DotProduct(dirA);

            // Normalize to ensure t_a0 < t_a1
            if (t_a1 < t_a0)
            {
                double temp = t_a0;
                t_a0 = t_a1;
                t_a1 = temp;
            }

            // Normalize b to ensure t_b0 < t_b1
            if (t_b1 < t_b0)
            {
                double temp = t_b0;
                t_b0 = t_b1;
                t_b1 = temp;
            }

            // Check if intervals overlap
            return !(t_a1 < t_b0 - Tolerance || t_b1 < t_a0 - Tolerance);
        }

        /// <summary>
        /// Checks if a point lies on an infinite line in 2D
        /// </summary>
        private static bool IsPointOnLine2D(XYZ point, XYZ lineStart, XYZ lineDir)
        {
            XYZ pointDir = point - lineStart;

            // Cross product magnitude should be near zero for collinear points
            double cross = Math.Abs(lineDir.X * pointDir.Y - lineDir.Y * pointDir.X);
            return cross < Tolerance;
        }

        /// <summary>
        /// Alternative method: Uses Revit's built-in intersection detection
        /// </summary>
        public static List<Curve> RemoveOverlapsUsingIntersection(List<Curve> curves)
        {
            var result = new List<Curve>();
            var curvesList = new List<Curve>(curves);

            while (curvesList.Any())
            {
                var current = curvesList.First();
                curvesList.RemoveAt(0);

                var overlapping = new List<Curve> { current };

                // Find all curves that intersect or overlap with current
                for (int i = curvesList.Count - 1; i >= 0; i--)
                {
                    var other = curvesList[i];

                    var intersection = current.Intersect(other);
                    if (intersection == SetComparisonResult.Overlap ||
                        intersection == SetComparisonResult.Subset ||
                        intersection == SetComparisonResult.Superset)
                    {
                        overlapping.Add(other);
                        curvesList.RemoveAt(i);
                    }
                }

                // If we have multiple overlapping curves, keep the longest
                if (overlapping.Count > 1)
                {
                    var longest = overlapping.OrderByDescending(c => c.Length).First();
                    result.Add(longest);
                }
                else
                {
                    result.Add(current);
                }
            }

            return result;
        }
    }
}