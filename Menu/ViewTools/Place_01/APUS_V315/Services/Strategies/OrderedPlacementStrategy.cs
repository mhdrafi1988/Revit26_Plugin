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

public sealed class OrderedPlacementStrategy : IPlacementStrategy
{
    private readonly ISheetService _sheetService;
    private readonly IViewSizeCalculator _sizeCalculator;
    private readonly ILogService _logService;

    public string Name => "Ordered";
    public string Description => "Strict left-to-right, top-to-bottom placement. Maintains spatial order.";

    public OrderedPlacementStrategy(
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
        int sectionIndex = 0;

        // Create temporary sheet for area calculation
        var tempSheet = _sheetService.CreateSheet(document, titleBlockId, 0);
        var area = _sheetService.CalculatePlacementArea(tempSheet, margins);
        _sheetService.DeleteSheet(document, tempSheet.Id);

        double gapX = UnitConversionHelper.MmToFeet(gaps.HorizontalMm);
        double gapY = UnitConversionHelper.MmToFeet(gaps.VerticalMm);

        double left = area.Origin.X + gapX;
        double right = area.Right - gapX;
        double top = area.Origin.Y - gapY;
        double bottom = area.Bottom + gapY;

        while (sectionIndex < sections.Count && !isCancelled())
        {
            var sheet = _sheetService.CreateSheet(document, titleBlockId, sheetIndex++);
            sheetNumbers.Add(sheet.SheetNumber);
            _logService.LogInfo($"📄 Created sheet: {sheet.SheetNumber}");

            double cursorX = left;
            double cursorY = top;
            double rowMaxHeight = 0;
            int placedOnSheet = 0;

            while (sectionIndex < sections.Count)
            {
                var section = sections[sectionIndex];

                if (!Viewport.CanAddViewToSheet(document, sheet.Id, section.View.Id))
                {
                    _logService.LogWarning($"⏭️ SKIPPED (already placed): {section.ViewName}");
                    sectionIndex++;
                    continue;
                }

                var footprint = _sizeCalculator.Calculate(section.View);
                double w = footprint.WidthFeet;
                double h = footprint.HeightFeet;

                // Check if fits on current row
                if (cursorX + w > right)
                {
                    cursorX = left;
                    cursorY -= rowMaxHeight + gapY;
                    rowMaxHeight = 0;
                }

                // Check if fits vertically
                if (cursorY - h < bottom)
                    break;

                double centerX = cursorX + w / 2;
                double centerY = cursorY - h / 2;

                var viewport = Viewport.Create(document, sheet.Id, section.View.Id, new XYZ(centerX, centerY, 0));

                detailNumber++;
                var detailParam = viewport.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                if (detailParam != null && !detailParam.IsReadOnly)
                {
                    detailParam.Set(detailNumber.ToString());
                }

                _logService.LogInfo($"✅ ORDERED PLACEMENT: {section.ViewName} on {sheet.SheetNumber}");

                cursorX += w + gapX;
                rowMaxHeight = Math.Max(rowMaxHeight, h);

                placedOnSheet++;
                totalPlaced++;
                sectionIndex++;
            }

            if (placedOnSheet == 0)
            {
                _sheetService.DeleteSheet(document, sheet.Id);
                sheetNumbers.Remove(sheet.SheetNumber);
                _logService.LogWarning($"⚠️ No views placed on sheet {sheet.SheetNumber}");
                break;
            }
        }

        return new PlacementResult(true, totalPlaced, sheetNumbers);
    }
}