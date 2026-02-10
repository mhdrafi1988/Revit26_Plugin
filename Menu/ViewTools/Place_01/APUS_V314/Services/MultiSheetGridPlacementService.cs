// File: MultiSheetGridPlacementService.cs
using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V314.Models;
using Revit26_Plugin.APUS_V314.ViewModels;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V314.Services
{
    /// <summary>
    /// Multi-sheet grid placement orchestrator
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

            try
            {
                // Calculate grid layout
                if (!GridLayoutCalculationService.TryCalculate(
                    sortedSections,
                    area,
                    horizontalGapMm,
                    verticalGapMm,
                    out double cellWidth,
                    out double cellHeight,
                    out int columns,
                    out int rows))
                {
                    vm.LogError("Grid calculation failed.");
                    return;
                }

                vm.LogInfo($"GRID LAYOUT: {columns} columns × {rows} rows, Cell: {cellWidth:F2}×{cellHeight:F2} ft");

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

                    // Create new sheet
                    ViewSheet sheet = _sheetCreator.Create(titleBlock, sheetIndex++);
                    vm.LogInfo($"CREATED SHEET: {sheet.SheetNumber}");
                    sheetNumbers.Add(sheet.SheetNumber);

                    // Place views on current sheet
                    int placed = _placer.PlaceAdaptiveOnSheet(
                        sheet,
                        sortedSections.ToList(),
                        index,
                        area,
                        cellWidth,
                        horizontalGapMm,
                        verticalGapMm,
                        columns,
                        vm,
                        ref detailIndex);

                    if (placed == 0)
                    {
                        vm.LogWarning($"No views fit on sheet {sheet.SheetNumber}");
                        _doc.Delete(sheet.Id);
                        sheetNumbers.Remove(sheet.SheetNumber);
                        break;
                    }

                    index += placed;
                    placedCount += placed;
                }

                vm.LogInfo($"Grid placement completed: {placedCount} placed, {failedCount} failed");
            }
            catch (System.Exception ex)
            {
                vm.LogError($"Grid placement failed: {ex.Message}");
            }
        }
    }
}