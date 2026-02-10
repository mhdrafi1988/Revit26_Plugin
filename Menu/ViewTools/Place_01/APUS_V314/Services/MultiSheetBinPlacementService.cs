// File: MultiSheetBinPlacementService.cs
using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V314.Helpers;
using Revit26_Plugin.APUS_V314.Models;
using Revit26_Plugin.APUS_V314.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Revit26_Plugin.APUS_V314.Services
{
    /// <summary>
    /// Bin packing placement across multiple sheets
    /// </summary>
    public class MultiSheetBinPlacementService
    {
        private readonly Document _doc;
        private readonly SheetCreationService _sheetCreator;

        public MultiSheetBinPlacementService(Document doc)
        {
            _doc = doc;
            _sheetCreator = new SheetCreationService(doc);
        }

        public void Place(
            IList<SectionItemViewModel> sections,
            FamilySymbol titleBlock,
            SheetPlacementArea area,
            double gapMm,
            AutoPlaceSectionsViewModel vm,
            ref int placedCount,
            ref int failedCount,
            ref HashSet<string> sheetNumbers)
        {
            if (sections == null || !sections.Any())
            {
                vm.LogWarning("No sections to place.");
                return;
            }

            try
            {
                // Read offsets from ViewModel
                var offsets = ReadOffsetsFromViewModel(vm);
                double gapFt = UnitConversionHelper.MmToFeet(gapMm);

                // Calculate usable packing area
                double usableWidth = area.Width - offsets.LeftFt - offsets.RightFt;
                double usableHeight = area.Height - offsets.TopFt - offsets.BottomFt;

                if (usableWidth <= 0 || usableHeight <= 0)
                {
                    vm.LogError("Invalid usable area after offsets.");
                    return;
                }

                vm.LogInfo($"Bin packing area: {usableWidth:F2} × {usableHeight:F2} ft");

                int sheetIndex = 1;
                int detailIndex = 0;
                int sectionIndex = 0;

                while (sectionIndex < sections.Count)
                {
                    if (vm.Progress.IsCancelled)
                    {
                        vm.LogWarning("Placement cancelled.");
                        break;
                    }

                    // Create new sheet
                    ViewSheet sheet = _sheetCreator.Create(titleBlock, sheetIndex++);
                    vm.LogInfo($"CREATED SHEET: {sheet.SheetNumber}");
                    sheetNumbers.Add(sheet.SheetNumber);

                    // Create bin packer for this sheet
                    var packer = new BinPackerService(usableWidth, usableHeight);

                    // Place views on current sheet
                    while (sectionIndex < sections.Count)
                    {
                        var item = sections[sectionIndex];

                        if (!Viewport.CanAddViewToSheet(_doc, sheet.Id, item.View.Id))
                        {
                            vm.LogWarning($"SKIPPED (already placed): {item.ViewName}");
                            sectionIndex++;
                            failedCount++;
                            continue;
                        }

                        var footprint = ViewSizeService.Calculate(item.View);
                        double w = footprint.WidthFt + gapFt;
                        double h = footprint.HeightFt + gapFt;

                        // Try to pack
                        if (!packer.TryPack(w, h, out double packX, out double packY))
                        {
                            // Current sheet is full
                            vm.LogInfo($"Sheet {sheet.SheetNumber} full ({packer.CalculateEfficiency(0):P0})");
                            break;
                        }

                        // Convert to sheet coordinates
                        double sheetX = area.Origin.X + offsets.LeftFt + packX;
                        double sheetY = area.Origin.Y - offsets.TopFt - packY;

                        // Viewport center
                        XYZ center = new XYZ(
                            sheetX + w / 2,
                            sheetY - h / 2,
                            0);

                        Viewport vp = Viewport.Create(_doc, sheet.Id, item.View.Id, center);

                        // Set detail number
                        detailIndex++;
                        var detailParam = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                        if (detailParam != null && !detailParam.IsReadOnly)
                            detailParam.Set(detailIndex.ToString());

                        vm.LogInfo($"BIN PACKED: {item.ViewName} on {sheet.SheetNumber}");
                        vm.Progress.Step();

                        placedCount++;
                        sectionIndex++;
                    }

                    // Calculate and log packing efficiency
                    double efficiency = packer.CalculateEfficiency(CalculatePlacedArea(packer, usableWidth, usableHeight));
                    vm.LogInfo($"Packing efficiency: {efficiency:P0}");
                }

                vm.LogInfo($"Bin packing complete: {placedCount} placed, {failedCount} failed");
            }
            catch (Exception ex)
            {
                vm.LogError($"Bin packing failed: {ex.Message}");
            }
        }

        private double CalculatePlacedArea(BinPackerService packer, double binWidth, double binHeight)
        {
            double binArea = binWidth * binHeight;
            double freeArea = packer.GetFreeArea();
            return binArea - freeArea;
        }

        private Offsets ReadOffsetsFromViewModel(AutoPlaceSectionsViewModel vm)
        {
            var offsets = new Offsets();

            // Use reflection to read offset properties
            var vmType = vm.GetType();

            offsets.LeftFt = UnitConversionHelper.MmToFeet(ReadDoubleProperty(vmType, vm,
                "LeftMarginMm", "OffsetLeftMm", "PlacementOffsetLeftMm"));

            offsets.RightFt = UnitConversionHelper.MmToFeet(ReadDoubleProperty(vmType, vm,
                "RightMarginMm", "OffsetRightMm", "PlacementOffsetRightMm"));

            offsets.TopFt = UnitConversionHelper.MmToFeet(ReadDoubleProperty(vmType, vm,
                "TopMarginMm", "OffsetTopMm", "PlacementOffsetTopMm"));

            offsets.BottomFt = UnitConversionHelper.MmToFeet(ReadDoubleProperty(vmType, vm,
                "BottomMarginMm", "OffsetBottomMm", "PlacementOffsetBottomMm"));

            return offsets;
        }

        private double ReadDoubleProperty(Type type, object obj, params string[] propertyNames)
        {
            foreach (var name in propertyNames)
            {
                var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (prop != null)
                {
                    var value = prop.GetValue(obj);
                    if (value is double d) return d;
                    if (value is int i) return i;
                    if (value != null && double.TryParse(value.ToString(), out double parsed))
                        return parsed;
                }
            }
            return 0;
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