using Autodesk.Revit.DB;
using Revit26_Plugin.CreaserAdv_V00.Services.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CreaserAdv_V00.Services.Geometry
{
    /// <summary>
    /// Removes boundary creases using polygon containment (midpoint test).
    /// </summary>
    public class RoofCreaseBoundaryFilterService
    {
        private readonly LoggingService _log;

        public RoofCreaseBoundaryFilterService(LoggingService log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public IList<Line> KeepOnlyInternalCreases(
            IList<Line> creaseLines2d,
            IList<IList<XYZ>> outerPolygons2d,
            IList<IList<IList<XYZ>>> holePolygons2d)
        {
            var result = new List<Line>();

            foreach (Line crease in creaseLines2d)
            {
                XYZ mid =
                    (crease.GetEndPoint(0) + crease.GetEndPoint(1)) * 0.5;

                bool insideOuter =
                    outerPolygons2d.Any(p => IsPointInsidePolygon(mid, p));

                bool insideHole =
                    holePolygons2d.Any(hs =>
                        hs.Any(h => IsPointInsidePolygon(mid, h)));

                if (insideOuter && !insideHole)
                    result.Add(crease);
            }

            _log.Info(
                $"Internal creases kept: {result.Count}/{creaseLines2d.Count}");

            return result;
        }

        // Ray casting algorithm
        private bool IsPointInsidePolygon(XYZ p, IList<XYZ> poly)
        {
            bool inside = false;
            int j = poly.Count - 1;

            for (int i = 0; i < poly.Count; i++)
            {
                XYZ pi = poly[i];
                XYZ pj = poly[j];

                if (((pi.Y > p.Y) != (pj.Y > p.Y)) &&
                    (p.X <
                     (pj.X - pi.X) * (p.Y - pi.Y) /
                     (pj.Y - pi.Y + 1e-9) + pi.X))
                {
                    inside = !inside;
                }

                j = i;
            }

            return inside;
        }
    }
}
