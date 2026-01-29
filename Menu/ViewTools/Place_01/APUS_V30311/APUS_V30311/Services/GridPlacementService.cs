using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V311.Helpers;
using Revit26_Plugin.APUS_V311.Models;
using Revit26_Plugin.APUS_V311.ViewModels;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Revit26_Plugin.APUS_V311.Services
{
    /// <summary>
    /// Grid placement with:
    /// - Fixed column width
    /// - Adaptive row height (tallest view per row)
    /// - Horizontal gap applied ONLY between columns
    /// - Vertical gap applied ONLY between rows
    /// - Left-aligned views (no wasted width)
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
            ref int detailIndex)
        {
            double gapX = UnitConversionHelper.MmToFeet(horizontalGapMm);
            double gapY = UnitConversionHelper.MmToFeet(verticalGapMm);

            double currentY = area.Origin.Y;
            double bottomLimit = area.Origin.Y - area.Height;

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

                rowHeight += gapY;

                // Check vertical fit
                if (currentY - rowHeight < bottomLimit)
                    break;

                // Place row
                for (int col = 0; col < rowItems.Count; col++)
                {
                    var item = rowItems[col];

                    if (!Viewport.CanAddViewToSheet(_doc, sheet.Id, item.View.Id))
                    {
                        vm.LogWarning($"SKIPPED (already placed): {item.Name}");
                        index++;
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

                    double y = currentY;

                    // LEFT-aligned placement
                    XYZ center = new XYZ(
                        x + viewWidth / 2,
                        y - viewHeight / 2,
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

                    vm.LogInfo($"PLACED: {item.Name} on {sheet.SheetNumber}");
                    vm.Progress.Step();

                    placed++;
                    index++;
                }

                // Move to next row
                currentY -= rowHeight;
            }

            return placed;
        }
    }
}
