using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V315.Helpers;
using Revit26_Plugin.APUS_V315.Models.Entities;
using Revit26_Plugin.APUS_V315.Models.Requests;
using Revit26_Plugin.APUS_V315.Services.Abstractions;
using Revit26_Plugin.APUS_V315.ViewModels.Items;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V315.Services.Strategies;

public sealed class AdaptiveGridPlacementStrategy : IPlacementStrategy
{
    private readonly ISheetService _sheetService;
    private readonly IViewSizeCalculator _sizeCalculator;
    private readonly ILogService _logService;

    public string Name => "Adaptive Grid";
    public string Description => "Adaptive grid based on view sizes. Groups similar sizes together.";

    public AdaptiveGridPlacementStrategy(
        ISheetService sheetService,
        IViewSizeCalculator sizeCalculator,
        ILogService logService)
    {
        _sheetService = sheetService ?? throw new ArgumentNullException(nameof(sheetService));
        _sizeCalculator = sizeCalculator ?? throw new ArgumentNullException(nameof(sizeCalculator));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
    }

    public PlacementResult Place(
        Document document,
        IReadOnlyList<SectionItemViewModel> sections,
        ElementId titleBlockId,
        Margins margins,
        Gaps gaps,
        Func<bool> isCancelled)
    {
        var sheetNumbers = new List<string>();
        int totalPlaced = 0;
        int detailNumber = 0;
        int sheetIndex = 1;

        // Create temporary sheet for area calculation
        var tempSheet = _sheetService.CreateSheet(document, titleBlockId, 0);
        var area = _sheetService.CalculatePlacementArea(tempSheet, margins);
        _sheetService.DeleteSheet(document, tempSheet.Id);

        // Group sections by size
        var sizeGroups = GroupBySize(sections);
        _logService.LogInfo($"📊 Size groups: {sizeGroups.Count}");

        foreach (var group in sizeGroups)
        {
            if (!group.Any() || isCancelled())
                continue;

            _logService.LogInfo($"📦 Processing group with {group.Count} views");

            // Calculate optimal columns for this group
            int columns = CalculateOptimalColumns(group, area, gaps.HorizontalMm);
            double gapX = UnitConversionHelper.MmToFeet(gaps.HorizontalMm);
            double gapY = UnitConversionHelper.MmToFeet(gaps.VerticalMm);

            double cellWidth = (area.Width - (columns - 1) * gapX) / columns;
            double cellHeight = group.Max(s => _sizeCalculator.Calculate(s.View).HeightFeet) + gapY;

            _logService.LogInfo($"   • Columns: {columns}, Cell: {cellWidth:F2}′×{cellHeight:F2}′");

            int groupIndex = 0;
            while (groupIndex < group.Count && !isCancelled())
            {
                var sheet = _sheetService.CreateSheet(document, titleBlockId, sheetIndex++);
                sheetNumbers.Add(sheet.SheetNumber);
                _logService.LogInfo($"📄 Created sheet: {sheet.SheetNumber}");

                int placedOnSheet = PlaceOnSheet(
                    document,
                    sheet,
                    group,
                    groupIndex,
                    area,
                    columns,
                    cellWidth,
                    cellHeight,
                    gapX,
                    gapY,
                    ref detailNumber,
                    ref totalPlaced);

                if (placedOnSheet == 0)
                {
                    _sheetService.DeleteSheet(document, sheet.Id);
                    sheetNumbers.Remove(sheet.SheetNumber);
                    break;
                }

                groupIndex += placedOnSheet;
            }
        }

        return new PlacementResult(true, totalPlaced, sheetNumbers);
    }

    private List<List<SectionItemViewModel>> GroupBySize(IReadOnlyList<SectionItemViewModel> sections)
    {
        var groups = new List<List<SectionItemViewModel>>();
        var small = new List<SectionItemViewModel>();
        var medium = new List<SectionItemViewModel>();
        var large = new List<SectionItemViewModel>();
        var extraLarge = new List<SectionItemViewModel>();

        foreach (var section in sections)
        {
            var footprint = _sizeCalculator.Calculate(section.View);
            double area = footprint.WidthFeet * footprint.HeightFeet;

            if (area < 2.0) small.Add(section);
            else if (area < 5.0) medium.Add(section);
            else if (area < 10.0) large.Add(section);
            else extraLarge.Add(section);
        }

        if (extraLarge.Any()) groups.Add(extraLarge);
        if (large.Any()) groups.Add(large);
        if (medium.Any()) groups.Add(medium);
        if (small.Any()) groups.Add(small);

        return groups;
    }

    private int CalculateOptimalColumns(
        List<SectionItemViewModel> group,
        SheetPlacementArea area,
        double horizontalGapMm)
    {
        double avgWidth = group.Average(s => _sizeCalculator.Calculate(s.View).WidthFeet);
        double gapX = UnitConversionHelper.MmToFeet(horizontalGapMm);

        int maxColumns = (int)Math.Floor((area.Width + gapX) / (avgWidth + gapX));
        maxColumns = Math.Max(1, maxColumns);

        int optimalColumns = (int)Math.Ceiling(Math.Sqrt(group.Count));
        return Math.Max(1, Math.Min(optimalColumns, maxColumns));
    }

    private int PlaceOnSheet(
        Document document,
        ViewSheet sheet,
        List<SectionItemViewModel> group,
        int startIndex,
        SheetPlacementArea area,
        int columns,
        double cellWidth,
        double cellHeight,
        double gapX,
        double gapY,
        ref int detailNumber,
        ref int totalPlaced)
    {
        int placed = 0;
        int index = startIndex;
        double currentY = area.Origin.Y;

        while (index < group.Count)
        {
            var rowItems = group.Skip(index).Take(columns).ToList();
            if (!rowItems.Any())
                break;

            // Calculate row height (tallest in row)
            double rowHeight = rowItems
                .Select(s => _sizeCalculator.Calculate(s.View).HeightFeet)
                .Max() + gapY;

            if (currentY - rowHeight < area.Bottom)
                break;

            // Place row
            for (int col = 0; col < rowItems.Count; col++)
            {
                var section = rowItems[col];

                if (!Viewport.CanAddViewToSheet(document, sheet.Id, section.View.Id))
                {
                    _logService.LogWarning($"⏭️ SKIPPED (already placed): {section.ViewName}");
                    index++;
                    continue;
                }

                var footprint = _sizeCalculator.Calculate(section.View);

                double x = area.Origin.X + col * (cellWidth + gapX);
                double centerX = x + cellWidth / 2;
                double viewBottomY = currentY - rowHeight + gapY / 2;
                double centerY = viewBottomY + footprint.HeightFeet / 2;

                var viewport = Viewport.Create(document, sheet.Id, section.View.Id, new XYZ(centerX, centerY, 0));

                detailNumber++;
                var detailParam = viewport.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                if (detailParam != null && !detailParam.IsReadOnly)
                {
                    detailParam.Set(detailNumber.ToString());
                }

                _logService.LogInfo($"✅ PLACED: {section.ViewName} on {sheet.SheetNumber}");

                placed++;
                index++;
                totalPlaced++;
            }

            currentY -= rowHeight;
        }

        return placed;
    }
}