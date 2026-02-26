// ==================================
// File: CreaseLineProjectionService.cs
// ==================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    /// <summary>
    /// Projects 3D crease lines into plan-safe 2D detail lines.
    /// </summary>
    public class CreaseLineProjectionService
    {
        private readonly LoggingService _log;
        private const double GeometryTolerancePoint = 0.0001; // Define a tolerance value

        public CreaseLineProjectionService(LoggingService log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public IList<Line> ProjectToPlan(IList<Line> sourceLines)
        {
            var projected = new List<Line>();

            foreach (Line line in sourceLines)
            {
                XYZ p1 = line.GetEndPoint(0);
                XYZ p2 = line.GetEndPoint(1);

                double z = Math.Max(p1.Z, p2.Z);

                XYZ a = new XYZ(p1.X, p1.Y, z);
                XYZ b = new XYZ(p2.X, p2.Y, z);

                if (a.DistanceTo(b) < GeometryTolerancePoint)
                    continue;

                projected.Add(Line.CreateBound(a, b));
            }

            _log.Info($"Projected 2D crease lines: {projected.Count}");
            return projected;
        }
    }
}
