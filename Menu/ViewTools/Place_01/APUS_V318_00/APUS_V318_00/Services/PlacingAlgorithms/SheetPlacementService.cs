// File: SheetPlacementService.cs
using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V318.ExternalEvents;
using Revit26_Plugin.APUS_V318.Helpers;
using Revit26_Plugin.APUS_V318.Models;
using Revit26_Plugin.APUS_V318.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V318.Services
{
    public class SheetPlacementService
    {
        private readonly Document _doc;
        private readonly SheetCreationService _sheetCreator;
        private const double EFFICIENCY_TARGET = 0.75; // Target 75% sheet utilization

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
                int sheetCount = 0;
                int totalPlaced = 0;

                // Calculate optimal columns once for consistency
                int optimalColumns = CalculateOptimalColumns(sections, context);
                context.ViewModel?.LogInfo($"📊 Optimal columns per sheet: {optimalColumns}");

                while (sectionIndex < sections.Count)
                {
                    if (context.ViewModel?.Progress.IsCancelled == true)
                        break;

                    // Create new sheet
                    string sheetNumber = context.SheetNumberService.GetNextAvailableSheetNumber("OR");
                    context.SheetNumberService.TryReserveSheetNumber(sheetNumber);

                    var sheet = _sheetCreator.Create(context.TitleBlock, sheetNumber, $"Ordered-{sheetNumber}");
                    context.ViewModel?.LogInfo($"Created sheet: {sheet.SheetNumber}");

                    // Place on current sheet with optimized columns
                    int placed = PlaceOrderedOptimized(
                        sheet,
                        sections,
                        sectionIndex,
                        context,
                        optimalColumns,
                        result);

                    if (placed == 0)
                    {
                        _doc.Delete(sheet.Id);
                        context.ViewModel?.LogWarning($"No views placed on sheet {sheet.SheetNumber}, removing");
                        break;
                    }

                    // Log sheet efficiency
                    double efficiency = CalculateSheetEfficiency(sheet, placed, context);
                    context.ViewModel?.LogInfo($"Sheet {sheet.SheetNumber} efficiency: {efficiency:P0}");

                    sectionIndex += placed;
                    totalPlaced += placed;
                    result.PlacedCount += placed;
                    result.SheetNumbers.Add(sheet.SheetNumber);
                    sheetCount++;

                    // Adjust columns if efficiency is too low
                    if (efficiency < EFFICIENCY_TARGET && optimalColumns > 1)
                    {
                        optimalColumns--;
                        context.ViewModel?.LogInfo($"📉 Reducing columns to {optimalColumns} for better fit");
                    }
                }

                context.ViewModel?.LogInfo(
                    $"Ordered placement complete: {totalPlaced} views on {sheetCount} sheets, " +
                    $"Avg efficiency: {(totalPlaced > 0 ? result.PlacedCount * 100.0 / (sheetCount * optimalColumns * 4) : 0):F0}%");

                return result;
            }
            catch (Exception ex)
            {
                context.ViewModel?.LogError($"Ordered placement failed: {ex.Message}");
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        private int PlaceOrderedOptimized(
            ViewSheet sheet,
            IList<SectionItemViewModel> sections,
            int startIndex,
            SectionPlacementHandler.PlacementContext context,
            int optimalColumns,
            SectionPlacementHandler.PlacementResult result)
        {
            double gapX = UnitConversionHelper.MmToFeet(context.HorizontalGapMm);
            double gapY = UnitConversionHelper.MmToFeet(context.VerticalGapMm);

            double left = context.PlacementArea.Origin.X + gapX;
            double right = context.PlacementArea.Right - gapX;
            double top = context.PlacementArea.Origin.Y - gapY;
            double bottom = context.PlacementArea.Bottom + gapY;

            double columnWidth = (right - left - (optimalColumns - 1) * gapX) / optimalColumns;

            int placed = 0;
            int currentCol = 0;
            double currentY = top;
            double currentRowHeight = 0;
            List<SectionItemViewModel> currentRow = new List<SectionItemViewModel>();

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

                // Check if view fits in current column width
                if (footprint.WidthFt > columnWidth + 0.01) // 0.01ft tolerance
                {
                    context.ViewModel?.LogWarning($"⚠️ {item.ViewName} width ({footprint.WidthFt:F2}ft) exceeds column width ({columnWidth:F2}ft)");
                    // Still try to place but log warning
                }

                // Start new row if column limit reached
                if (currentCol >= optimalColumns)
                {
                    currentY -= currentRowHeight + gapY;
                    currentCol = 0;
                    currentRowHeight = 0;
                    currentRow.Clear();
                }

                // Check vertical space
                if (currentY - footprint.HeightFt < bottom)
                {
                    break; // Sheet full
                }

                // Calculate position
                double x = left + currentCol * (columnWidth + gapX);
                double centerX = x + footprint.WidthFt / 2;

                // Bottom align
                double viewBottomY = currentY - footprint.HeightFt;
                double centerY = viewBottomY + footprint.HeightFt / 2;

                // Create viewport
                var vp = Viewport.Create(_doc, sheet.Id, item.View.Id, new XYZ(centerX, centerY, 0));

                // Set detail number
                int detailNumber = context.GetNextDetailNumber();
                var detailParam = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                if (detailParam != null && !detailParam.IsReadOnly)
                {
                    detailParam.Set(detailNumber.ToString());
                }

                context.ViewModel?.LogInfo(
                    $"ORDERED: {item.ViewName} on {sheet.SheetNumber} (Col {currentCol + 1}, Detail {detailNumber})");

                context.ViewModel?.Progress.Step();

                currentRowHeight = Math.Max(currentRowHeight, footprint.HeightFt);
                currentCol++;
                placed++;
            }

            return placed;
        }

        private int CalculateOptimalColumns(
            List<SectionItemViewModel> sections,
            SectionPlacementHandler.PlacementContext context)
        {
            if (!sections.Any()) return 1;

            double gapX = UnitConversionHelper.MmToFeet(context.HorizontalGapMm);
            double usableWidth = context.PlacementArea.Width - gapX * 2;

            // Get width statistics
            var widths = sections
                .Take(20) // Sample first 20 views
                .Select(s => ViewSizeService.Calculate(s.View).WidthFt)
                .ToList();

            double avgWidth = widths.Average();
            double maxWidth = widths.Max();
            double medianWidth = widths.OrderBy(w => w).ElementAt(widths.Count / 2);

            // Try different column counts
            for (int cols = 6; cols >= 1; cols--)
            {
                double cellWidth = (usableWidth - (cols - 1) * gapX) / cols;

                // Check if most views fit
                if (cellWidth >= medianWidth * 0.9) // Within 90% of median
                {
                    return cols;
                }
            }

            return 3; // Default fallback
        }

        private double CalculateSheetEfficiency(
            ViewSheet sheet,
            int viewsPlaced,
            SectionPlacementHandler.PlacementContext context)
        {
            if (viewsPlaced == 0) return 0;

            try
            {
                var viewports = new FilteredElementCollector(_doc, sheet.Id)
                    .OfClass(typeof(Viewport))
                    .Cast<Viewport>()
                    .ToList();

                if (!viewports.Any()) return 0;

                double totalViewArea = 0;
                foreach (var vp in viewports)
                {
                    var view = _doc.GetElement(vp.ViewId) as ViewSection;
                    if (view != null)
                    {
                        var footprint = ViewSizeService.Calculate(view);
                        totalViewArea += footprint.WidthFt * footprint.HeightFt;
                    }
                }

                double sheetArea = context.PlacementArea.Width * context.PlacementArea.Height;
                return sheetArea > 0 ? totalViewArea / sheetArea : 0;
            }
            catch
            {
                return viewsPlaced / 8.0; // Rough estimate: 8 views = 100%
            }
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