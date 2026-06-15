using Autodesk.Revit.DB;
using Revit26_Plugin.CreaserAdv_V00_701.Services.Logging;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V00_701.Services.Geometry
{
    public class RoofSharedTopFaceCreaseService
    {
        private readonly LoggingService _log;

        public RoofSharedTopFaceCreaseService(LoggingService log)
        {
            _log = log;
        }

        public IList<Line> ExtractSharedTopFaceCreases(Element roof)
        {
            var result = new List<Line>();

            var geom = roof.get_Geometry(new Options());
            if (geom == null) return result;

            foreach (var obj in geom)
            {
                if (obj is not Solid solid) continue;

                foreach (Edge edge in solid.Edges)
                {
                    if (edge.AsCurve() is not Line raw) continue;

                    var f0 = edge.GetFace(0);
                    var f1 = edge.GetFace(1);

                    if (!IsTop(f0) || !IsTop(f1)) continue;

                    var a = raw.GetEndPoint(0);
                    var b = raw.GetEndPoint(1);

                    result.Add(Line.CreateBound(a, b));
                }
            }

            _log.Info($"Creases extracted: {result.Count}");
            return result;
        }

        private static bool IsTop(Face f)
        {
            return f is PlanarFace pf && pf.FaceNormal.Z >= 0.4;
        }
    }
}
