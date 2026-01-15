// ==================================
// File: RoofProfileExtractionService.cs
// ==================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    public class RoofProfileExtractionService
    {
        private readonly LoggingService _log;

        public RoofProfileExtractionService(LoggingService log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public IList<Line> ExtractProfileLines(Element roof)
        {
            var result = new List<Line>();

            var options = new Options
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = false
            };

            GeometryElement geom = roof.get_Geometry(options);
            if (geom == null)
                return result;

            foreach (GeometryObject obj in geom)
            {
                if (obj is not Solid solid || solid.Faces.IsEmpty)
                    continue;

                foreach (Face face in solid.Faces)
                {
                    if (face is not PlanarFace pf)
                        continue;

                    // Only roof top faces
                    if (pf.FaceNormal.Z < 0.4)
                        continue;

                    foreach (EdgeArray loop in pf.EdgeLoops)
                    {
                        foreach (Edge edge in loop)
                        {
                            if (edge.AsCurve() is not Line raw)
                                continue;

                            XYZ a = raw.GetEndPoint(0);
                            XYZ b = raw.GetEndPoint(1);

                            XYZ p1 = a.Z >= b.Z ? a : b;
                            XYZ p2 = a.Z >= b.Z ? b : a;

                            if (p1.DistanceTo(p2) < 1e-6)
                                continue;

                            result.Add(Line.CreateBound(p1, p2));
                        }
                    }
                }
            }

            _log.Info($"Profile lines extracted: {result.Count}");
            return result;
        }
    }
}
