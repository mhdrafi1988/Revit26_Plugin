using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    public class DrainDetectionService
    {
        private const double DrainZTolerance = 1e-3;

        public IList<XYZ> DetectDrains(
            IList<Face> topFaces,
            LoggingService log)
        {
            if (topFaces == null || topFaces.Count == 0)
                throw new ArgumentException("No top faces provided.");

            log.Info("Detecting drains from lowest Z vertices.");

            var vertices = new List<XYZ>();

            foreach (var face in topFaces)
            {
                var mesh = face.Triangulate();
                foreach (var v in mesh.Vertices)
                    vertices.Add(v);
            }

            if (vertices.Count == 0)
            {
                log.Warning("No vertices found on top faces.");
                return new List<XYZ>();
            }

            double minZ = vertices.Min(v => v.Z);

            var drains =
                vertices
                    .Where(v => Math.Abs(v.Z - minZ) <= DrainZTolerance)
                    .GroupBy(v =>
                        new XYZ(
                            Quantize(v.X),
                            Quantize(v.Y),
                            0))
                    .Select(g => g.First())
                    .ToList();

            log.Info($"Drains detected: {drains.Count}");
            return drains;
        }

        private static double Quantize(double value)
        {
            double t = GeometryTolerance.Point;
            return Math.Round(value / t) * t;
        }
    }
}
