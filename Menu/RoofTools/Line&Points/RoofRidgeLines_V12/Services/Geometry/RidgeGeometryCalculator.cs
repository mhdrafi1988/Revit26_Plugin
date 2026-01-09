using Autodesk.Revit.DB;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V12.Services.Geometry
{
    public class RidgeGeometryCalculator : IRidgeGeometryCalculator
    {
        public XYZ GetDirection(XYZ p1, XYZ p2)
            => (p2 - p1).Normalize();

        public XYZ GetPerpendicular(XYZ direction)
            => new XYZ(-direction.Y, direction.X, 0);
    }
}
