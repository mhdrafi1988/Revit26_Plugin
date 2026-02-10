// File: AdaptiveGridPlacementService.cs
using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V314.Models;
using Revit26_Plugin.APUS_V314.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using Revit26_Plugin.APUS_V314.Converters;

namespace Revit26_Plugin.APUS_V314.Services
{
    /// <summary>
    /// Adaptive Grid Placement - Groups views by size and creates optimal grid per group
    /// </summary>
    public class AdaptiveGridPlacementService
    {
        private readonly Document _doc;
        private readonly SheetCreationService _sheetCreator;

        public AdaptiveGridPlacementService(Document doc)
        {
            _doc = doc;
            _sheetCreator = new SheetCreationService(doc);
        }

        public bool PlaceSections(
            List<SectionItemViewModel> sections,
            FamilySymbol titleBlock,
            SheetPlacementArea area,
            double horizontalGapMm,
            double verticalGapMm,
            AutoPlaceSectionsViewModel vm)
        {
            if (sections == null || !sections.Any())
            {
                vm.LogWarning("No sections to place.");
                return false;
            }

            try
            {
                // Group sections by size categories
                var sizeGroups = GroupSectionsBySize(sections, vm);
                vm.LogInfo($"Created {sizeGroups.Count} size groups");

                int totalPlaced = 0;
                int sheetIndex = 1;

                // Process each size group
                foreach (var group in sizeGroups)
                {
                    if (!group.Any()) continue;

                    // Calculate optimal layout for this size group
                    var layout = CalculateAdaptiveLayout(group, area, horizontalGapMm, verticalGapMm, vm);

                    // Place group using calculated layout
                    int placed = PlaceGroupOnSheets(
                        group,
                        titleBlock,
                        area,
                        layout,
                        ref sheetIndex,
                        vm,
                        ref totalPlaced);

                    vm.LogInfo($"Group placed: {placed} views");
                }

                vm.LogSuccess($"Adaptive placement complete: {totalPlaced} total views placed");
                return totalPlaced > 0;
            }
            catch (Exception ex)
            {
                vm.LogError($"Adaptive placement failed: {ex.Message}");
                return false;
            }
        }

        private List<List<SectionItemViewModel>> GroupSectionsBySize(
            List<SectionItemViewModel> sections,
            AutoPlaceSectionsViewModel vm)
        {
            var groups = new List<List<SectionItemViewModel>>();
            var small = new List<SectionItemViewModel>();
            var medium = new List<SectionItemViewModel>();
            var large = new List<SectionItemViewModel>();
            var extraLarge = new List<SectionItemViewModel>();

            foreach (var section in sections)
            {
                var footprint = ViewSizeService.Calculate(section.View);
                double area = footprint.WidthFt * footprint.HeightFt;

                if (area < 2.0) small.Add(section);
                else if (area < 5.0) medium.Add(section);
                else if (area < 10.0) large.Add(section);
                else extraLarge.Add(section);
            }

            // Add groups in order of size (largest first for better packing)
            if (extraLarge.Any()) groups.Add(extraLarge);
            if (large.Any()) groups.Add(large);
            if (medium.Any()) groups.Add(medium);
            if (small.Any()) groups.Add(small);

            vm.LogInfo($"Size groups: XL({extraLarge.Count}), L({large.Count}), M({medium.Count}), S({small.Count})");
            return groups;
        }

        private AdaptiveLayout CalculateAdaptiveLayout(
            List<SectionItemViewModel> group,
            SheetPlacementArea area,
            double horizontalGapMm,
            double verticalGapMm,
            AutoPlaceSectionsViewModel vm)
        {
            if (!group.Any()) return null;

            // Calculate average and max dimensions
            var footprints = group.Select(s => ViewSizeService.Calculate(s.View)).ToList();
            double avgWidth = footprints.Average(f => f.WidthFt);
            double avgHeight = footprints.Average(f => f.HeightFt);
            double maxWidth = footprints.Max(f => f.WidthFt);
            double maxHeight = footprints.Max(f => f.HeightFt);

            // Convert gaps to feet
            double gapX = UnitConversionHelper.MmToFeet(horizontalGapMm);
            double gapY = UnitConversionHelper.MmToFeet(verticalGapMm);

            // Calculate optimal columns based on average width
            int maxColumns = (int)Math.Floor(area.Width / (maxWidth + gapX));
            int minColumns = (int)Math.Ceiling(area.Width / (avgWidth * 1.5 + gapX));
            int optimalColumns = Math.Max(1, Math.Min(maxColumns, minColumns));

            // Calculate cell dimensions
            double cellWidth = (area.Width - (optimalColumns - 1) * gapX) / optimalColumns;
            double cellHeight = maxHeight + gapY;

            vm.LogInfo($"Adaptive layout: {optimalColumns} columns, Cell: {cellWidth:F2}×{cellHeight:F2} ft");

            return new AdaptiveLayout
            {
                Columns = optimalColumns,
                CellWidth = cellWidth,
                CellHeight = cellHeight,
                HorizontalGap = gapX,
                VerticalGap = gapY
            };
        }

