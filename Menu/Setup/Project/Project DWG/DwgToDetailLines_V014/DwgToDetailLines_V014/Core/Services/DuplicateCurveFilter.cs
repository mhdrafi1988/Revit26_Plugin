// ==============================================
// File: DuplicateCurveFilter.cs
// Layer: Core/Services
// ADDED in V014: DWGs often carry the same line drawn two or more times on
// one layer; only the first copy needs a detail line.
// ==============================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.DwgToDetailLines.V014.Core.Services
{
    /// <summary>
    /// Drops exact duplicate curves: same curve type, same end points (in either
    /// direction) and, for non-lines, the same mid point, within a tolerance.
    /// Partly overlapping lines are kept.
    /// </summary>
    public static class DuplicateCurveFilter
    {
        /// <summary>
        /// Returns <paramref name="curves"/> without exact duplicates, keeping the first copy.
        /// Unbound curves are always kept.
        /// </summary>
        /// <param name="tolerance">Points closer than this (feet) are treated as equal.</param>
        /// <param name="removed">Number of duplicates dropped.</param>
        public static List<Curve> RemoveDuplicates(IEnumerable<Curve> curves, double tolerance, out int removed)
        {
            removed = 0;
            var result = new List<Curve>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            double q = tolerance > 0 ? tolerance : 1e-6;

            foreach (Curve c in curves)
            {
                if (!c.IsBound)
                {
                    result.Add(c);
                    continue;
                }

                if (seen.Add(Key(c, q)))
                    result.Add(c);
                else
                    removed++;
            }

            return result;
        }

        private static string Key(Curve c, double q)
        {
            string a = Point(c.GetEndPoint(0), q);
            string b = Point(c.GetEndPoint(1), q);
            if (string.CompareOrdinal(a, b) > 0)
                (a, b) = (b, a);

            // A line is fixed by its end points; arcs and splines also need a
            // point between them (two arcs can share both ends).
            string mid = c is Line ? string.Empty : Point(c.Evaluate(0.5, true), q);
            return $"{c.GetType().Name}|{a}|{b}|{mid}";
        }

        private static string Point(XYZ p, double q) =>
            $"{Math.Round(p.X / q)},{Math.Round(p.Y / q)},{Math.Round(p.Z / q)}";
    }
}
