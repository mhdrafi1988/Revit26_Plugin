using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CreaserAdv_V00.Services.Geometry
{
    /// <summary>
    /// Ensures that at any plan corner point,
    /// only ONE drain path exists.
    /// The kept path is the shortest one.
    /// </summary>
    public static class CornerDrainPathFilterService
    {
        // 5 mm tolerance in feet
        private const double POINT_TOL = 0.0164042;

        public static IList<Line> KeepShortestPerCorner(
            IList<Line> planLines)
        {
            if (planLines == null || planLines.Count == 0)
                return planLines;

            // Group by start point (corner)
            var groups = planLines
                .GroupBy(l => QuantizePoint(l.GetEndPoint(0)));

            var result = new List<Line>();

            foreach (var group in groups)
            {
                // Choose shortest line from this corner
                Line shortest =
                    group
                        .OrderBy(l => l.Length)
                        .First();

                result.Add(shortest);
            }

            return result;
        }

        // ------------------------------------
        // Helpers
        // ------------------------------------

        private static XYZ QuantizePoint(XYZ p)
        {
            return new XYZ(
                Quantize(p.X),
                Quantize(p.Y),
                0);
        }

        private static double Quantize(double v)
        {
            return Math.Round(v / POINT_TOL) * POINT_TOL;
        }
    }
}
