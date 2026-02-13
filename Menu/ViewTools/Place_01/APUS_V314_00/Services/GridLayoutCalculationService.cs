// File: GridLayoutCalculationService.cs
using Revit26_Plugin.APUS_V314.Helpers;
using Revit26_Plugin.APUS_V314.Models;
using Revit26_Plugin.APUS_V314.ViewModels;
using Revit26_Plugin.APUS_V314.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Revit26_Plugin.APUS_V314.Services
{
    /// <summary>
    /// Calculates optimal grid layout parameters
    /// </summary>
    public static class GridLayoutCalculationService
    {
        public static bool TryCalculate(
            IList<SectionItemViewModel> sections,
            SheetPlacementArea area,
            double horizontalGapMm,
            double verticalGapMm,
            out double cellWidthFt,
            out double cellHeightFt,
            out int columns,
            out int rows,
            AutoPlaceSectionsViewModel vm = null)
        {
            cellWidthFt = 0;
            cellHeightFt = 0;
            columns = 0;
            rows = 0;

            if (sections == null || sections.Count == 0)
            {
                vm?.LogWarning("⚠️ No sections provided for grid calculation");
                return false;
            }

            vm?.LogInfo("🔍 STARTING GRID LAYOUT CALCULATION");
            vm?.LogInfo($"   • Input sections: {sections.Count}");
            vm?.LogInfo($"   • Placement area: {area.Width:F2} × {area.Height:F2} ft");
            vm?.LogInfo($"   • Gaps: {horizontalGapMm}mm × {verticalGapMm}mm");

            try
            {
                // Get all view dimensions
                var footprints = sections
                    .Select(x =>
                    {
                        var footprint = ViewSizeService.Calculate(x.View);
                        return footprint;
                    })
                    .Where(f => f.WidthFt > 0 && f.HeightFt > 0)
                    .ToList();

                if (!footprints.Any())
                {
                    vm?.LogError("❌ No valid view footprints found");
                    return false;
                }

                vm?.LogInfo($"✅ Valid footprints: {footprints.Count}");

                // Convert gaps to feet
                double gapX = UnitConversionHelper.MmToFeet(horizontalGapMm);
                double gapY = UnitConversionHelper.MmToFeet(verticalGapMm);

                vm?.LogInfo($"📐 Gaps in feet: {gapX:F4} × {gapY:F4} ft");

                // Calculate statistics
                double maxWidth = footprints.Max(f => f.WidthFt);
                double maxHeight = footprints.Max(f => f.HeightFt);
                double minWidth = footprints.Min(f => f.WidthFt);
                double minHeight = footprints.Min(f => f.HeightFt);
                double avgWidth = footprints.Average(f => f.WidthFt);
                double avgHeight = footprints.Average(f => f.HeightFt);
                double medianWidth = CalculateMedian(footprints.Select(f => f.WidthFt).ToList());
                double medianHeight = CalculateMedian(footprints.Select(f => f.HeightFt).ToList());

                vm?.LogInfo("📊 VIEW DIMENSION STATISTICS:");
                vm?.LogInfo($"   • Width: Min={minWidth:F3}, Max={maxWidth:F3}, Avg={avgWidth:F3}, Med={medianWidth:F3} ft");
                vm?.LogInfo($"   • Height: Min={minHeight:F3}, Max={maxHeight:F3}, Avg={avgHeight:F3}, Med={medianHeight:F3} ft");

                // Calculate optimal cell width (median + 20% padding)
                cellWidthFt = Math.Max(maxWidth, medianWidth * 1.2);
                vm?.LogInfo($"📏 Calculated cell width: {cellWidthFt:F3} ft (Max={maxWidth:F3}, Med×1.2={medianWidth * 1.2:F3})");

                // Calculate maximum possible columns
                int maxColumns = (int)Math.Floor((area.Width + gapX) / (cellWidthFt + gapX));
                columns = Math.Max(1, Math.Min(maxColumns, sections.Count));

                vm?.LogInfo($"🔢 Column calculation:");
                vm?.LogInfo($"   • Max possible: {maxColumns} = floor(({area.Width:F2} + {gapX:F3}) / ({cellWidthFt:F3} + {gapX:F3}))");
                vm?.LogInfo($"   • Selected: {columns} columns");

                // Adjust cell width to fit exactly with gaps
                cellWidthFt = (area.Width - (columns - 1) * gapX) / columns;
                vm?.LogInfo($"📏 Adjusted cell width: {cellWidthFt:F3} ft = ({area.Width:F2} - ({columns - 1} × {gapX:F3})) / {columns}");

                // Calculate cell height (tallest view in collection)
                cellHeightFt = maxHeight;
                vm?.LogInfo($"📏 Cell height (tallest view): {cellHeightFt:F3} ft");

                // Calculate rows based on available height
                double availableHeight = area.Height;
                rows = (int)Math.Floor((availableHeight + gapY) / (cellHeightFt + gapY));
                rows = Math.Max(1, rows);

                vm?.LogInfo($"🔢 Row calculation:");
                vm?.LogInfo($"   • Available height: {availableHeight:F2} ft");
                vm?.LogInfo($"   • Cell + gap: {cellHeightFt:F3} + {gapY:F3} = {cellHeightFt + gapY:F3} ft");
                vm?.LogInfo($"   • Selected: {rows} rows");

                // Calculate total capacity
                int totalCapacity = columns * rows;
                vm?.LogInfo($"📊 Total grid capacity: {columns} × {rows} = {totalCapacity} cells");
                vm?.LogInfo($"📊 Views to place: {sections.Count}");
                vm?.LogInfo($"📊 Fill ratio: {(sections.Count * 100.0 / totalCapacity):F1}%");

                if (columns <= 0 || rows <= 0)
                {
                    vm?.LogError($"❌ Invalid grid dimensions: {columns} × {rows}");
                    return false;
                }

                // Verify fit
                bool widthFits = cellWidthFt >= maxWidth;
                bool heightFits = cellHeightFt >= maxHeight;

                vm?.LogInfo("✅ FIT VERIFICATION:");
                vm?.LogInfo($"   • Width fit: {cellWidthFt:F3} >= {maxWidth:F3} = {widthFits}");
                vm?.LogInfo($"   • Height fit: {cellHeightFt:F3} >= {maxHeight:F3} = {heightFits}");

                if (!widthFits)
                {
                    vm?.LogWarning($"⚠️ Cell width ({cellWidthFt:F3} ft) is less than max view width ({maxWidth:F3} ft)");
                    vm?.LogInfo($"   💡 Consider reducing columns to {Math.Max(1, columns - 1)}");
                }

                if (!heightFits)
                {
                    vm?.LogWarning($"⚠️ Cell height ({cellHeightFt:F3} ft) is less than max view height ({maxHeight:F3} ft)");
                }

                vm?.LogInfo("✅ FINAL GRID LAYOUT:");
                vm?.LogInfo($"   • Grid: {columns} × {rows}");
                vm?.LogInfo($"   • Cell: {cellWidthFt:F3} × {cellHeightFt:F3} ft");
                vm?.LogInfo($"   • Total area: {cellWidthFt * columns:F2} × {cellHeightFt * rows:F2} ft");
                vm?.LogInfo($"   • Sheet utilization: {(cellWidthFt * columns * cellHeightFt * rows * 100 / (area.Width * area.Height)):F1}%");

                return true;
            }
            catch (Exception ex)
            {
                vm?.LogError($"❌ Grid calculation failed: {ex.Message}");
                vm?.LogError($"📋 Stack trace:\n{ex.StackTrace}");
                return false;
            }
        }

        public static bool TryCalculateAdaptive(
            List<SectionItemViewModel> sections,
            SheetPlacementArea area,
            double horizontalGapMm,
            double verticalGapMm,
            out GridLayout layout,
            AutoPlaceSectionsViewModel vm = null)
        {
            layout = null;

            if (sections == null || !sections.Any())
            {
                vm?.LogWarning("⚠️ No sections provided for adaptive grid calculation");
                return false;
            }

            vm?.LogInfo("🔍 STARTING ADAPTIVE GRID CALCULATION");
            vm?.LogInfo($"   • Input sections: {sections.Count}");
            vm?.LogInfo($"   • Placement area: {area.Width:F2} × {area.Height:F2} ft");

            try
            {
                // Group by similar widths
                var widthGroups = GroupByWidth(sections, vm);
                var heightGroups = GroupByHeight(sections, vm);

                vm?.LogInfo($"📊 Found {widthGroups.Count} width groups, {heightGroups.Count} height groups");

                // Calculate optimal columns for each width group
                var columnOptions = new List<int>();
                foreach (var group in widthGroups)
                {
                    if (TryCalculateOptimalColumns(group, area, horizontalGapMm, out int cols, vm))
                    {
                        columnOptions.Add(cols);
                        vm?.LogInfo($"   • Group ({group.Count} views): {cols} optimal columns");
                    }
                }

                if (!columnOptions.Any())
                {
                    vm?.LogWarning("⚠️ No valid column options from width groups");
                    // Try with all sections
                    if (TryCalculateOptimalColumns(sections, area, horizontalGapMm, out int defaultCols, vm))
                    {
                        columnOptions.Add(defaultCols);
                    }
                }

                if (!columnOptions.Any())
                {
                    vm?.LogError("❌ Could not calculate any column options");
                    return false;
                }

                // Use median of column options
                int optimalColumns = CalculateMedian(columnOptions);
                vm?.LogInfo($"🔢 Selected optimal columns: {optimalColumns} (from options: {string.Join(", ", columnOptions)})");

                // Calculate cell dimensions
                double gapX = UnitConversionHelper.MmToFeet(horizontalGapMm);
                double cellWidth = (area.Width - (optimalColumns - 1) * gapX) / optimalColumns;

                vm?.LogInfo($"📏 Cell width: {cellWidth:F3} ft = ({area.Width:F2} - ({optimalColumns - 1} × {gapX:F3})) / {optimalColumns}");

                // Find tallest view for cell height
                double maxHeight = sections.Max(s => ViewSizeService.Calculate(s.View).HeightFt);
                double gapY = UnitConversionHelper.MmToFeet(verticalGapMm);
                double cellHeight = maxHeight + gapY;

                vm?.LogInfo($"📏 Cell height: {cellHeight:F3} ft = Max height {maxHeight:F3} + gap {gapY:F3}");

                layout = new GridLayout
                {
                    Columns = optimalColumns,
                    Rows = 0, // Will be calculated per sheet
                    CellWidth = cellWidth,
                    CellHeight = cellHeight,
                    HorizontalGap = gapX,
                    VerticalGap = gapY
                };

                vm?.LogInfo("✅ ADAPTIVE GRID CALCULATION COMPLETE:");
                vm?.LogInfo($"   • Columns: {layout.Columns}");
                vm?.LogInfo($"   • Cell: {layout.CellWidth:F3} × {layout.CellHeight:F3} ft");
                vm?.LogInfo($"   • Gaps: {layout.HorizontalGap:F4} × {layout.VerticalGap:F4} ft");

                return true;
            }
            catch (Exception ex)
            {
                vm?.LogError($"❌ Adaptive grid calculation failed: {ex.Message}");
                vm?.LogError($"📋 Stack trace:\n{ex.StackTrace}");
                return false;
            }
        }

        private static bool TryCalculateOptimalColumns(
            List<SectionItemViewModel> group,
            SheetPlacementArea area,
            double horizontalGapMm,
            out int columns,
            AutoPlaceSectionsViewModel vm = null)
        {
            columns = 0;

            if (!group.Any())
            {
                vm?.LogDebug("Empty group for column calculation");
                return false;
            }

            try
            {
                // Get average width for this group
                double avgWidth = group.Average(s => ViewSizeService.Calculate(s.View).WidthFt);
                double gapX = UnitConversionHelper.MmToFeet(horizontalGapMm);

                vm?.LogDebug($"Group avg width: {avgWidth:F3} ft, Gap: {gapX:F3} ft");

                // Calculate maximum columns that fit
                int maxColumns = (int)Math.Floor((area.Width + gapX) / (avgWidth + gapX));
                maxColumns = Math.Max(1, maxColumns);

                vm?.LogDebug($"Max columns possible: {maxColumns}");

                // Use square-ish layout preference
                int optimalColumns = (int)Math.Ceiling(Math.Sqrt(group.Count));
                optimalColumns = Math.Max(1, Math.Min(optimalColumns, maxColumns));

                vm?.LogDebug($"Optimal columns: {optimalColumns} (sqrt={Math.Sqrt(group.Count):F1})");

                columns = optimalColumns;
                return columns > 0;
            }
            catch (Exception ex)
            {
                vm?.LogWarning($"Column calculation failed for group: {ex.Message}");
                return false;
            }
        }

        private static List<List<SectionItemViewModel>> GroupByWidth(List<SectionItemViewModel> sections, AutoPlaceSectionsViewModel vm = null)
        {
            var groups = new List<List<SectionItemViewModel>>();

            if (!sections.Any())
                return groups;

            var sorted = sections
                .Select(s => new
                {
                    Section = s,
                    Width = ViewSizeService.Calculate(s.View).WidthFt
                })
                .OrderBy(x => x.Width)
                .ToList();

            double firstWidth = sorted[0].Width;
            var currentGroup = new List<SectionItemViewModel> { sorted[0].Section };

            vm?.LogDebug($"Starting width grouping with {sorted.Count} sections");
            vm?.LogDebug($"First width: {firstWidth:F3} ft");

            for (int i = 1; i < sorted.Count; i++)
            {
                double currentWidth = sorted[i].Width;

                // Group if within 25% of first item in group
                double widthRatio = Math.Abs(currentWidth - firstWidth) / firstWidth;

                if (widthRatio <= 0.25)
                {
                    currentGroup.Add(sorted[i].Section);
                }
                else
                {
                    if (currentGroup.Count >= 2)
                    {
                        groups.Add(currentGroup);
                        vm?.LogDebug($"Width group {groups.Count}: {currentGroup.Count} views, width range: {ViewSizeService.Calculate(currentGroup.First().View).WidthFt:F3} to {ViewSizeService.Calculate(currentGroup.Last().View).WidthFt:F3} ft");
                    }
                    currentGroup = new List<SectionItemViewModel> { sorted[i].Section };
                    firstWidth = currentWidth;
                }
            }

            if (currentGroup.Count >= 2)
            {
                groups.Add(currentGroup);
                vm?.LogDebug($"Final width group {groups.Count}: {currentGroup.Count} views");
            }

            vm?.LogInfo($"📊 Created {groups.Count} width groups (min 2 views per group)");
            return groups;
        }

        private static List<List<SectionItemViewModel>> GroupByHeight(List<SectionItemViewModel> sections, AutoPlaceSectionsViewModel vm = null)
        {
            var groups = new List<List<SectionItemViewModel>>();

            if (!sections.Any())
                return groups;

            var sorted = sections
                .Select(s => new
                {
                    Section = s,
                    Height = ViewSizeService.Calculate(s.View).HeightFt
                })
                .OrderBy(x => x.Height)
                .ToList();

            double firstHeight = sorted[0].Height;
            var currentGroup = new List<SectionItemViewModel> { sorted[0].Section };

            vm?.LogDebug($"Starting height grouping with {sorted.Count} sections");

            for (int i = 1; i < sorted.Count; i++)
            {
                double currentHeight = sorted[i].Height;

                // Group if within 25% of first item in group
                if (Math.Abs(currentHeight - firstHeight) / firstHeight <= 0.25)
                {
                    currentGroup.Add(sorted[i].Section);
                }
                else
                {
                    if (currentGroup.Count >= 2)
                    {
                        groups.Add(currentGroup);
                    }
                    currentGroup = new List<SectionItemViewModel> { sorted[i].Section };
                    firstHeight = currentHeight;
                }
            }

            if (currentGroup.Count >= 2)
            {
                groups.Add(currentGroup);
            }

            vm?.LogDebug($"Created {groups.Count} height groups");
            return groups;
        }

        private static double CalculateMedian(List<double> values)
        {
            if (values == null || values.Count == 0)
                return 0;

            var sorted = values.OrderBy(v => v).ToList();
            int count = sorted.Count;

            if (count % 2 == 0)
                return (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0;
            else
                return sorted[count / 2];
        }

        private static int CalculateMedian(List<int> values)
        {
            if (values == null || values.Count == 0)
                return 0;

            var sorted = values.OrderBy(v => v).ToList();
            int count = sorted.Count;

            if (count % 2 == 0)
                return (sorted[count / 2 - 1] + sorted[count / 2]) / 2;
            else
                return sorted[count / 2];
        }

        public class GridLayout
        {
            public int Columns { get; set; }
            public int Rows { get; set; }
            public double CellWidth { get; set; }
            public double CellHeight { get; set; }
            public double HorizontalGap { get; set; }
            public double VerticalGap { get; set; }

            public override string ToString()
            {
                return $"Grid: {Columns}×{Rows}, Cell: {CellWidth:F2}×{CellHeight:F2} ft";
            }
        }

        // Extension method for detailed logging
        private static void LogDebug(this AutoPlaceSectionsViewModel vm, string message)
        {
            if (vm != null)
            {
                // You can enable/disable debug logging as needed
                // vm.LogInfo($"[DEBUG] {message}");
            }
        }
    }
}