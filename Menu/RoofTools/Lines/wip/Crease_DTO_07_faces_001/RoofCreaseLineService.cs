// ==================================
// File: RoofCreaseLineService.cs
// ==================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    /// <summary>
    /// Converts normalized 3D roof crease lines into plan-view-safe 2D lines.
    /// p1 is guaranteed to originate from the higher-Z 3D endpoint.
    /// </summary>
    public class RoofCreaseLineService
    {
        private readonly LoggingService _log;

        public RoofCreaseLineService(LoggingService log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public IList<Line> GetCreaseLines(
            Element roof,
            ViewPlan view)
        {
            var lines = new List<Line>();

            if (view?.GenLevel == null)
            {
                _log.Warning("Invalid plan view or missing level.");
                return lines;
            }

            double viewZ = view.GenLevel.Elevation;

            var options = new Options
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = false
            };

            GeometryElement geom = roof.get_Geometry(options);
            if (geom == null)
            {
                _log.Warning("Roof geometry not found.");
                return lines;
            }

            foreach (GeometryObject obj in geom)
            {
                if (obj is not Solid solid || solid.Faces.IsEmpty)
                    continue;

                foreach (Face face in solid.Faces)
                {
                    if (face is not PlanarFace pf)
                        continue;

                    if (pf.FaceNormal.Z < 0.4)
                        continue;

                    foreach (EdgeArray loop in pf.EdgeLoops)
                    {
                        foreach (Edge edge in loop)
                        {
                            if (edge.AsCurve() is not Line line3d)
                                continue;

                            XYZ a3 = line3d.GetEndPoint(0);
                            XYZ b3 = line3d.GetEndPoint(1);

                            // 🔑 Normalize again (defensive)
                            XYZ p1 = a3.Z >= b3.Z ? a3 : b3;
                            XYZ p2 = a3.Z >= b3.Z ? b3 : a3;

                            // Project into plan view plane
                            XYZ p1_2d = new XYZ(p1.X, p1.Y, viewZ);
                            XYZ p2_2d = new XYZ(p2.X, p2.Y, viewZ);

                            if (p1_2d.DistanceTo(p2_2d) < 1e-6)
                                continue;

                            lines.Add(Line.CreateBound(p1_2d, p2_2d));
                        }
                    }
                }
            }

            _log.Info($"Plan-safe crease lines created (Z-ordered): {lines.Count}");
            return lines;
        }
    }
}
