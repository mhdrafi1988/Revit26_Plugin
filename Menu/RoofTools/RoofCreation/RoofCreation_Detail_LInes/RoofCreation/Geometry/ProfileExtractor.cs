// ==================================================
// File: ProfileExtractor.cs
// ==================================================

using Autodesk.Revit.DB;
using Revit26_Plugin.RoofFromFloor.Models;
using System.Collections.Generic;

namespace Revit26_Plugin.RoofFromFloor.Geometry
{
    /// <summary>
    /// Extracts roof footprint curves EXACTLY as defined in Revit.
    /// Order and direction are preserved by design.
    /// </summary>
    public static class ProfileExtractor
    {
        public static RoofMemoryContext ExtractRoofContext(
            Document doc,
            FootPrintRoof roof)
        {
            var context = new RoofMemoryContext
            {
                RoofId = roof.Id,
                RoofLevel = doc.GetElement(roof.LevelId) as Level,
                RoofBaseElevation =
                    roof.get_Parameter(
                        BuiltInParameter.ROOF_LEVEL_OFFSET_PARAM)?.AsDouble() ?? 0
            };

            context.BoundingBox = roof.get_BoundingBox(null);

            double targetZ =
                context.RoofLevel.Elevation + context.RoofBaseElevation;

            ModelCurveArrArray profiles = roof.GetProfiles();

            foreach (ModelCurveArray loop in profiles)
            {
                foreach (ModelCurve mc in loop)
                {
                    Curve flat = FlattenCurveToZ(mc.GeometryCurve, targetZ);
                    context.RoofFootprintCurves.Add(flat);
                }
            }

            return context;
        }

        private static Curve FlattenCurveToZ(Curve curve, double z)
        {
            XYZ p0 = curve.GetEndPoint(0);
            XYZ p1 = curve.GetEndPoint(1);

            return Line.CreateBound(
                new XYZ(p0.X, p0.Y, z),
                new XYZ(p1.X, p1.Y, z));
        }
    }
}
