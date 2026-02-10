// File: SheetCreationService.cs
using Autodesk.Revit.DB;

namespace Revit26_Plugin.APUS_V314.Services
{
    public class SheetCreationService
    {
        private readonly Document _doc;

        public SheetCreationService(Document doc)
        {
            _doc = doc;
        }

        public ViewSheet Create(FamilySymbol titleBlock, int index)
        {
            if (!titleBlock.IsActive)
                titleBlock.Activate();

            var sheet = ViewSheet.Create(_doc, titleBlock.Id);
            sheet.Name = $"APUS-{index:000}";
            sheet.SheetNumber = $"AP{index:000}";
            return sheet;
        }
    }
}