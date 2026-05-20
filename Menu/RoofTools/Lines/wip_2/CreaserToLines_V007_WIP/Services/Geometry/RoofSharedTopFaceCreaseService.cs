using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V00.Services.Geometry
{
    /// <summary>
    /// Extracts roof crease lines shared by top roof faces.
    /// IMPORTANT:
    /// - Crease direction is normalized HIGH ? LOW here.
    /// - No other service may change endpoint order.
    /// </summary>
    public sealed class RoofSharedTopFaceCreaseService
    {
        public IList<Line> ExtractNormalizedCreaseLines(Element roof)
        {
            var result = new List<Line>();

            if (roof == null)
                return result;

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
                if (obj is not Solid solid || solid.Edges.IsEmpty)
                    continue;

                foreach (Edge edge in solid.Edges)
                {
                    if (edge.AsCurve() is not Line raw)
                        continue;

                    Face f0 = edge.GetFace(0);
                    Face f1 = edge.GetFace(1);

                    // Skip side faces
                    if (IsSideFace(f0) || IsSideFace(f1))
                        continue;

                    // Must touch at least one top face
                    if (!IsTopFace(f0) && !IsTopFace(f1))
                        continue;

                    XYZ a = raw.GetEndPoint(0);
                    XYZ b = raw.GetEndPoint(1);

                    if (a.DistanceTo(b) < 1e-6)
                        continue;

                    // ?? Normalize HIGH ? LOW (slope direction)
                    XYZ high = a.Z >= b.Z ? a : b;
                    XYZ low = a.Z >= b.Z ? b : a;

                    result.Add(Line.CreateBound(high, low));
                }
            }

            return result;
        }

        private static bool IsTopFace(Face face)
        {
            return face is PlanarFace pf && pf.FaceNormal.Z >= 0.4;
        }

        private static bool IsSideFace(Face face)
        {
            return face is PlanarFace pf && Math.Abs(pf.FaceNormal.Z) < 0.2;
        }
    }
}
