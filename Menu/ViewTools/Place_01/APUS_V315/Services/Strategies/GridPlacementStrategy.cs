using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V315.Helpers;
using Revit26_Plugin.APUS_V315.Models.Entities;
using Revit26_Plugin.APUS_V315.Models.Requests;
using Revit26_Plugin.APUS_V315.Services.Abstractions;
using Revit26_Plugin.APUS_V315.Services.Calculators;
using Revit26_Plugin.APUS_V315.ViewModels.Items;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V315.Services.Strategies;

public sealed class GridPlacementStrategy : IPlacementStrategy
{
    private readonly ISheetService _sheetService;
    private readonly IViewSizeCalculator _sizeCalculator;
    private readonly IGridLayoutCalculator _gridCalculator;
    private readonly ILogService _logService;

    public string Name => "Grid";
    public string Description => "Uniform grid layout with fixed columns. Best for consistent view sizes.";

    public GridPlacementStrategy(
        ISheetService sheetService,
        IViewSizeCalculator sizeCalculator,
        IGridLayoutCalculator gridCalculator,
        ILogService logService)
    {
        _sheetService = sheetService ?? throw new ArgumentNullException(nameof(sheetService));
        _sizeCalculator = sizeCalculator ?? throw new ArgumentNullException(nameof(sizeCalculator));
        _gridCalculator = gridCalculator ?? throw new ArgumentNullException(nameof(gridCalculator));
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

        // Calculate optimal grid layout
        var layout = _gridCalculator.CalculateOptimal(sections, area, gaps.HorizontalMm, gaps.VerticalMm);
        if (layout == null)
        {
            return new PlacementResult(false, 0, Array.Empty<string>(), "Failed to calculate grid layout");
        }

        _logService.LogInfo($"📊 Grid layout: {layout.Columns}×{layout.Rows}, Cell: {layout.CellWidthFeet:F2}′×{layout.CellHeightFeet:F2}′");

        int index = 0;
        while (index < sections.Count && !isCancelled())
        {
            var sheet = _sheetService.CreateSheet(document, titleBlockId, sheetIndex++);
            sheetNumbers.Add(sheet.SheetNumber);
            _logService.LogInfo($"📄 Created sheet: {sheet.SheetNumber}");

            int placedOnSheet = PlaceOnSheet(
                document,
                sheet,
                sections,
                index,
                area,
                layout,
                gaps,
                ref detailNumber,
                ref totalPlaced);

            if (placedOnSheet == 0)
            {
                _sheetService.DeleteSheet(document, sheet.Id);
                sheetNumbers.Remove(sheet.SheetNumber);
                _logService.LogWarning($"⚠️ No views fit on sheet {sheet.SheetNumber}");
                break;
            }

            index += placedOnSheet;
        }

        return new PlacementResult(true, totalPlaced, sheetNumbers);
    }

    private int PlaceOnSheet(
        Document document,
        ViewSheet sheet,
        IReadOnlyList<SectionItemViewModel> sections,
        int startIndex,
        SheetPlacementArea area,
        GridLayout layout,
        Gaps gaps,
        ref int detailNumber,
        ref int totalPlaced)
    {
        int placed = 0;
        int index = startIndex;
        double currentY = area.Origin.Y;
        int currentRow = 0;

        while (index < sections.Count && currentRow < layout.Rows && !document.IsModifiable)
        {
            var rowItems = sections.Skip(index).Take(layout.Columns).ToList();
            if (!rowItems.Any())
                break;

            double rowHeight = rowItems
                .Select(s => _sizeCalculator.Calculate(s.View).HeightFeet)
                .Max() + layout.VerticalGapFeet;

            if (currentY - rowHeight < area.Bottom)
                break;

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

                double x = area.Origin.X + col * (layout.CellWidthFeet + layout.HorizontalGapFeet);
                double centerX = x + layout.CellWidthFeet / 2;
                double viewBottomY = currentY - rowHeight + layout.VerticalGapFeet / 2;
                double centerY = viewBottomY + footprint.HeightFeet / 2;

                var center = new XYZ(centerX, centerY, 0);
                var viewport = Viewport.Create(document, sheet.Id, section.View.Id, center);

                detailNumber++;
                var detailParam = viewport.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                if (detailParam != null && !detailParam.IsReadOnly)
                {
                    detailParam.Set(detailNumber.ToString());
                }

                _logService.LogInfo($"✅ PLACED: {section.ViewName} on {sheet.SheetNumber} (Detail {detailNumber})");

                placed++;
                index++;
                totalPlaced++;
            }

            currentY -= rowHeight;
            currentRow++;
        }

        return placed;
    }
}