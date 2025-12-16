using Autodesk.Revit.DB;
using Revit22_Plugin.RoofTagV4.Models;
using System;

namespace Revit22_Plugin.RoofTagV4.Helpers
{
    public static class BendCalculationServiceV4
    {
        public static void ComputeBendAndEndPoints(
            RoofLoopsModel geom,
            XYZ basePoint,
            XYZ outwardDir,
            double bendOffsetFt,
            double endOffsetFt,
            double angleDeg,
            bool bendInward,
            out XYZ bend,
            out XYZ end)
        {
            // inward direction ALWAYS correct now
            XYZ inwardDir = outwardDir.Negate();

            XYZ useDir = bendInward ? inwardDir : outwardDir;

            bend = basePoint + (useDir * bendOffsetFt);

            double angleRad = angleDeg * Math.PI / 180.0;

            XYZ horiz =
                new XYZ(useDir.X * System.Math.Cos(angleRad),
                        useDir.Y * System.Math.Cos(angleRad),
                        useDir.Z);

            end = bend + horiz * endOffsetFt;
        }
    }
}
