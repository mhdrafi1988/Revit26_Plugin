// File: GridPlacementService.cs
using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V314.Helpers;
using Revit26_Plugin.APUS_V314.Models;
using Revit26_Plugin.APUS_V314.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V314.Services
{
    /// <summary>
    /// Grid placement with fixed column width and adaptive row height
    /// </summary>
    public class GridPlacementService
    {
        private readonly Document _doc;

        public GridPlacementService(Document doc)
        {
            _doc = doc;
        }

        public int PlaceOnSheet(
            ViewSheet sheet,
            IList<SectionItemViewModel> sections,
            int startIndex,
            SheetPlacementArea area,
            GridLayoutCalculationService.GridLayout layout,
            AutoPlaceSectionsViewModel vm,
            ref int detailIndex)
        {
            if (sections == null || !sections.Any())
                return 0;

            int placed = 0;
            int currentIndex = startIndex;
            double currentY = area.Origin.Y;
            int currentRow = 0;

            while (currentIndex < sections.Count && currentRow < layout.Rows)
            {
                // Get views for current row
                var rowItems = sections
                    .Skip(currentIndex)
                    .Take(layout.Columns)
                    .ToList();

                if (!rowItems.Any())
                    break;

                // Calculate row height (tallest view in row)
                double rowHeight = rowItems
                    .Select(x => ViewSizeService.Calculate(x.View).HeightFt)
                    .Max() + layout.VerticalGap;

                // Check vertical fit
                if (currentY - rowHeight < area.Bottom)
                    break;

                // Place each view in the row
                for (int col = 0; col < rowItems.Count; col++)
                {
                    var item = rowItems[col];

                    if (!Viewport.CanAddViewToSheet(_doc, sheet.Id, item.View.Id))
                    {
                        vm.LogWarning($"SKIPPED (already placed): {item.ViewName}");
                        currentIndex++;
                        continue;
                    }

                    var footprint = ViewSizeService.Calculate(item.View);

                    // Calculate position
                    double x = area.Origin.X + col * (layout.CellWidth + layout.HorizontalGap);
                    double centerX = x + layout.CellWidth / 2;

                    // Bottom-align view in cell
                    double viewBottomY = currentY - rowHeight + layout.VerticalGap / 2;
                    double centerY = viewBottomY + footprint.HeightFt / 2;

                    XYZ center = new XYZ(centerX, centerY, 0);

                    // Create viewport
                    Viewport vp = Viewport.Create(_doc, sheet.Id, item.View.Id, center);

                    // Set detail number
                    detailIndex++;
                    var detailParam = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                    if (detailParam != null && !detailParam.IsReadOnly)
                        detailParam.Set(detailIndex.ToString());

                    vm.LogInfo($"PLACED: {item.ViewName} on {sheet.SheetNumber} (Detail {detailIndex})");
                    vm.Progress.Step();

                    placed++;
                    currentIndex++;
                }

                currentY -= rowHeight;
                currentRow++;
            }

            return placed;
        }

        public int PlaceAdaptiveOnSheet(
            ViewSheet sheet,
            List<SectionItemViewModel> sections,
            int startIndex,
            SheetPlacementArea area,
            double cellWidth,
            double horizontalGapMm,
            double verticalGapMm,
            int columns,
            AutoPlaceSectionsViewModel vm,
            ref int detailIndex)
        {
            double gapX = UnitConversionHelper.MmToFeet(horizontalGapMm);
            double gapY = UnitConversionHelper.MmToFeet(verticalGapMm);

            double currentY = area.Origin.Y;
            int placed = 0;
            int index = startIndex;

            while (index < sections.Count)
            {
                // Take next row
                var rowItems = sections
                    .Skip(index)
                    .Take(columns)
                    .ToList();

                if (!rowItems.Any())
                    break;

                // Calculate adaptive row height
                double rowHeight = rowItems
                    .Select(x => ViewSizeService.Calculate(x.View).HeightFt)
                    .Max() + gapY;

                // Check vertical fit
                if (currentY - rowHeight < area.Bottom)
                    break;

                // Place row
                for (int col = 0; col < rowItems.Count; col++)
                {
                    var item = rowItems[col];

                    if (!Viewport.CanAddViewToSheet(_doc, sheet.Id, item.View.Id))
                    {
                        vm.LogWarning($"SKIPPED (already placed): {item.ViewName}");
                        index++;
                        continue;
                    }

                    var footprint = ViewSizeService.Calculate(item.View);

                    // Horizontal position
                    double x = area.Origin.X + col * (cellWidth + gapX);

                    // Bottom-aligned position
                    double viewTopY = currentY - (rowHeight - footprint.HeightFt - gapY / 2);
                    double viewCenterY = viewTopY - (footprint.HeightFt / 2);

                    // Center in cell
                    double centerX = x + cellWidth / 2;
                    XYZ center = new XYZ(centerX, viewCenterY, 0);

                    Viewport vp = Viewport.Create(_doc, sheet.Id, item.View.Id, center);

                    detailIndex++;
                    var detailParam = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                    if (detailParam != null && !detailParam.IsReadOnly)
                        detailParam.Set(detailIndex.ToString());

                    vm.LogInfo($"PLACED: {item.ViewName} on {sheet.SheetNumber}");
                    vm.Progress.Step();

                    placed++;
                    index++;
                }

                currentY -= rowHeight;
            }

            return placed;
        }
    }
}