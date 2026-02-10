// File: GridLayoutCalculationService.cs
using Revit26_Plugin.APUS_V314.Models;
using Revit26_Plugin.APUS_V314.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using Revit26_Plugin.APUS_V314.Helpers;

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
            out int rows)
        {
            cellWidthFt = 0;
            cellHeightFt = 0;
            columns = 0;
            rows = 0;

            if (sections == null || sections.Count == 0)
                return false;

            // Get all view dimensions
            var footprints = sections
                .Select(x => ViewSizeService.Calculate(x.View))
                .Where(f => f.WidthFt > 0 && f.HeightFt > 0)
                .ToList();

            if (!footprints.Any())
                return false;

            // Convert gaps to feet
            double gapX = UnitConversionHelper.MmToFeet(horizontalGapMm);
            double gapY = UnitConversionHelper.MmToFeet(verticalGapMm);

            // Calculate statistics
            double maxWidth = footprints.Max(f => f.WidthFt);
            double maxHeight = footprints.Max(f => f.HeightFt);
            double avgWidth = footprints.Average(f => f.WidthFt);
            double avgHeight = footprints.Average(f => f.HeightFt);
            double medianWidth = CalculateMedian(footprints.Select(f => f.WidthFt).ToList());

            // Calculate optimal cell width (median + padding)
            cellWidthFt = Math.Max(maxWidth, medianWidth * 1.2);

            // Calculate maximum possible columns
            int maxColumns = (int)Math.Floor((area.Width + gapX) / (cellWidthFt + gapX));
            columns = Math.Max(1, Math.Min(maxColumns, sections.Count));

            // Adjust cell width to fit exactly with gaps
            cellWidthFt = (area.Width - (columns - 1) * gapX) / columns;

            // Calculate cell height (tallest view in collection)
            cellHeightFt = maxHeight;

            // Calculate rows based on available height
            double availableHeight = area.Height;
            rows = (int)Math.Floor((availableHeight + gapY) / (cellHeightFt + gapY));

            return columns > 0 && rows > 0;
        }

        public static bool TryCalculateAdaptive(
            List<SectionItemViewModel> sections,
            SheetPlacementArea area,
            double horizontalGapMm,
            double verticalGapMm,
            out GridLayout layout)
        {
            layout = null;

            if (sections == null || !sections.Any())
                return false;

            // Group by similar widths
            var widthGroups = GroupByWidth(sections);
            var heightGroups = GroupByHeight(sections);

            // Calculate optimal columns for each width group
            var columnOptions = new List<int>();
            foreach (var group in widthGroups)
            {
                if (TryCalculateOptimalColumns(group, area, horizontalGapMm, out int cols))
                {
                    columnOptions.Add(cols);
                }
            }

            if (!columnOptions.Any())
                return false;

            // Use median of column options
            int optimalColumns = CalculateMedian(columnOptions);

            // Calculate cell dimensions
            double gapX = UnitConversionHelper.MmToFeet(horizontalGapMm);
            double cellWidth = (area.Width - (optimalColumns - 1) * gapX) / optimalColumns;

            // Find tallest view for cell height
            double maxHeight = sections.Max(s => ViewSizeService.Calculate(s.View).HeightFt);
            double cellHeight = maxHeight + UnitConversionHelper.MmToFeet(verticalGapMm);

            layout = new GridLayout
            {
                Columns = optimalColumns,
                Rows = 0, // Will be calculated per sheet
                CellWidth = cellWidth,
                CellHeight = cellHeight,
                HorizontalGap = gapX,
                VerticalGap = UnitConversionHelper.MmToFeet(verticalGapMm)
            };

            return true;
        }

        private static bool TryCalculateOptimalColumns(
            List<SectionItemViewModel> group,
            SheetPlacementArea area,
            double horizontalGapMm,
            out int columns)
        {
            columns = 0;

            if (!group.Any()) return false;

            // Get average width for this group
            double avgWidth = group.Average(s => ViewSizeService.Calculate(s.View).WidthFt);
            double gapX = UnitConversionHelper.MmToFeet(horizontalGapMm);

            // Calculate maximum columns that fit
            int maxColumns = (int)Math.Floor((area.Width + gapX) / (avgWidth + gapX));

            // Use square-ish layout preference
            int optimalColumns = (int)Math.Ceiling(Math.Sqrt(group.Count));
            columns = Math.Max(1, Math.Min(optimalColumns, maxColumns));

            return columns > 0;
        }

        private static List<List<SectionItemViewModel>> GroupByWidth(List<SectionItemViewModel> sections)
        {
            var groups = new List<List<SectionItemViewModel>>();
            var sorted = sections.OrderBy(s => ViewSizeService.Calculate(s.View).WidthFt).ToList();

            if (!sorted.Any()) return groups;

            double firstWidth = ViewSizeService.Calculate(sorted[0].View).WidthFt;
            var currentGroup = new List<SectionItemViewModel> { sorted[0] };

            for (int i = 1; i < sorted.Count; i++)
            {
                double currentWidth = ViewSizeService.Calculate(sorted[i].View).WidthFt;

                // Group if within 25% of first item in group
                if (Math.Abs(currentWidth - firstWidth) / firstWidth <= 0.25)
                {
                    currentGroup.Add(sorted[i]);
                }
                else
                {
                    groups.Add(currentGroup);
                    currentGroup = new List<SectionItemViewModel> { sorted[i] };
                    firstWidth = currentWidth;
                }
            }

            if (currentGroup.Any())
                groups.Add(currentGroup);

            return groups.Where(g => g.Count >= 2).ToList(); // Only keep groups with multiple items
        }

        private static List<List<SectionItemViewModel>> GroupByHeight(List<SectionItemViewModel> sections)
        {
            var groups = new List<List<SectionItemViewModel>>();
            var sorted = sections.OrderBy(s => ViewSizeService.Calculate(s.View).HeightFt).ToList();

            if (!sorted.Any()) return groups;

            double firstHeight = ViewSizeService.Calculate(sorted[0].View).HeightFt;
            var currentGroup = new List<SectionItemViewModel> { sorted[0] };

            for (int i = 1; i < sorted.Count; i++)
            {
                double currentHeight = ViewSizeService.Calculate(sorted[i].View).HeightFt;

                if (Math.Abs(currentHeight - firstHeight) / firstHeight <= 0.25)
                {
                    currentGroup.Add(sorted[i]);
                }
                else
                {
                    groups.Add(currentGroup);
                    currentGroup = new List<SectionItemViewModel> { sorted[i] };
                    firstHeight = currentHeight;
                }
            }

            if (currentGroup.Any())
                groups.Add(currentGroup);

            return groups.Where(g => g.Count >= 2).ToList();
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
                return $"Grid: {Columns}×{Rows}, Cell: {CellWidth:F2}×{CellHeight:F2}";
            }
        }
    }
}