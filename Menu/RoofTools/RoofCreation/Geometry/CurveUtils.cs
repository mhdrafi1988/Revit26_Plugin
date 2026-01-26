using Autodesk.Revit.DB;
using Revit26_Plugin.RoofFromFloor.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.RoofFromFloor.Geometry
{
    public static class CurveUtils
    {
        public static bool ArePointsClose(XYZ a, XYZ b)
        {
            return a.DistanceTo(b) <= GeometryTolerance.EndpointTolerance;
        }

        public static bool AreCurvesOverlapping(Curve a, Curve b)
        {
            if (a.GetType() != b.GetType())
                return false;

            XYZ a0 = a.GetEndPoint(0);
            XYZ a1 = a.GetEndPoint(1);
            XYZ b0 = b.GetEndPoint(0);
            XYZ b1 = b.GetEndPoint(1);

            return
                (ArePointsClose(a0, b0) && ArePointsClose(a1, b1)) ||
                (ArePointsClose(a0, b1) && ArePointsClose(a1, b0));
        }

        public static XYZ SnapPoint(XYZ source, XYZ target)
        {
            return source.DistanceTo(target) <= GeometryTolerance.EndpointTolerance
                ? target
                : source;
        }
    }
}
