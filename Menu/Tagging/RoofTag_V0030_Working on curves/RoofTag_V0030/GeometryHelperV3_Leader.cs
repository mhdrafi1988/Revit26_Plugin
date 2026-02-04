using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.RoofTag_V03.Helpers
{
    internal static partial class GeometryHelperV3
    {
        public static XYZ ComputeBendPoint(
            XYZ origin,
            XYZ centroid,
            double offset,
            bool inward)
        {
            XYZ dir = (centroid - origin).Normalize();
            if (!inward) dir = -dir;
            return origin + dir * offset;
        }

        public static XYZ ComputeEndPointWithAngle(
            XYZ origin,
            XYZ bend,
            double angleDeg,
            double offset,
            XYZ outwardDir,
            bool inward)
        {
            double angleRad = angleDeg * Math.PI / 180.0;
            XYZ baseDir = (bend - origin).Normalize();

            XYZ rotated = new XYZ(
                baseDir.X * Math.Cos(angleRad) - baseDir.Y * Math.Sin(angleRad),
                baseDir.X * Math.Sin(angleRad) + baseDir.Y * Math.Cos(angleRad),
                0);

            if (!inward)
                rotated = -rotated;

            return bend + rotated.Normalize() * offset;
        }

        public static XYZ GetOutwardDirectionForPoint(
            XYZ point,
            List<XYZ> boundary,
            XYZ centroid)
        {
            return (point - centroid).Normalize();
        }
    }
}
