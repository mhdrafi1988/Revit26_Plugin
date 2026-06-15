using Revit26_Plugin.APUS_V315.Helpers;
using Revit26_Plugin.APUS_V315.Models.Entities;
using Revit26_Plugin.APUS_V315.Services.Abstractions;
using Revit26_Plugin.APUS_V315.Services.Calculators;
using Revit26_Plugin.APUS_V315.ViewModels.Items;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V315.Services.Calculators;

public sealed class GridLayout
{
    public int Columns { get; }
    public int Rows { get; }
    public double CellWidthFeet { get; }
    public double CellHeightFeet { get; }
    public double HorizontalGapFeet { get; }
    public double VerticalGapFeet { get; }

    public GridLayout(int columns, int rows, double cellWidthFeet, double cellHeightFeet,
                      double horizontalGapFeet, double verticalGapFeet)
    {
        Columns = columns;
        Rows = rows;
        CellWidthFeet = cellWidthFeet;
        CellHeightFeet = cellHeightFeet;
        HorizontalGapFeet = horizontalGapFeet;
        VerticalGapFeet = verticalGapFeet;
    }
}

public interface IGridLayoutCalculator
{
    GridLayout? CalculateOptimal(
        IReadOnlyList<SectionItemViewModel> sections,
        SheetPlacementArea area,
        double horizontalGapMm,
        double verticalGapMm);
}

public sealed class GridLayoutCalculator : IGridLayoutCalculator
{
    private readonly IViewSizeCalculator _sizeCalculator;

    public GridLayoutCalculator(IViewSizeCalculator sizeCalculator)
    {
        _sizeCalculator = sizeCalculator ?? throw new ArgumentNullException(nameof(sizeCalculator));
    }

    public GridLayout? CalculateOptimal(
        IReadOnlyList<SectionItemViewModel> sections,
        SheetPlacementArea area,
        double horizontalGapMm,
        double verticalGapMm)
    {
        if (sections == null || !sections.Any() || area == null)
            return null;

        var footprints = sections
            .Select(s => _sizeCalculator.Calculate(s.View))
            .Where(f => f.WidthFeet > 0 && f.HeightFeet > 0)
            .ToList();

        if (!footprints.Any())
            return null;

        double gapX = UnitConversionHelper.MmToFeet(horizontalGapMm);
        double gapY = UnitConversionHelper.MmToFeet(verticalGapMm);

        double maxWidth = footprints.Max(f => f.WidthFeet);
        double maxHeight = footprints.Max(f => f.HeightFeet);
        double avgWidth = footprints.Average(f => f.WidthFeet);

        double targetCellWidth = Math.Max(maxWidth, avgWidth * 1.2);
        int maxColumns = (int)Math.Floor((area.Width + gapX) / (targetCellWidth + gapX));
        int optimalColumns = Math.Max(1, Math.Min(maxColumns, sections.Count));

        double cellWidth = (area.Width - (optimalColumns - 1) * gapX) / optimalColumns;
        double cellHeight = maxHeight + gapY;

        int rows = (int)Math.Floor((area.Height + gapY) / (cellHeight + gapY));
        rows = Math.Max(1, rows);

        return new GridLayout(
            optimalColumns,
            rows,
            cellWidth,
            cellHeight,
            gapX,
            gapY
        );
    }
}