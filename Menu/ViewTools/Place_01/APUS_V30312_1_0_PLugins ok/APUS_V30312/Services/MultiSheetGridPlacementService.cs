using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V312.Models;
using Revit26_Plugin.APUS_V312.ViewModels;
using System.Collections.Generic;

namespace Revit26_Plugin.APUS_V312.Services
{
    /// <summary>
    /// Orchestrates adaptive grid placement across multiple sheets.
    /// </summary>
    public class MultiSheetGridPlacementService
    {
        private readonly Document _doc;
        private readonly SheetCreationService _sheetCreator;
        private readonly GridPlacementService _placer;

        public MultiSheetGridPlacementService(Document doc)
        {
            _doc = doc;
            _sheetCreator = new SheetCreationService(doc);
            _placer = new GridPlacementService(doc);
        }

        public void Place(
            IList<SectionItemViewModel> sortedSections,
            FamilySymbol titleBlock,
            SheetPlacementArea area,
            double horizontalGapMm,
            double verticalGapMm,
            AutoPlaceSectionsViewModel vm,
            ref int placedCount,
            ref int failedCount,
            ref HashSet<string> sheetNumbers)
        {
            if (sortedSections == null || sortedSections.Count == 0)
            {
                vm.LogWarning("No sections to place.");
                return;
            }

            // Calculate grid width + column count
            if (!GridLayoutCalculationService.TryCalculate(
                sortedSections,
                area,
                out double cellWidth,
                out int columns))
            {
                vm.LogError("Grid calculation failed.");
                return;
            }

            vm.LogInfo(
                $"GRID: Columns={columns}, CellWidth(ft)={cellWidth:F2}");

            int index = 0;
            int detailIndex = 0;
            int sheetIndex = 1;

            while (index < sortedSections.Count)
            {
                if (vm.Progress.IsCancelled)
                {
                    vm.LogWarning("Placement cancelled.");
                    break;
                }

                ViewSheet sheet =
                    _sheetCreator.Create(titleBlock, sheetIndex++);

                vm.LogInfo($"CREATED SHEET: {sheet.SheetNumber}");
                sheetNumbers.Add(sheet.SheetNumber);

                int placed =
                    _placer.PlaceOnSheet(
                        sheet,
                        sortedSections,
                        index,
                        area,
                        cellWidth,
                        horizontalGapMm,
                        verticalGapMm,
                        columns,
                        vm,
                        ref detailIndex,
                        ref placedCount,
                        ref failedCount);

                if (placed == 0)
                {
                    vm.LogWarning(
                        $"No views fit on sheet {sheet.SheetNumber}.");
                    // Remove empty sheet
                    _doc.Delete(sheet.Id);
                    sheetNumbers.Remove(sheet.SheetNumber);
                    break;
                }

                index += placed;
            }

            vm.LogInfo("Grid placement completed.");
        }
    }
}