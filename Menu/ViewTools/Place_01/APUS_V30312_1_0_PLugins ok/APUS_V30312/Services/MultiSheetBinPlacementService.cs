// File: MultiSheetBinPlacementService.cs
using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V312.Helpers;
using Revit26_Plugin.APUS_V312.Models;
using Revit26_Plugin.APUS_V312.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace Revit26_Plugin.APUS_V312.Services
{
    /// <summary>
    /// Packs sorted section views across multiple sheets using bin packing (order-preserving first-fit).
    /// Creates new sheets when current sheet is full.
    ///
    /// IMPORTANT:
    /// - BinPacker uses local coordinates (0,0) = top-left of the usable packing area.
    /// - UI offsets must be applied ONLY when translating bin coords -> sheet coords.
    /// </summary>
    public class MultiSheetBinPlacementService
    {
        private readonly Document _doc;
        private readonly SheetCreationService _sheetCreator;

        public MultiSheetBinPlacementService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _sheetCreator = new SheetCreationService(_doc);
        }

        /// <summary>
        /// Places views in sorted order using bin packing across as many sheets as needed.
        /// Offsets are pulled from the ViewModel (common property names supported).
        /// </summary>
        public void PlaceSections(
            IList<SectionItemViewModel> sortedSections,
            FamilySymbol titleBlock,
            SheetPlacementArea area,
            double gapMm,
            AutoPlaceSectionsViewModel vm)
        {
            if (sortedSections == null || sortedSections.Count == 0)
            {
                vm?.LogWarning("No sections to place.");
                return;
            }

            if (titleBlock == null)
                throw new ArgumentNullException(nameof(titleBlock));

            // ------------------------------------------------------
            // Read UI offsets (mm) from VM, with safe fallback to 0.
            // ------------------------------------------------------
            // Supported property name aliases:
            // - OffsetLeftMm / LeftOffsetMm / PlacementOffsetLeftMm / SheetOffsetLeftMm
            // - OffsetTopMm  / TopOffsetMm  / PlacementOffsetTopMm  / SheetOffsetTopMm
            // - OffsetRightMm / OffsetBottomMm (optional)
            var offsetsMm = ReadOffsetsFromViewModel(vm);

            double gapFt = UnitConversionHelper.MmToFeet(gapMm);

            double offsetLeftFt = UnitConversionHelper.MmToFeet(offsetsMm.LeftMm);
            double offsetTopFt = UnitConversionHelper.MmToFeet(offsetsMm.TopMm);
            double offsetRightFt = UnitConversionHelper.MmToFeet(offsetsMm.RightMm);
            double offsetBottomFt = UnitConversionHelper.MmToFeet(offsetsMm.BottomMm);

            // ------------------------------------------------------
            // Compute usable packing dimensions.
            // IMPORTANT: we reduce packable width/height by offsets
            // so bin packing cannot "use" reserved margin areas.
            // ------------------------------------------------------
            double usableWidthFt = area.Width - offsetLeftFt - offsetRightFt - gapFt;
            double usableHeightFt = area.Height - offsetTopFt - offsetBottomFt - gapFt;

            if (usableWidthFt <= 0 || usableHeightFt <= 0)
            {
                vm?.LogWarning(
                    $"Invalid usable area after offsets/gap. " +
                    $"WidthFt={usableWidthFt:F3}, HeightFt={usableHeightFt:F3}. Placement aborted.");
                return;
            }

            vm?.LogInfo(
                $"Using UI Offsets (mm): L={offsetsMm.LeftMm}, T={offsetsMm.TopMm}, R={offsetsMm.RightMm}, B={offsetsMm.BottomMm}. " +
                $"Gap={gapMm}mm");

            int sheetIndex = 1;
            int detailIndex = 0;
            int sectionCursor = 0;

            // Caller should own the Transaction scope (recommended).
            // This service is transaction-agnostic by design.
            while (sectionCursor < sortedSections.Count)
            {
                // ------------------------------------------------------
                // Create new sheet
                // ------------------------------------------------------
                ViewSheet sheet = _sheetCreator.Create(titleBlock, sheetIndex++);
                vm?.LogInfo($"CREATED SHEET: {sheet.SheetNumber} ({sheet.Name})");

                // ------------------------------------------------------
                // Create bin packer for this sheet's usable area
                // ------------------------------------------------------
                var packer = new BinPackerService(usableWidthFt, usableHeightFt);

                // ------------------------------------------------------
                // Try placing sections until sheet is full
                // ------------------------------------------------------
                while (sectionCursor < sortedSections.Count)
                {
                    SectionItemViewModel item = sortedSections[sectionCursor];
                    if (item?.View == null)
                    {
                        vm?.LogWarning("SKIPPED (null view item).");
                        sectionCursor++;
                        continue;
                    }

                    // Hard safety: skip already-placed views
                    if (!Viewport.CanAddViewToSheet(_doc, sheet.Id, item.View.Id))
                    {
                        vm?.LogWarning($"SKIPPED (already placed): {item.ViewName}");
                        sectionCursor++;
                        continue;
                    }

                    // Calculate conservative paper footprint (feet)
                    SectionFootprint fp = ViewSizeService.Calculate(item.View);

                    if (fp.WidthFt <= 0 || fp.HeightFt <= 0)
                    {
                        vm?.LogWarning($"SKIPPED (invalid size): {item.ViewName}");
                        sectionCursor++;
                        continue;
                    }

                    // Add gap around each view footprint (keeps spacing deterministic)
                    double w = fp.WidthFt + gapFt;
                    double h = fp.HeightFt + gapFt;

                    // --------------------------------------------------
                    // Attempt bin placement in local bin coords
                    // --------------------------------------------------
                    if (!packer.TryPlace(w, h, out double binX, out double binY))
                    {
                        // Current sheet is full -> create next sheet
                        vm?.LogInfo($"SHEET FULL ({sheet.SheetNumber}) -> creating next sheet");
                        break; // exit inner loop; outer loop will create next sheet
                    }

                    // --------------------------------------------------
                    // Translate bin coords -> sheet coords
                    //
                    // Bin coords:
                    //   (0,0) is top-left of usable packing area.
                    //
                    // Sheet coords:
                    //   area.Origin is top-left of sheet placement area.
                    //
                    // UI offsets shift the usable packing origin inside the area.
                    // --------------------------------------------------
                    double sheetX = area.Origin.X + offsetLeftFt + binX;
                    double sheetY = area.Origin.Y - offsetTopFt - binY;

                    // Viewport.Create expects CENTER point.
                    XYZ center = new XYZ(
                        sheetX + w / 2.0,
                        sheetY - h / 2.0,
                        0);

                    Viewport viewport = Viewport.Create(_doc, sheet.Id, item.View.Id, center);

                    // --------------------------------------------------
                    // Continuous detail numbering across all sheets
                    // --------------------------------------------------
                    detailIndex++;
                    Parameter detailParam = viewport.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                    if (detailParam != null && !detailParam.IsReadOnly)
                        detailParam.Set(detailIndex.ToString(CultureInfo.InvariantCulture));

                    vm?.LogInfo($"PLACED: {item.ViewName} on {sheet.SheetNumber} (Detail {detailIndex})");
                    vm?.Progress?.Step();

                    sectionCursor++;
                }
            }
        }

        /// <summary>
        /// Reads common offset properties from the ViewModel using reflection so we don't require
        /// you to change your VM class to compile this service.
        ///
        /// If your VM uses different property names, add aliases here.
        /// </summary>
        private static OffsetsMm ReadOffsetsFromViewModel(object vm)
        {
            // Safe defaults
            var result = new OffsetsMm(0, 0, 0, 0);

            if (vm == null)
                return result;

            // Common aliases used in UIs
            result.LeftMm = TryReadDouble(vm,
                "OffsetLeftMm", "LeftOffsetMm", "PlacementOffsetLeftMm", "SheetOffsetLeftMm", "PlaceOffsetLeftMm");

            result.TopMm = TryReadDouble(vm,
                "OffsetTopMm", "TopOffsetMm", "PlacementOffsetTopMm", "SheetOffsetTopMm", "PlaceOffsetTopMm");

            // Optional (if you have them)
            result.RightMm = TryReadDouble(vm,
                "OffsetRightMm", "RightOffsetMm", "PlacementOffsetRightMm", "SheetOffsetRightMm", "PlaceOffsetRightMm");

            result.BottomMm = TryReadDouble(vm,
                "OffsetBottomMm", "BottomOffsetMm", "PlacementOffsetBottomMm", "SheetOffsetBottomMm", "PlaceOffsetBottomMm");

            return result;
        }

        private static double TryReadDouble(object obj, params string[] propertyNames)
        {
            Type t = obj.GetType();

            foreach (string name in propertyNames)
            {
                PropertyInfo pi = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                if (pi == null) continue;

                object val = pi.GetValue(obj);
                if (val == null) return 0;

                // Handles double, int, string, etc.
                if (val is double d) return d;
                if (val is int i) return i;

                if (double.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
                    return parsed;

                // If it exists but isn't parseable, treat as 0 to avoid crashing placement.
                return 0;
            }

            // Property not found -> 0
            return 0;
        }

        private struct OffsetsMm
        {
            public double LeftMm;
            public double TopMm;
            public double RightMm;
            public double BottomMm;

            public OffsetsMm(double leftMm, double topMm, double rightMm, double bottomMm)
            {
                LeftMm = leftMm;
                TopMm = topMm;
                RightMm = rightMm;
                BottomMm = bottomMm;
            }
        }
    }
}
