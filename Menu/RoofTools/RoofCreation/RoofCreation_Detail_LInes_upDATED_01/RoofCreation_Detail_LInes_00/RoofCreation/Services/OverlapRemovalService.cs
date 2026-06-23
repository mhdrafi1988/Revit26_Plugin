// ==================================================
// File: OverlapRemovalService.cs
// ==================================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.RoofFromFloor.Services
{
    public static class OverlapRemovalService
    {
        private const double Tolerance = 0.0328084; // 1cm in feet

        /// <summary>
        /// Performs 3-pass aggressive overlap removal to ensure no duplicates remain
        /// </summary>
        public static List<Curve> AggressiveOverlapRemoval(List<Curve> curves)
        {
            if (curves == null || curves.Count == 0)
                return new List<Curve>();

            var working = new List<Curve>(curves);
            int previousCount = 0;
            int pass = 1;

            // Keep processing until no more changes (max 3 passes)
            while (working.Count != previousCount && pass <= 3)
            {
                previousCount = working.Count;
                working = RemoveOverlapsPass(working);
                pass++;
            }

            return working;
        }

        private static List<Curve> RemoveOverlapsPass(List<Curve> curves)
        {
            var result = new List<Curve>();
            var curvesCopy = new List<Curve>(curves);

            // Create a dictionary to track which curves have been processed
            var processed = new bool[curvesCopy.Count];

            for (int i = 0; i < curvesCopy.Count; i++)
            {
                if (processed[i]) continue;

                var current = curvesCopy[i];
                var overlappingGroup = new List<Curve> { current };
                var overlappingIndices = new List<int> { i };

                // Find all curves that overlap with current
                for (int j = i + 1; j < curvesCopy.Count; j++)
                {
                    if (processed[j]) continue;

                    if (AreCurvesOverlapping2D(current, curvesCopy[j]))
                    {
                        overlappingGroup.Add(curvesCopy[j]);
                        overlappingIndices.Add(j);
                    }
                }

                // If we found overlaps, keep only the longest
                if (overlappingGroup.Count > 1)
                {
                    var longest = overlappingGroup.OrderByDescending(c => c.Length).First();
                    result.Add(longest);

                    // Mark all as processed
                    foreach (var idx in overlappingIndices)
                    {
                        processed[idx] = true;
                    }
                }
                else
                {
                    result.Add(current);
                    processed[i] = true;
                }
            }

            // Second pass: Check for any remaining overlaps in the result
            return RemoveRemainingOverlaps(result);
        }

        private static List<Curve> RemoveRemainingOverlaps(List<Curve> curves)
        {
            var result = new List<Curve>();
            var toRemove = new HashSet<Curve>();

            for (int i = 0; i < curves.Count; i++)
            {
                if (toRemove.Contains(curves[i])) continue;

                for (int j = i + 1; j < curves.Count; j++)
                {
                    if (toRemove.Contains(curves[j])) continue;

                    if (AreCurvesOverlapping2D(curves[i], curves[j]))
                    {
                        // Keep the longer one, mark the shorter for removal
                        if (curves[i].Length >= curves[j].Length)
                        {
                            toRemove.Add(curves[j]);
                        }
                        else
                        {
                            toRemove.Add(curves[i]);
                            break; // Current curve will be removed, move to next
                        }
                    }
                }
            }

            // Add all curves that weren't marked for removal
            for (int i = 0; i < curves.Count; i++)
            {
                if (!toRemove.Contains(curves[i]))
                {
                    result.Add(curves[i]);
                }
            }

            return result;
        }

        private static bool AreCurvesOverlapping2D(Curve a, Curve b)
        {
            // Get 2D projections
            XYZ a0 = new XYZ(a.GetEndPoint(0).X, a.GetEndPoint(0).Y, 0);
            XYZ a1 = new XYZ(a.GetEndPoint(1).X, a.GetEndPoint(1).Y, 0);
            XYZ b0 = new XYZ(b.GetEndPoint(0).X, b.GetEndPoint(0).Y, 0);
            XYZ b1 = new XYZ(b.GetEndPoint(1).X, b.GetEndPoint(1).Y, 0);

            // Quick bounding box check
            double aMinX = Math.Min(a0.X, a1.X);
            double aMaxX = Math.Max(a0.X, a1.X);
            double aMinY = Math.Min(a0.Y, a1.Y);
            double aMaxY = Math.Max(a0.Y, a1.Y);

            double bMinX = Math.Min(b0.X, b1.X);
            double bMaxX = Math.Max(b0.X, b1.X);
            double bMinY = Math.Min(b0.Y, b1.Y);
            double bMaxY = Math.Max(b0.Y, b1.Y);

            // If bounding boxes don't intersect, curves cannot overlap
            if (aMaxX < bMinX - Tolerance || aMinX > bMaxX + Tolerance ||
                aMaxY < bMinY - Tolerance || aMinY > bMaxY + Tolerance)
            {
                return false;
            }

            // Check if curves are collinear
            XYZ dirA = (a1 - a0).Normalize();
            XYZ dirB = (b1 - b0).Normalize();

            // Check if directions are parallel (within tolerance)
            double cross = Math.Abs(dirA.X * dirB.Y - dirA.Y * dirB.X);
            if (cross > Tolerance)
                return false; // Not parallel

            // Check if b0 lies on line A
            if (!IsPointOnLine2D(b0, a0, dirA))
                return false; // Not collinear

            // Check for actual overlap using parameter projection
            double t_a0 = 0;
            double t_a1 = (a1 - a0).DotProduct(dirA);
            double t_b0 = (b0 - a0).DotProduct(dirA);
            double t_b1 = (b1 - a0).DotProduct(dirA);

            // Ensure t_a0 < t_a1
            if (t_a1 < t_a0)
            {
                double temp = t_a0;
                t_a0 = t_a1;
                t_a1 = temp;
            }

            // Ensure t_b0 < t_b1
            if (t_b1 < t_b0)
            {
                double temp = t_b0;
                t_b0 = t_b1;
                t_b1 = temp;
            }

            // Check if the intervals overlap
            bool intervalsOverlap = !(t_a1 + Tolerance < t_b0 || t_b1 + Tolerance < t_a0);

            // Also check if endpoints are very close (within tolerance)
            bool endpointsClose =
                a0.DistanceTo(b0) < Tolerance ||
                a0.DistanceTo(b1) < Tolerance ||
                a1.DistanceTo(b0) < Tolerance ||
                a1.DistanceTo(b1) < Tolerance;

            return intervalsOverlap || endpointsClose;
        }

        private static bool IsPointOnLine2D(XYZ point, XYZ lineStart, XYZ lineDir)
        {
            XYZ pointDir = point - lineStart;

            // Cross product should be near zero for collinear points
            double cross = Math.Abs(lineDir.X * pointDir.Y - lineDir.Y * pointDir.X);

            // Also check if point is within the line segment bounds
            double dot = pointDir.DotProduct(lineDir);
            double lineLengthSq = lineDir.DotProduct(lineDir);

            bool withinBounds = dot >= -Tolerance && dot <= lineLengthSq + Tolerance;

            return cross < Tolerance && withinBounds;
        }

        /// <summary>
        /// Debug method to visualize what's being removed
        /// </summary>
        public static Dictionary<string, List<Curve>> DebugOverlapRemoval(List<Curve> curves)
        {
            var result = new Dictionary<string, List<Curve>>();
            var kept = new List<Curve>();
            var removed = new List<Curve>();

            var curvesCopy = new List<Curve>(curves);
            var processed = new bool[curvesCopy.Count];

            for (int i = 0; i < curvesCopy.Count; i++)
            {
                if (processed[i]) continue;

                var current = curvesCopy[i];
                var overlapping = new List<Curve> { current };
                var overlappingIndices = new List<int> { i };

                for (int j = i + 1; j < curvesCopy.Count; j++)
                {
                    if (processed[j]) continue;

                    if (AreCurvesOverlapping2D(current, curvesCopy[j]))
                    {
                        overlapping.Add(curvesCopy[j]);
                        overlappingIndices.Add(j);
                    }
                }

                if (overlapping.Count > 1)
                {
                    var longest = overlapping.OrderByDescending(c => c.Length).First();
                    kept.Add(longest);

                    foreach (var curve in overlapping)
                    {
                        if (curve != longest)
                            removed.Add(curve);
                    }

                    foreach (var idx in overlappingIndices)
                    {
                        processed[idx] = true;
                    }
                }
                else
                {
                    kept.Add(current);
                    processed[i] = true;
                }
            }

            result["Kept"] = kept;
            result["Removed"] = removed;
            return result;
        }
    }
}