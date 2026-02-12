// File: SheetPlacementService.cs
// REFACTORED - Ordered placement with proper transaction context
using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V317.ExternalEvents;
using Revit26_Plugin.APUS_V317.Helpers; // Added for UnitConversionHelper
using Revit26_Plugin.APUS_V317.Models;
using Revit26_Plugin.APUS_V317.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V317.Services
{
    /// <summary>
    /// Simple ordered placement (left-to-right, top-to-bottom)
    /// CRITICAL: Assumes active transaction exists.
    /// </summary>
    public class SheetPlacementService
    {
        private readonly Document _doc;
        private readonly SheetCreationService _sheetCreator;

        public SheetPlacementService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _sheetCreator = new SheetCreationService(doc);
        }

        public SectionPlacementHandler.PlacementResult PlaceOnMultipleSheets(
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
                int sectionIndex = 0;

                while (sectionIndex < sections.Count)
                {
                    if (context.ViewModel?.Progress.IsCancelled == true)
                        break;

                    // Create new sheet with unique number
                    string sheetNumber = context.SheetNumberService.GetNextAvailableSheetNumber("OR");
                    context.SheetNumberService.TryReserveSheetNumber(sheetNumber);

                    var sheet = _sheetCreator.Create(context.TitleBlock, sheetNumber, $"Ordered-{sheetNumber}");
                    context.ViewModel?.LogInfo($"Created sheet: {sheet.SheetNumber}");

                    // Place on current sheet
                    int placed = PlaceOrdered(
                        sheet,
                        sections,
                        sectionIndex,
                        context,
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
                }

                context.ViewModel?.LogInfo(
                    $"Ordered placement complete: {result.PlacedCount} views on {result.SheetNumbers.Count} sheets");

                return result;
            }
            catch (Exception ex)
            {
                context.ViewModel?.LogError($"Ordered placement failed: {ex.Message}");
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        private int PlaceOrdered(
            ViewSheet sheet,
            IList<SectionItemViewModel> sections,
            int startIndex,
            SectionPlacementHandler.PlacementContext context,
            SectionPlacementHandler.PlacementResult result)
        {
            double gapX = UnitConversionHelper.MmToFeet(context.HorizontalGapMm);
            double gapY = UnitConversionHelper.MmToFeet(context.VerticalGapMm);

            double left = context.PlacementArea.Origin.X + gapX;
            double right = context.PlacementArea.Right - gapX;
            double top = context.PlacementArea.Origin.Y - gapY;
            double bottom = context.PlacementArea.Bottom + gapY;

            double cursorX = left;
            double cursorY = top;
            double rowMaxHeight = 0;

            int placed = 0;

            for (int i = startIndex; i < sections.Count; i++)
            {
                var item = sections[i];

                if (!CanPlaceView(item.View, sheet.Id))
                {
                    context.ViewModel?.LogWarning($"SKIPPED (already placed): {item.ViewName}");
                    result.FailedCount++;
                    continue;
                }

                var footprint = ViewSizeService.Calculate(item.View);
                double w = footprint.WidthFt;
                double h = footprint.HeightFt;

                // Check if view fits on current row
                if (cursorX + w > right)
                {
                    // Move to next row
                    cursorX = left;
                    cursorY -= rowMaxHeight + gapY;
                    rowMaxHeight = 0;
                }

                // Check if view fits vertically
                if (cursorY - h < bottom)
                {
                    // No more vertical space
                    break;
                }

                // Calculate center point
                double centerX = cursorX + w / 2;
                double centerY = cursorY - h / 2;

                XYZ center = new XYZ(centerX, centerY, 0);

                // Create viewport
                var vp = Viewport.Create(_doc, sheet.Id, item.View.Id, center);

                // Set detail number
                int detailNumber = context.GetNextDetailNumber();
                var detailParam = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                if (detailParam != null && !detailParam.IsReadOnly)
                {
                    detailParam.Set(detailNumber.ToString());
                }

                context.ViewModel?.LogInfo(
                    $"ORDERED PLACEMENT: {item.ViewName} on {sheet.SheetNumber} (Detail {detailNumber})");

                // Update cursor position
                cursorX += w + gapX;
                rowMaxHeight = Math.Max(rowMaxHeight, h);

                context.ViewModel?.Progress.Step();
                placed++;
            }

            return placed;
        }

        private bool CanPlaceView(ViewSection view, ElementId sheetId)
        {
            try
            {
                return Viewport.CanAddViewToSheet(_doc, sheetId, view.Id);
            }
            catch
            {
                return false;
            }
        }
    }
}