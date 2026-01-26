using Autodesk.Revit.DB;
using Revit26_Plugin.RoofFromFloor.Models;
using System.Collections.Generic;

namespace Revit26_Plugin.RoofFromFloor.Geometry
{
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
                RoofBaseElevation = roof.get_Parameter(
                    BuiltInParameter.ROOF_LEVEL_OFFSET_PARAM)?.AsDouble() ?? 0
            };

            // ---------------------------
            // Bounding Box (model space)
            // ---------------------------
            context.BoundingBox = roof.get_BoundingBox(null);

            // ---------------------------
            // Footprint Curves
            // ---------------------------
            var it = roof.GetProfiles().ForwardIterator();
            while (it.MoveNext())
            {
                ModelCurveArray curveArray = it.Current as ModelCurveArray;
                foreach (ModelCurve mc in curveArray)
                {
                    Curve flattened = FlattenCurveToZ(
                        mc.GeometryCurve,
                        context.RoofLevel.Elevation + context.RoofBaseElevation);

                    context.RoofFootprintCurves.Add(flattened);
                }
            }

            return context;
        }

        private static Curve FlattenCurveToZ(Curve curve, double z)
        {
            XYZ p0 = curve.GetEndPoint(0);
            XYZ p1 = curve.GetEndPoint(1);

            XYZ fp0 = new XYZ(p0.X, p0.Y, z);
            XYZ fp1 = new XYZ(p1.X, p1.Y, z);

            return Line.CreateBound(fp0, fp1);
        }
    }
}
