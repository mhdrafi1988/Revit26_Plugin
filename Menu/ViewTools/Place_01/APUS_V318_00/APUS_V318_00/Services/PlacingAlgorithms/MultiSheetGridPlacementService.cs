// File: MultiSheetGridPlacementService.cs
// NEW - Complete implementation
using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V318.ExternalEvents;
using Revit26_Plugin.APUS_V318.Helpers; // Added for UnitConversionHelper
using Revit26_Plugin.APUS_V318.Models;
using Revit26_Plugin.APUS_V318.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V318.Services
{
    /// <summary>
    /// Multi-sheet grid placement service
    /// CRITICAL: Assumes active transaction exists.
    /// </summary>
    public class MultiSheetGridPlacementService
    {
        private readonly Document _doc;
        private readonly SheetCreationService _sheetCreator;
        private readonly GridPlacementService _gridPlacer;

        public MultiSheetGridPlacementService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _sheetCreator = new SheetCreationService(doc);
            _gridPlacer = new GridPlacementService(doc);
        }

        public SectionPlacementHandler.PlacementResult Place(
            SectionPlacementHandler.PlacementContext context,
            List<SectionItemViewModel> sections)
        {
            var result = new SectionPlacementHandler.PlacementResult();

            if (sections == null || !sections.Any())
            {
                context.ViewModel?.LogWarning("No sections to place.");
                result.ErrorMessage = "No sections to place";
                return result;
            }

            try
            {
                // Calculate optimal grid layout for first sheet
                if (!GridLayoutCalculationService.TryCalculate(
                    sections,
                    context.PlacementArea,
                    context.HorizontalGapMm,
                    context.VerticalGapMm,
                    out double cellWidth,
                    out double cellHeight,
                    out int columns,
                    out int rows,
                    context.ViewModel))
                {
                    result.ErrorMessage = "Failed to calculate grid layout";
                    return result;
                }

                var layout = new GridLayoutCalculationService.GridLayout
                {
                    Columns = columns,
                    Rows = rows,
                    CellWidth = cellWidth,
                    CellHeight = cellHeight,
                    HorizontalGap = UnitConversionHelper.MmToFeet(context.HorizontalGapMm),
                    VerticalGap = UnitConversionHelper.MmToFeet(context.VerticalGapMm)
                };

                context.ViewModel?.LogInfo(
                    $"Grid layout: {columns} cols × {rows} rows per sheet, " +
                    $"Cell: {cellWidth:F2}×{cellHeight:F2} ft");

                int sectionIndex = 0;
                int sheetCount = 0;

                while (sectionIndex < sections.Count)
                {
                    if (context.ViewModel?.Progress.IsCancelled == true)
                        break;

                    // Create new sheet
                    string sheetNumber = context.SheetNumberService.GetNextAvailableSheetNumber("GR");
                    context.SheetNumberService.TryReserveSheetNumber(sheetNumber);

                    var sheet = _sheetCreator.Create(context.TitleBlock, sheetNumber, $"Grid-{sheetNumber}");
                    context.ViewModel?.LogInfo($"Created sheet: {sheet.SheetNumber}");

                    // Place views on this sheet
                    int placed = _gridPlacer.PlaceOnSheet(
                        sheet,
                        sections,
                        sectionIndex,
                        context,
                        layout,
                        result);

                    if (placed == 0)
                    {
                        // Remove empty sheet
                        _doc.Delete(sheet.Id);
                        context.ViewModel?.LogWarning($"No views placed on sheet {sheet.SheetNumber}, removing");
                        break;
                    }

                    sectionIndex += placed;
                    result.PlacedCount += placed;
                    result.SheetNumbers.Add(sheet.SheetNumber);
                    sheetCount++;

                    context.ViewModel?.LogInfo(
                        $"Placed {placed} views on sheet {sheet.SheetNumber}, " +
                        $"total: {result.PlacedCount}/{sections.Count}");
                }

                context.ViewModel?.LogInfo(
                    $"Grid placement complete: {result.PlacedCount} views on {sheetCount} sheets");

                return result;
            }
            catch (Exception ex)
            {
                context.ViewModel?.LogError($"Grid placement failed: {ex.Message}");
                result.ErrorMessage = ex.Message;
                return result;
            }
        }
    }
}