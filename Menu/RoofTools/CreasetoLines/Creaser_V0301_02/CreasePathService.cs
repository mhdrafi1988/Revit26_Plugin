using Autodesk.Revit.DB;
//using Revit26_Plugin.AutoLiner_V04.Services;
using Revit26_Plugin.Creaser_V32.Helpers;
using Revit26_Plugin.Creaser_V32.Models;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.Creaser_V32.Services
{
    public class CreasePathService
    {
        private readonly UiLogService _log;

        public CreasePathService(UiLogService log)
        {
            _log = log;
        }

        public List<CreasePath> BuildPaths(RoofGeometryData data)
        {
            double minZ = data.ShapePoints.Min(p => p.Z);
            var drains = data.ShapePoints.Where(p => p.Z == minZ).ToList();

            _log.Write($"Drain points: {drains.Count}");

            List<CreasePath> paths = new();

            foreach (var corner in data.CornerPoints)
            {
                var nearestDrain = drains
                    .OrderBy(d => corner.DistanceTo(new XYZ(d.X, d.Y, corner.Z)))
                    .First();

                var relatedCreases = data.Creases
                    .Where(c =>
                        c.GetEndPoint(0).IsAlmostEqualTo(corner) ||
                        c.GetEndPoint(1).IsAlmostEqualTo(nearestDrain))
                    .ToList();

                paths.Add(new CreasePath(corner, nearestDrain, relatedCreases));
            }

            return paths;
        }

        public List<DirectedLine> ConvertToDirectedLines(List<CreasePath> paths)
        {
            List<DirectedLine> lines = new();

            foreach (var path in paths)
            {
                foreach (var curve in path.Curves)
                {
                    XYZ a = curve.GetEndPoint(0);
                    XYZ b = curve.GetEndPoint(1);

                    var p1 = a.Z >= b.Z ? a : b;
                    var p2 = a.Z < b.Z ? a : b;

                    lines.Add(new DirectedLine(p1, p2));
                }
            }

            return lines
                .Distinct()
                .Where(l => !l.IsZeroLength)
                .ToList();
        }
    }
}
