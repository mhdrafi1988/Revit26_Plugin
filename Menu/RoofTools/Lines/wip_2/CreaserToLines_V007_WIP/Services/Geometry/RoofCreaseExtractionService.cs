using Autodesk.Revit.DB;
using Revit26_Plugin.CreaserAdv_V00.Services.Logging;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V00.Services.Geometry
{
    /// <summary>
    /// Extracts internal roof creases where BOTH adjacent faces
    /// are top-facing roof planes.
    /// </summary>
    public class RoofCreaseExtractionService
    {
        private readonly LoggingService _log;

        public RoofCreaseExtractionService(LoggingService log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public IList<Line> ExtractInternalCreaseLines(Element roof)
        {
            var result = new List<Line>();

            var options = new Options
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = false
            };

            GeometryElement geom = roof.get_Geometry(options);
            if (geom == null)
            {
                _log.Warning("Roof geometry not found.");
                return result;
            }

            foreach (GeometryObject obj in geom)
            {
                if (obj is not Solid solid || solid.Edges.IsEmpty)
                    continue;

                foreach (Edge edge in solid.Edges)
                {
                    if (edge.AsCurve() is not Line raw)
                        continue;

                    Face f0 = edge.GetFace(0);
                    Face f1 = edge.GetFace(1);

                    if (!IsTopRoofFace(f0) || !IsTopRoofFace(f1))
                        continue;

                    XYZ a = raw.GetEndPoint(0);
                    XYZ b = raw.GetEndPoint(1);

                    if (a.DistanceTo(b) < 1e-6)
                        continue;

                    XYZ p1 = a.Z >= b.Z ? a : b;
                    XYZ p2 = a.Z >= b.Z ? b : a;

                    result.Add(Line.CreateBound(p1, p2));
                }
            }

            _log.Info($"Internal creases extracted: {result.Count}");
            return result;
        }

        private static bool IsTopRoofFace(Face face)
        {
            if (face is not PlanarFace pf)
                return false;

            return pf.FaceNormal.Z >= 0.4;
        }
    }
}
