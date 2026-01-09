using Autodesk.Revit.DB;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V12.Services.Geometry
{
    public interface IRidgeGeometryCalculator
    {
        XYZ GetDirection(XYZ p1, XYZ p2);
        XYZ GetPerpendicular(XYZ direction);
    }
}
