// File: SheetLayoutService.cs
using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V314.Helpers;
using Revit26_Plugin.APUS_V314.Models;
using Revit26_Plugin.APUS_V314.Helpers;

namespace Revit26_Plugin.APUS_V314.Services
{
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