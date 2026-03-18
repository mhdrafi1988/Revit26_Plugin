using Autodesk.Revit.DB;
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
                // Use feet internally (Revit units)
                double minDistance = 3.28084; // 1 meter in feet

                p1 = uidoc.Selection.PickPoint("Pick first point");
                p2 = uidoc.Selection.PickPoint("Pick second point (far from first)");

                double dist = p1.DistanceTo(p2);
                if (dist < minDistance)
                {
                    TaskDialog.Show("Error", $"Points too close. Minimum distance is 1 meter ({minDistance:F2} feet).");
                    return false;
                }

                return true;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // User cancelled - return false silently
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}