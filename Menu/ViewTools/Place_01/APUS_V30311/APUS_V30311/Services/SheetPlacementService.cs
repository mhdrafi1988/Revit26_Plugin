using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V311.Helpers;
using Revit26_Plugin.APUS_V311.Models;
using Revit26_Plugin.APUS_V311.ViewModels;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.APUS_V311.Services
{
    /// <summary>
    /// Places section views in strict order using
    /// independent horizontal and vertical gaps.
    /// </summary>
    public class SheetPlacementService
    {
        private readonly Document _doc;

        public SheetPlacementService(Document doc)
        {
            _doc = doc;
        }

        public int PlaceBatchOrdered(
            ViewSheet sheet,
            IList<SectionItemViewModel> sections,
            int startIndex,
            SheetPlacementArea area,
            double horizontalGapMm,
            double verticalGapMm,
            AutoPlaceSectionsViewModel vm,
            ref int detailIndex)
        {
            double gapX = UnitConversionHelper.MmToFeet(horizontalGapMm);
            double gapY = UnitConversionHelper.MmToFeet(verticalGapMm);

            double left = area.Origin.X;
            double right = area.Origin.X + area.Width;
            double top = area.Origin.Y;
            double bottom = area.Origin.Y - area.Height;

            double cursorX = left + gapX;
            double cursorY = top - gapY;

            double rowMaxHeight = 0;
            int placed = 0;

            for (int i = startIndex; i < sections.Count; i++)
            {
                var item = sections[i];

                if (!Viewport.CanAddViewToSheet(_doc, sheet.Id, item.View.Id))
                {
                    vm.LogWarning($"SKIPPED (already placed): {item.Name}");
                    continue;
                }

                SectionFootprint fp = ViewSizeService.Calculate(item.View);

                double w = fp.WidthFt;
                double h = fp.HeightFt;

                if (cursorX + w > right)
                {
                    cursorX = left + gapX;
                    cursorY -= rowMaxHeight + gapY;
                    rowMaxHeight = 0;
                }

                if (cursorY - h < bottom)
                {
                    break;
                }

                XYZ center = new XYZ(
                    cursorX + w / 2,
                    cursorY - h / 2,
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
                    p.Set(detailIndex.ToString());

                vm.LogInfo(
                    $"PLACED: {item.Name} on {sheet.SheetNumber}");

                cursorX += w + gapX;
                rowMaxHeight = Math.Max(rowMaxHeight, h);

                vm.Progress.Step();
                placed++;
            }

            return placed;
        }
    }
}
