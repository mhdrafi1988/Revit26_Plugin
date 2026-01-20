using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V306.Helpers;
using Revit26_Plugin.APUS_V306.Models;
using Revit26_Plugin.APUS_V306.ViewModels;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.APUS_V306.Services
{
    /// <summary>
    /// Places section views in the given (already sorted) order.
    /// Uses fixed gap (mm) on ALL sides.
    /// Skips already-placed views safely.
    /// Deterministic row-by-row placement.
    /// </summary>
    public class SheetPlacementService
    {
        private readonly Document _doc;

        public SheetPlacementService(Document doc)
        {
            _doc = doc;
        }

        /// <summary>
        /// Places views left ? right, then top ? bottom.
        /// Stops only when sheet space is exhausted.
        /// </summary>
        public int PlaceBatchOrdered(
            ViewSheet sheet,
            IList<SectionItemViewModel> sortedSections,
            SheetPlacementArea area,
            double gapMm,
            AutoPlaceSectionsViewModel vm,
            ref int detailIndex)
        {
            double gapFt = UnitConversionHelper.MmToFeet(gapMm);

            // Sheet usable bounds
            double leftLimit = area.Origin.X;
            double rightLimit = area.Origin.X + area.Width;
            double topLimit = area.Origin.Y;
            double bottomLimit = area.Origin.Y - area.Height;

            // Cursor starts inside margins + gap
            double cursorX = leftLimit + gapFt;
            double cursorY = topLimit - gapFt;

            double rowMaxHeight = 0;
            int placedCount = 0;

            foreach (var item in sortedSections)
            {
                // --------------------------------------------------
                // HARD SAFETY: skip already placed views
                // --------------------------------------------------
                if (!Viewport.CanAddViewToSheet(_doc, sheet.Id, item.View.Id))
                {
                    vm.LogWarning($"SKIPPED (already placed): {item.Name}");
                    continue;
                }

                SectionFootprint fp = ViewSizeService.Calculate(item.View);

                // Invalid footprint safety
                if (fp.WidthFt <= 0 || fp.HeightFt <= 0)
                {
                    vm.LogWarning($"SKIPPED (invalid size): {item.Name}");
                    continue;
                }

                double viewWidth = fp.WidthFt;
                double viewHeight = fp.HeightFt;

                // --------------------------------------------------
                // Wrap to next row if horizontal overflow
                // --------------------------------------------------
                if (cursorX + viewWidth > rightLimit)
                {
                    cursorX = leftLimit + gapFt;
                    cursorY -= rowMaxHeight + gapFt;
                    rowMaxHeight = 0;
                }

                // --------------------------------------------------
                // Stop if vertical space exhausted
                // --------------------------------------------------
                if (cursorY - viewHeight < bottomLimit)
                {
                    vm.LogWarning(
                        $"STOPPED: No vertical space left on sheet {sheet.SheetNumber}");
                    break;
                }

                // --------------------------------------------------
                // Place viewport (center-based)
                // --------------------------------------------------
                XYZ center = new XYZ(
                    cursorX + viewWidth / 2,
                    cursorY - viewHeight / 2,
                    0);

                Viewport viewport = Viewport.Create(
                    _doc,
                    sheet.Id,
                    item.View.Id,
                    center);

                // --------------------------------------------------
                // Detail number (continuous)
                // --------------------------------------------------
                detailIndex++;

                Parameter detailParam =
                    viewport.get_Parameter(
                        BuiltInParameter.VIEWPORT_DETAIL_NUMBER);

                if (detailParam != null && !detailParam.IsReadOnly)
                {
                    detailParam.Set(detailIndex.ToString());
                }

                vm.LogInfo(
                    $"PLACED: {item.Name} on {sheet.SheetNumber} (Gap {gapMm}mm)");

                // --------------------------------------------------
                // Advance cursor AFTER successful placement
                // --------------------------------------------------
                cursorX += viewWidth + gapFt;
                rowMaxHeight = Math.Max(rowMaxHeight, viewHeight);

                vm.Progress.Step();
                placedCount++;
            }

            return placedCount;
        }
    }
}
