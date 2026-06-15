using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V315.Helpers;
using Revit26_Plugin.APUS_V315.Models.Requests;
using Revit26_Plugin.APUS_V315.Services.Abstractions;
using Revit26_Plugin.APUS_V315.Services.Calculators;
using Revit26_Plugin.APUS_V315.ViewModels.Items;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V315.Services.Strategies;

public sealed class BinPackingPlacementStrategy : IPlacementStrategy
{
    private readonly ISheetService _sheetService;
    private readonly IViewSizeCalculator _sizeCalculator;
    private readonly ILogService _logService;

    public string Name => "Bin Packing";
    public string Description => "Space-optimized packing. Maximizes sheet usage efficiency.";

    public BinPackingPlacementStrategy(
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

        double gapFt = UnitConversionHelper.MmToFeet(gaps.HorizontalMm);
        double usableWidth = area.Width;
        double usableHeight = area.Height;

        _logService.LogInfo($"📦 Bin packing area: {usableWidth:F2}′ × {usableHeight:F2}′");

        while (sectionIndex < sections.Count && !isCancelled())
        {
            var sheet = _sheetService.CreateSheet(document, titleBlockId, sheetIndex++);
            sheetNumbers.Add(sheet.SheetNumber);
            _logService.LogInfo($"📄 Created sheet: {sheet.SheetNumber}");

            var packer = new BinPackerService(usableWidth, usableHeight);
            double placedArea = 0;

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
                double w = footprint.WidthFeet + gapFt;
                double h = footprint.HeightFeet + gapFt;

                if (!packer.TryPack(w, h, out double packX, out double packY))
                {
                    _logService.LogInfo($"📊 Sheet {sheet.SheetNumber} packing efficiency: {packer.CalculateEfficiency(placedArea):P0}");
                    break;
                }

                // Convert to sheet coordinates
                double sheetX = area.Origin.X + packX;
                double sheetY = area.Origin.Y - packY;

                var center = new XYZ(
                    sheetX + w / 2,
                    sheetY - h / 2,
                    0);

                var viewport = Viewport.Create(document, sheet.Id, section.View.Id, center);

                detailNumber++;
                var detailParam = viewport.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                if (detailParam != null && !detailParam.IsReadOnly)
                {
                    detailParam.Set(detailNumber.ToString());
                }

                placedArea += footprint.WidthFeet * footprint.HeightFeet;
                _logService.LogInfo($"✅ BIN PACKED: {section.ViewName} on {sheet.SheetNumber}");

                totalPlaced++;
                sectionIndex++;
            }
        }

        return new PlacementResult(true, totalPlaced, sheetNumbers);
    }
}