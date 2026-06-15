// File: MultiSheetBinPlacementService.cs
// REFACTORED - Works within transaction, no transaction management
using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V314.ExternalEvents;
using Revit26_Plugin.APUS_V314.Helpers;
using Revit26_Plugin.APUS_V314.Models;
using Revit26_Plugin.APUS_V314.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V314.Services
{
    /// <summary>
    /// Bin packing placement across multiple sheets.
    /// CRITICAL: Assumes active transaction exists.
    /// </summary>
    public class MultiSheetBinPlacementService
    {
        private readonly Document _doc;
        private readonly SheetCreationService _sheetCreator;

        public MultiSheetBinPlacementService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _sheetCreator = new SheetCreationService(doc);
        }

        public SectionPlacementHandler.PlacementResult Place(
            SectionPlacementHandler.PlacementContext context,
            List<SectionItemViewModel> sections)
        {
            var result = new SectionPlacementHandler.PlacementResult();

            if (sections == null || !sections.Any())
            {
                context.ViewModel?.LogWarning("No sections to place.");
                result.ErrorMessage = "No sections to place";
                return result;
            }

            try
            {
                // Calculate offsets and usable area
                var offsets = CalculateOffsets(context.ViewModel);
                double gapFt = UnitConversionHelper.MmToFeet(context.HorizontalGapMm);

                double usableWidth = context.PlacementArea.Width - offsets.LeftFt - offsets.RightFt;
                double usableHeight = context.PlacementArea.Height - offsets.TopFt - offsets.BottomFt;

                if (usableWidth <= 0 || usableHeight <= 0)
                {
                    context.ViewModel?.LogError("Invalid usable area after offsets.");
                    result.ErrorMessage = "Invalid usable area";
                    return result;
                }

                context.ViewModel?.LogInfo($"Bin packing area: {usableWidth:F2} × {usableHeight:F2} ft");

                int sectionIndex = 0;
                int sheetCount = 0;

                while (sectionIndex < sections.Count)
                {
                    if (context.ViewModel?.Progress.IsCancelled == true)
                    {
                        context.ViewModel?.LogWarning("Placement cancelled.");
                        break;
                    }

                    // Create new sheet with unique number
                    string sheetNumber = context.SheetNumberService.GetNextAvailableSheetNumber("AP");
                    context.SheetNumberService.TryReserveSheetNumber(sheetNumber);

                    var sheet = _sheetCreator.Create(context.TitleBlock, sheetNumber, $"APUS-{sheetNumber}");
                    context.ViewModel?.LogInfo($"Created sheet: {sheet.SheetNumber}");

                    // Create bin packer for this sheet
                    var packer = new BinPackerService(usableWidth, usableHeight);

                    // Place views on current sheet
                    int viewsPlacedOnSheet = 0;
                    while (sectionIndex < sections.Count)
                    {
                        var item = sections[sectionIndex];

                        if (!CanPlaceView(item.View, sheet.Id))
                        {
                            context.ViewModel?.LogWarning($"SKIPPED (already placed): {item.ViewName}");
                            sectionIndex++;
                            result.FailedCount++;
                            continue;
                        }

                        var footprint = ViewSizeService.Calculate(item.View);
                        double w = footprint.WidthFt + gapFt;
                        double h = footprint.HeightFt + gapFt;

                        // Try to pack
                        if (!packer.TryPack(w, h, out double packX, out double packY))
                        {
                            // Current sheet is full
                            double efficiency = packer.CalculateEfficiency(
                                CalculatePlacedArea(packer, usableWidth, usableHeight));
                            context.ViewModel?.LogInfo(
                                $"Sheet {sheet.SheetNumber} full ({efficiency:P0})");
                            break;
                        }

                        // Calculate position and create viewport
                        bool placed = PlaceViewport(
                            sheet,
                            item,
                            context.PlacementArea,
                            offsets,
                            packX,
                            packY,
                            w,
                            h,
                            context.GetNextDetailNumber());

                        if (placed)
                        {
                            context.ViewModel?.LogInfo(
                                $"BIN PACKED: {item.ViewName} on {sheet.SheetNumber}");
                            context.ViewModel?.Progress.Step();

                            result.PlacedCount++;
                            result.SheetNumbers.Add(sheet.SheetNumber);
                            sectionIndex++;
                            viewsPlacedOnSheet++;
                        }
                        else
                        {
                            context.ViewModel?.LogWarning(
                                $"FAILED: Could not place {item.ViewName}");
                            sectionIndex++;
                            result.FailedCount++;
                        }
                    }

                    // If no views placed on this sheet, remove it
                    if (viewsPlacedOnSheet == 0)
                    {
                        _doc.Delete(sheet.Id);
                        context.ViewModel?.LogWarning(
                            $"Removed empty sheet: {sheet.SheetNumber}");
                    }
                    else
                    {
                        sheetCount++;
                    }

                    // Log sheet efficiency
                    double sheetEfficiency = packer.CalculateEfficiency(
                        CalculatePlacedArea(packer, usableWidth, usableHeight));
                    context.ViewModel?.LogInfo(
                        $"Sheet {sheet.SheetNumber} efficiency: {sheetEfficiency:P0}");
                }

                context.ViewModel?.LogInfo(
                    $"Bin packing complete: {result.PlacedCount} placed, {result.FailedCount} failed on {sheetCount} sheets");

                return result;
            }
            catch (Exception ex)
            {
                context.ViewModel?.LogError($"Bin packing failed: {ex.Message}");
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        private bool CanPlaceView(ViewSection view, ElementId sheetId)
        {
            try
            {
                return Viewport.CanAddViewToSheet(_doc, sheetId, view.Id);
            }
            catch
            {
                return false;
            }
        }

        private bool PlaceViewport(
            ViewSheet sheet,
            SectionItemViewModel item,
            SheetPlacementArea area,
            Offsets offsets,
            double packX,
            double packY,
            double widthWithGap,
            double heightWithGap,
            int detailNumber)
        {
            try
            {
                // Convert to sheet coordinates
                double sheetX = area.Origin.X + offsets.LeftFt + packX;
                double sheetY = area.Origin.Y - offsets.TopFt - packY;

                // Calculate viewport center
                XYZ center = new XYZ(
                    sheetX + widthWithGap / 2,
                    sheetY - heightWithGap / 2,
                    0);

                // Create viewport
                var vp = Viewport.Create(_doc, sheet.Id, item.View.Id, center);

                // Set detail number
                var detailParam = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                if (detailParam != null && !detailParam.IsReadOnly)
                {
                    detailParam.Set(detailNumber.ToString());
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private double CalculatePlacedArea(BinPackerService packer, double binWidth, double binHeight)
        {
            double binArea = binWidth * binHeight;
            double freeArea = packer.GetFreeArea();
            return binArea - freeArea;
        }

        private Offsets CalculateOffsets(AutoPlaceSectionsViewModel vm)
        {
            return new Offsets
            {
                LeftFt = UnitConversionHelper.MmToFeet(vm?.LeftMarginMm ?? 40),
                RightFt = UnitConversionHelper.MmToFeet(vm?.RightMarginMm ?? 150),
                TopFt = UnitConversionHelper.MmToFeet(vm?.TopMarginMm ?? 40),
                BottomFt = UnitConversionHelper.MmToFeet(vm?.BottomMarginMm ?? 100)
            };
        }

        private struct Offsets
        {
            public double LeftFt;
            public double RightFt;
            public double TopFt;
            public double BottomFt;
        }
    }
}