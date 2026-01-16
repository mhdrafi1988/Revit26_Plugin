using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Revit22_Plugin.SectionPlacer.Services
{
    /// <summary>
    /// Handles Revit sheet creation and sizing.
    /// </summary>
    public class SheetService
    {
        private readonly Document _doc;
        private readonly UIApplication _uiapp;

        public SheetService(Document doc, UIApplication uiapp)
        {
            _doc = doc;
            _uiapp = uiapp;
        }

        /// <summary>
        /// Creates a new Revit sheet using the specified title block.
        /// </summary>
        public ViewSheet CreateSheet(FamilySymbol titleBlock, int counter)
        {
            if (titleBlock == null)
            {
                TaskDialog.Show("Error", "No title block selected for sheet creation.");
                return null;
            }

            if (!titleBlock.IsActive)
                titleBlock.Activate();

            var sheet = ViewSheet.Create(_doc, titleBlock.Id);
            sheet.Name = $"AUTO-PLACED-{counter:000}";

            // ⚠️ Do NOT activate sheet here (inside transaction not allowed)
            // The handler will set active sheet after commit

            return sheet;
        }

        /// <summary>
        /// Returns the sheet width and height in Revit internal units (feet).
        /// </summary>
        public (double Width, double Height) GetSheetSize(ViewSheet sheet)
        {
            if (sheet == null || sheet.Outline == null)
                return (0, 0);

            var outline = sheet.Outline;
            return (outline.Max.U - outline.Min.U, outline.Max.V - outline.Min.V);
        }
    }
}
