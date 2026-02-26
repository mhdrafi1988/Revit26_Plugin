using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V00.Services.Geometry
{
    /// <summary>
    /// Extracts crease-like segments from curved opening boundaries
    /// on top roof faces by tessellating curved edges.
    /// </summary>
    public static class CurvedCreaseExtractionHelper
    {
        // Max segment length ~300 mm (adjust as needed)
        private const double MAX_SEG_LEN = 1.0; // feet

        public static IList<Line> ExtractCurvedCreaseSegments(
            Element roof)
        {
            var result = new List<Line>();

            var geom = roof.get_Geometry(new Options());
            if (geom == null)
                return result;

            foreach (GeometryObject obj in geom)
            {
                if (obj is not Solid solid)
                    continue;

                foreach (Face face in solid.Faces)
                {
                    if (face is not PlanarFace pf)
                        continue;

                    // Only top faces
                    if (pf.FaceNormal.Z < 0.4)
                        continue;

                    foreach (EdgeArray loop in pf.EdgeLoops)
                    {
                        foreach (Edge edge in loop)
                        {
                            Curve curve = edge.AsCurve();

                            // Skip straight edges (already handled elsewhere)
                            if (curve is Line)
                                continue;

                            // Tessellate curved edge
                            IList<XYZ> pts = curve.Tessellate();

                            for (int i = 0; i < pts.Count - 1; i++)
                            {
                                XYZ a = pts[i];
                                XYZ b = pts[i + 1];

                                if (a.DistanceTo(b) > MAX_SEG_LEN)
                                    continue;

                                // Normalize HIGH ? LOW
                                XYZ high = a.Z >= b.Z ? a : b;
                                XYZ low = a.Z >= b.Z ? b : a;

                                result.Add(Line.CreateBound(high, low));
                            }
                        }
                    }
                }
            }

            return result;
        }
    }
}
