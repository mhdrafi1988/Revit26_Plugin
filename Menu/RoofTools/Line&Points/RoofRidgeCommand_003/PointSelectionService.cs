using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace Revit22_Plugin.RRLPV3.Services
{
    public static class PointSelectionService
    {
        /// <summary>
        /// Prompts user to pick two points far from each other
        /// </summary>
        public static bool PickTwoFarPoints(
            UIDocument uidoc,
            out XYZ point1,
            out XYZ point2,
            double minDistance = 1.0)
        {
            point1 = null;
            point2 = null;

            try
            {
                // First point
                point1 = uidoc.Selection.PickPoint("Pick first point");
                if (point1 == null) return false;

                // Second point
                point2 = uidoc.Selection.PickPoint("Pick second point");
                if (point2 == null) return false;

                // Validate minimum distance
                if (point1.DistanceTo(point2) < minDistance)
                {
                    TaskDialog.Show("Error", $"Points must be at least {minDistance} units apart.");
                    return false;
                }

                return true;
            }
            catch (Exception)
            {
                // User cancelled or error occurred
                return false;
            }
        }
    }
}