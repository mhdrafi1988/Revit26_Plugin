using System;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.DetailLineClosedLoop.V001.Core.Geometry
{
    /// <summary>
    /// Finds where two (possibly non-overlapping) infinite lines would meet,
    /// for the "extend" half of trim/extend. Detail lines in a single view are
    /// coplanar, so true intersections are exact; skew 3D lines are rejected
    /// via the closest-point gap check.
    /// </summary>
    public static class LineExtensionUtils
    {
        public static XYZ IntersectInfinite(Line a, Line b, double coincidenceTolerance)
        {
            XYZ p1 = a.GetEndPoint(0);
            XYZ d1 = a.Direction.Normalize();
            XYZ p2 = b.GetEndPoint(0);
            XYZ d2 = b.Direction.Normalize();

            XYZ w0 = p1 - p2;
            double dotAA = d1.DotProduct(d1);
            double dotAB = d1.DotProduct(d2);
            double dotBB = d2.DotProduct(d2);
            double dotAW = d1.DotProduct(w0);
            double dotBW = d2.DotProduct(w0);

            double denom = dotAA * dotBB - dotAB * dotAB;
            if (Math.Abs(denom) < 1e-9)
                return null; // parallel

            double s = (dotAB * dotBW - dotBB * dotAW) / denom;
            double t = (dotAA * dotBW - dotAB * dotAW) / denom;

            XYZ pointOnA = p1 + d1 * s;
            XYZ pointOnB = p2 + d2 * t;

            if (pointOnA.DistanceTo(pointOnB) > coincidenceTolerance)
                return null; // skew lines that never actually meet

            return pointOnA;
        }
    }
}
