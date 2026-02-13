using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V313.Helpers;
using Revit26_Plugin.APUS_V313.Models;

namespace Revit26_Plugin.APUS_V313.Services
{
    public static class SheetLayoutService
    {
        public static SheetPlacementArea Calculate(
            Document doc,
            FamilySymbol titleBlock,
            double leftMarginMm,
            double rightMarginMm,
            double topMarginMm,
            double bottomMarginMm)
        {
            // Create a temporary sheet to get its outline
            using (Transaction tempTx = new Transaction(doc, "Temp Sheet for Calculation"))
            {
                tempTx.Start();

                if (!titleBlock.IsActive)
                    titleBlock.Activate();

                var tempSheet = ViewSheet.Create(doc, titleBlock.Id);
                var outline = tempSheet.Outline;

                // Delete temporary sheet
                doc.Delete(tempSheet.Id);

                tempTx.RollBack(); // Roll back the transaction - sheet was only for measurement

                // Convert margins to internal units
                double left = UnitConversionHelper.MmToFeet(leftMarginMm);
                double right = UnitConversionHelper.MmToFeet(rightMarginMm);
                double top = UnitConversionHelper.MmToFeet(topMarginMm);
                double bottom = UnitConversionHelper.MmToFeet(bottomMarginMm);

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
}