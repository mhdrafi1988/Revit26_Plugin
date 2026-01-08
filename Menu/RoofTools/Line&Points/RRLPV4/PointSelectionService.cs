using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.RRLPV4.Services
{
    public static class PointSelectionService
    {
        public static bool PickTwoFarPoints(UIDocument uidoc, out XYZ p1, out XYZ p2)
        {
            p1 = null;
            p2 = null;
            try
            {
                p1 = uidoc.Selection.PickPoint("Pick first point");
                p2 = uidoc.Selection.PickPoint("Pick second point (far from first)");
                double dist = p1.DistanceTo(p2);
                if (dist < 1.0) // Min 1m/3ft
                {
                    TaskDialog.Show("Error", "Points too close (min 1m).");
                    return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}