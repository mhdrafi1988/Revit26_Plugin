using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V08.Services
{
    public static class ReferencePicker
    {
        public static bool PickTwoPoints(UIDocument uidoc, out XYZ point1, out XYZ point2)
        {
            point1 = null;
            point2 = null;

            try
            {
                point1 = uidoc.Selection.PickPoint("Pick first point");
                if (point1 == null) return false;

                point2 = uidoc.Selection.PickPoint("Pick second point");
                return point2 != null;
            }
            catch
            {
                return false;
            }
        }
    }
}