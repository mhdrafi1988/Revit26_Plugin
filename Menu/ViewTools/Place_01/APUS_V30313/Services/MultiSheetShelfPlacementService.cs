using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V313.Models;
using Revit26_Plugin.APUS_V313.ViewModels;
using System.Collections.Generic;

namespace Revit26_Plugin.APUS_V313.Services
{
    public class MultiSheetShelfPlacementService
    {
        private readonly Document _doc;
        private readonly SheetCreationService _sheetCreator;
        private readonly ShelfPlacementService _placer;

        public MultiSheetShelfPlacementService(Document doc)
        {
            _doc = doc;
            _sheetCreator = new SheetCreationService(doc);
            _placer = new ShelfPlacementService(doc);
        }

        public void Place(
            IList<SectionItemViewModel> orderedSections,
            FamilySymbol titleBlock,
            SheetPlacementArea area,
            double horizontalGapMm,
            double verticalGapMm,
            ref int placedCount,
            ref int failedCount)
        {
            int index = 0;
            int sheetIndex = 1;

            while (index < orderedSections.Count)
            {
                ViewSheet sheet = _sheetCreator.Create(titleBlock, sheetIndex++);

                // Call PlaceOnSheet and get the result object
                var result = _placer.PlaceOnSheet(
                    sheet,
                    orderedSections,
                    index,
                    area,
                    horizontalGapMm,
                    verticalGapMm);

                // Update placedCount and failedCount from the result
                placedCount += result.PlacedCount;
                failedCount += result.FailedCount;

                int placedOnSheet = result.PlacedCount;

                if (placedOnSheet == 0)
                {
                    _doc.Delete(sheet.Id);
                    break;
                }

                index += placedOnSheet;
            }
        }
    }
}
