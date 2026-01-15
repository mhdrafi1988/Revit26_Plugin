// ==================================
// File: LineProjectionService.cs
// ==================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    public class LineProjectionService
    {
        private readonly LoggingService _log;

        public LineProjectionService(LoggingService log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public IList<Line> ProjectToPlan(
            IList<Line> lines3d,
            ViewPlan view)
        {
            var result = new List<Line>();
            double z = view.GenLevel.Elevation;

            foreach (Line l in lines3d)
            {
                XYZ a = l.GetEndPoint(0);
                XYZ b = l.GetEndPoint(1);

                XYZ p1 = new XYZ(a.X, a.Y, z);
                XYZ p2 = new XYZ(b.X, b.Y, z);

                if (p1.DistanceTo(p2) < 1e-6)
                    continue;

                result.Add(Line.CreateBound(p1, p2));
            }

            _log.Info($"Lines projected to plan: {result.Count}");
            return result;
        }
    }
}
