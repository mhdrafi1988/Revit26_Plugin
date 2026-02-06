using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V312.Helpers;
using Revit26_Plugin.APUS_V312.Models;
using Revit26_Plugin.APUS_V312.ViewModels;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Revit26_Plugin.APUS_V312.Services
{
    /// <summary>
    /// Grid placement with:
    /// - Fixed column width
    /// - Adaptive row height (tallest view per row)
    /// - Horizontal gap applied ONLY between columns
    /// - Vertical gap applied ONLY between rows
    /// - Left-aligned views (no wasted width)
    /// - BOTTOM-ALIGNED rows (changed from top-aligned)
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
            double cellWidth,
            double horizontalGapMm,
            double verticalGapMm,
            int columns,
            AutoPlaceSectionsViewModel vm,
            ref int detailIndex,
            ref int placedCount,
            ref int failedCount)
        {
            double gapX = UnitConversionHelper.MmToFeet(horizontalGapMm);
            double gapY = UnitConversionHelper.MmToFeet(verticalGapMm);

            // Start from top and work downward (BOTTOM-ALIGNED)
            double currentY = area.Origin.Y; // Top of placement area
            double bottomLimit = area.Origin.Y - area.Height; // Bottom limit

            int placed = 0;
            int index = startIndex;

            while (index < sections.Count)
            {
                // Take next row
                var rowItems =
                    sections
                        .Skip(index)
                        .Take(columns)
                        .ToList();

                // Adaptive row height (tallest view)
                double rowHeight =
                    rowItems
                        .Select(x => ViewSizeService.Calculate(x.View).HeightFt)
                        .Max();

                rowHeight += gapY; // Add vertical gap below the row

                // Check vertical fit - we need space for this row plus any previous rows
                // Since we're bottom-aligning, we start from top and subtract row heights
                double bottomOfRow = currentY - rowHeight;
                if (bottomOfRow < bottomLimit)
                    break;

                // Place row
                for (int col = 0; col < rowItems.Count; col++)
                {
                    var item = rowItems[col];

                    if (!Viewport.CanAddViewToSheet(_doc, sheet.Id, item.View.Id))
                    {
                        vm.LogWarning($"SKIPPED (already placed): {item.ViewName}");
                        index++;
                        failedCount++;
                        continue;
                    }

                    double viewWidth =
                        ViewSizeService.Calculate(item.View).WidthFt;

                    double viewHeight =
                        ViewSizeService.Calculate(item.View).HeightFt;

                    // Horizontal position
                    double x =
                        area.Origin.X +
                        col * (cellWidth + gapX);

                    // BOTTOM-ALIGNED: Position from bottom of row
                    // currentY is the top of the row, so subtract view height from it
                    double viewTopY = currentY - (rowHeight - viewHeight - gapY / 2);
                    double viewCenterY = viewTopY - (viewHeight / 2);

                    // LEFT-aligned placement
                    XYZ center = new XYZ(
                        x + viewWidth / 2,
                        viewCenterY,
                        0);

                    Viewport vp = Viewport.Create(
                        _doc,
                        sheet.Id,
                        item.View.Id,
                        center);

                    detailIndex++;
                    Parameter p =
                        vp.get_Parameter(
                            BuiltInParameter.VIEWPORT_DETAIL_NUMBER);

                    if (p != null && !p.IsReadOnly)
                        p.Set(detailIndex.ToString(CultureInfo.InvariantCulture));

                    vm.LogInfo($"PLACED: {item.ViewName} on {sheet.SheetNumber}");
                    vm.Progress.Step();

                    placed++;
                    placedCount++;
                    index++;
                }

                // Move to next row - subtract the full row height
                currentY -= rowHeight;
            }

            return placed;
        }
    }
}