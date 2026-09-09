// File: SheetLayoutService.cs
using Autodesk.Revit.DB;
using Revit26_Plugin.APUS.V322.Helpers;
using Revit26_Plugin.APUS.V322.Models;

namespace Revit26_Plugin.APUS.V322.Services
{
    /// <summary>
    /// Computes the usable placement rectangle on a sheet by subtracting
    /// margins exactly once. This was already correct in V320 — the
    /// double-margin bug lived in the (now deleted) placement algorithms,
    /// which re-subtracted margins on top of this area. EvenGapPlacementService
    /// takes the result of this method as-is and never subtracts again.
    /// </summary>
    public static class SheetLayoutService
    {
        public static SheetPlacementArea Calculate(
            ViewSheet sheet,
            double leftMm,
            double rightMm,
            double topMm,
            double bottomMm)
        {
            double left = UnitConversionHelper.MmToFeet(leftMm);
            double right = UnitConversionHelper.MmToFeet(rightMm);
            double top = UnitConversionHelper.MmToFeet(topMm);
            double bottom = UnitConversionHelper.MmToFeet(bottomMm);

            var outline = sheet.Outline;

            double width = outline.Max.U - outline.Min.U - left - right;
            double height = outline.Max.V - outline.Min.V - top - bottom;

            var origin = new XYZ(
                outline.Min.U + left,
                outline.Max.V - top,
                0);

            return new SheetPlacementArea(origin, width, height);
        }
    }
}
