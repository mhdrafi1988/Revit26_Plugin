// File: SheetPlacementService.cs
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
    /// Simple ordered placement (left-to-right, top-to-bottom)
    /// </summary>
    public class SheetPlacementService
    {
        private readonly Document _doc;

        public SheetPlacementService(Document doc)
        {
            _doc = doc;
        }

        public int PlaceOrdered(
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

            double left = area.Origin.X + gapX;
            double right = area.Right - gapX;
            double top = area.Origin.Y - gapY;
            double bottom = area.Bottom + gapY;

            double cursorX = left;
            double cursorY = top;
            double rowMaxHeight = 0;

            int placed = 0;

            for (int i = startIndex; i < sections.Count; i++)
            {
                var item = sections[i];

                if (!Viewport.CanAddViewToSheet(_doc, sheet.Id, item.View.Id))
                {
                    vm.LogWarning($"SKIPPED (already placed): {item.ViewName}");
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
                Viewport vp = Viewport.Create(_doc, sheet.Id, item.View.Id, center);

                // Set detail number
                detailIndex++;
                var detailParam = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                if (detailParam != null && !detailParam.IsReadOnly)
                    detailParam.Set(detailIndex.ToString());

                vm.LogInfo($"ORDERED PLACEMENT: {item.ViewName} on {sheet.SheetNumber}");

                // Update cursor position
                cursorX += w + gapX;
                rowMaxHeight = Math.Max(rowMaxHeight, h);

                vm.Progress.Step();
                placed++;
            }

            return placed;
        }

        public bool PlaceOnMultipleSheets(
            List<SectionItemViewModel> sections,
            FamilySymbol titleBlock,
            SheetPlacementArea area,
            double horizontalGapMm,
            double verticalGapMm,
            AutoPlaceSectionsViewModel vm,
            out int totalPlaced,
            out List<string> createdSheets)
        {
            totalPlaced = 0;
            createdSheets = new List<string>();

            if (sections == null || !sections.Any())
                return false;

            try
            {
                var sheetCreator = new SheetCreationService(_doc);
                int sheetIndex = 1;
                int detailIndex = 0;
                int sectionIndex = 0;

                while (sectionIndex < sections.Count)
                {
                    if (vm.Progress.IsCancelled)
                        break;

                    // Create new sheet
                    ViewSheet sheet = sheetCreator.Create(titleBlock, sheetIndex++);
                    createdSheets.Add(sheet.SheetNumber);
                    vm.LogInfo($"Created sheet: {sheet.SheetNumber}");

                    // Place on current sheet
                    int placed = PlaceOrdered(
                        sheet,
                        sections,
                        sectionIndex,
                        area,
                        horizontalGapMm,
                        verticalGapMm,
                        vm,
                        ref detailIndex);

                    if (placed == 0)
                    {
                        // Remove empty sheet
                        _doc.Delete(sheet.Id);
                        createdSheets.Remove(sheet.SheetNumber);
                        vm.LogWarning($"No views placed on sheet {sheet.SheetNumber}, removing");
                        break;
                    }

                    sectionIndex += placed;
                    totalPlaced += placed;
                }

                vm.LogInfo($"Ordered placement complete: {totalPlaced} views on {createdSheets.Count} sheets");
                return totalPlaced > 0;
            }
            catch (Exception ex)
            {
                vm.LogError($"Ordered placement failed: {ex.Message}");
                return false;
            }
        }
    }
}