        private int PlaceGroupOnSheets(
            List<SectionItemViewModel> group,
            FamilySymbol titleBlock,
            SheetPlacementArea area,
            AdaptiveLayout layout,
            ref int sheetIndex,
            AutoPlaceSectionsViewModel vm,
            ref int totalPlaced)
        {
            int groupIndex = 0;
            int placedInGroup = 0;

            while (groupIndex < group.Count)
            {
                if (vm.Progress.IsCancelled) break;

                // Create new sheet
                ViewSheet sheet = _sheetCreator.Create(titleBlock, sheetIndex++);
                vm.LogInfo($"Created sheet: {sheet.SheetNumber}");

                // Place views on current sheet
                int placedOnSheet = PlaceOnSheet(
                    sheet,
                    group,
                    groupIndex,
                    area,
                    layout,
                    vm);

                if (placedOnSheet == 0)
                {
                    // Remove empty sheet
                    _doc.Delete(sheet.Id);
                    vm.LogWarning($"No views fit on sheet {sheet.SheetNumber}, removing empty sheet");
                    break;
                }

                groupIndex += placedOnSheet;
                placedInGroup += placedOnSheet;
                totalPlaced += placedOnSheet;
            }

            return placedInGroup;
        }

        private int PlaceOnSheet(
            ViewSheet sheet,
            List<SectionItemViewModel> group,
            int startIndex,
            SheetPlacementArea area,
            AdaptiveLayout layout,
            AutoPlaceSectionsViewModel vm)
        {
            int placed = 0;
            int currentRow = 0;
            double currentY = area.Origin.Y;

            while (startIndex + placed < group.Count)
            {
                // Calculate row height for current row
                double rowHeight = 0;
                int itemsInRow = 0;

                // Find tallest view in current row
                for (int i = 0; i < layout.Columns && startIndex + placed + i < group.Count; i++)
                {
                    var section = group[startIndex + placed + i];
                    var footprint = ViewSizeService.Calculate(section.View);
                    rowHeight = Math.Max(rowHeight, footprint.HeightFt);
                    itemsInRow++;
                }

                rowHeight += layout.VerticalGap;

                // Check if row fits vertically
                if (currentY - rowHeight < area.Bottom)
                    break; // No more vertical space

                // Place views in current row
                for (int col = 0; col < itemsInRow; col++)
                {
                    var section = group[startIndex + placed];

                    if (!Viewport.CanAddViewToSheet(_doc, sheet.Id, section.View.Id))
                    {
                        vm.LogWarning($"SKIPPED (already placed): {section.ViewName}");
                        placed++;
                        continue;
                    }

                    var footprint = ViewSizeService.Calculate(section.View);

                    // Calculate position (centered in cell)
                    double x = area.Origin.X + col * (layout.CellWidth + layout.HorizontalGap);
                    double cellCenterX = x + layout.CellWidth / 2;

                    // Bottom-aligned within cell
                    double viewBottomY = currentY - rowHeight + layout.VerticalGap / 2;
                    double viewCenterY = viewBottomY + footprint.HeightFt / 2;

                    XYZ center = new XYZ(cellCenterX, viewCenterY, 0);

                    // Create viewport
                    Viewport vp = Viewport.Create(_doc, sheet.Id, section.View.Id, center);

                    // Set detail number
                    var detailParam = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                    if (detailParam != null && !detailParam.IsReadOnly)
                    {
                        int detailNumber = vm.Progress.Current + 1;
                        detailParam.Set(detailNumber.ToString());
                    }

                    vm.LogInfo($"PLACED: {section.ViewName} on {sheet.SheetNumber}");
                    vm.Progress.Step();
                    placed++;
                }

                currentY -= rowHeight;
                currentRow++;
            }

            return placed;
        }

        private class AdaptiveLayout
        {
            public int Columns { get; set; }
            public double CellWidth { get; set; }
            public double CellHeight { get; set; }
            public double HorizontalGap { get; set; }
            public double VerticalGap { get; set; }
        }
    }

    // Add this helper class at the end of the file or in a suitable utilities location
    internal static class UnitConversionHelper
    {
        private const double FeetPerMillimeter = 0.00328084;
        public static double MmToFeet(double mm)
        {
            return mm * FeetPerMillimeter;
        }
    }
}