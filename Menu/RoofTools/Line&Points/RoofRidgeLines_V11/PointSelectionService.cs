using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V11.Services
{
    public static class PointSelectionService
    {
        public static bool PickTwoFarPoints(UIDocument uidoc, out XYZ p1, out XYZ p2)
        {
            p1 = uidoc.Selection.PickPoint("Pick first point");
            p2 = uidoc.Selection.PickPoint("Pick second point");

            double min = UnitUtils.ConvertToInternalUnits(1, UnitTypeId.Meters);
            return p1.DistanceTo(p2) >= min;
        }
    }
}
