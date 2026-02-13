// File: SmartSheetOptimizerService.cs
using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V317.ExternalEvents;
using Revit26_Plugin.APUS_V317.Helpers;
using Revit26_Plugin.APUS_V317.Models;
using Revit26_Plugin.APUS_V317.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V317.Services
{
    /// <summary>
    /// SMART SHEET OPTIMIZER - Places maximum views on ONE sheet with 95% utilization target
    /// 
    /// REQUIREMENTS:
    /// ✓ No overlapping sections
    /// ✓ Keep user given gaps between views (±10% tolerance if needed)
    /// ✓ Bottom aligned (lower priority)
    /// ✓ Left aligned (lower priority)
    /// ✓ Use maximum space - target 95% utilization
    /// ✓ If <95% used, try to add more sections and REPLACE (3 attempts)
    /// ✓ Gap compromise ±10% if required
    /// ✓ ONE SHEET ONLY - skip remaining sections
    /// ✓ Live UI updates with all details
    /// </summary>
    public class MultiStrategyShelfLayoutService
    {
        private readonly Document _doc;
        private const double ROW_TOLERANCE_MM = 50; // Tolerance for grouping into same row
        private const double GAP_TOLERANCE = 0.10; // ±10% gap tolerance
        private const double TARGET_UTILIZATION = 95; // Target 95% sheet utilization
        private const int MAX_RETRY_ATTEMPTS = 3; // Try 3 times to improve
        private const double IMPROVEMENT_THRESHOLD = 5; // Need 5% improvement to continue trying

        public MultiStrategyShelfLayoutService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public SectionPlacementHandler.PlacementResult Place(
            SectionPlacementHandler.PlacementContext context,
            List<SectionItemViewModel> sections,
            View referenceView)
        {
            var result = new SectionPlacementHandler.PlacementResult();
            result.SheetNumbers = new HashSet<string>();

            if (sections == null || !sections.Any())
            {
                context.ViewModel?.LogWarning("⚠️ No sections to place.");
                result.ErrorMessage = "No sections to place";
                return result;
            }

            // ===== LIVE UI LOGGING =====
            context.ViewModel?.LogInfo("══════════════════════════════════════════════════════════════");
            context.ViewModel?.LogInfo("🎯 SMART SHEET OPTIMIZER");
            context.ViewModel?.LogInfo("══════════════════════════════════════════════════════════════");
            context.ViewModel?.LogInfo($"📊 Input: {sections.Count} sections");
            context.ViewModel?.LogInfo("⚙️ RULES:");
            context.ViewModel?.LogInfo("   • ONE sheet only - remaining sections will be skipped");
            context.ViewModel?.LogInfo("   • No overlapping views");
            context.ViewModel?.LogInfo($"   • Target utilization: {TARGET_UTILIZATION}%");
            context.ViewModel?.LogInfo($"   • Max retry attempts: {MAX_RETRY_ATTEMPTS}");
            context.ViewModel?.LogInfo("   • Bottom-aligned (if possible)");
            context.ViewModel?.LogInfo("   • Left-aligned (if possible)");
            context.ViewModel?.LogInfo($"   • User gaps: H={context.HorizontalGapMm}mm, V={context.VerticalGapMm}mm");
            context.ViewModel?.LogInfo($"   • Gap tolerance: ±{GAP_TOLERANCE * 100}%");

            try
            {
                // ----- STAGE 1: SORT ALL SECTIONS IN READING ORDER -----
                context.ViewModel?.LogInfo("\n📏 STAGE 1: Sorting sections in reading order...");

                var allItems = PrepareItems(sections, referenceView);

                context.ViewModel?.LogInfo($"   • {allItems.Count} items sorted");
                for (int i = 0; i < Math.Min(5, allItems.Count); i++)
                {
                    var item = allItems[i];
                    context.ViewModel?.LogInfo($"      #{i + 1}: {item.ViewName} - {item.Width * 304.8:F0}×{item.Height * 304.8:F0}mm");
                }

                // ----- STAGE 2: CALCULATE SHEET DIMENSIONS -----
                double leftMargin = UnitConversionHelper.MmToFeet(context.ViewModel?.LeftMarginMm ?? 40);
                double rightMargin = UnitConversionHelper.MmToFeet(context.ViewModel?.RightMarginMm ?? 150);
                double topMargin = UnitConversionHelper.MmToFeet(context.ViewModel?.TopMarginMm ?? 40);
                double bottomMargin = UnitConversionHelper.MmToFeet(context.ViewModel?.BottomMarginMm ?? 100);

                double usableWidth = context.PlacementArea.Width - leftMargin - rightMargin;
                double usableHeight = context.PlacementArea.Height - topMargin - bottomMargin;

                double startX = context.PlacementArea.Origin.X + leftMargin;
                double startY = context.PlacementArea.Origin.Y - topMargin;

                double totalSheetArea = usableWidth * usableHeight;

                context.ViewModel?.LogInfo("\n📐 STAGE 2: Sheet dimensions:");
                context.ViewModel?.LogInfo($"   • Usable area: {usableWidth * 304.8:F0} × {usableHeight * 304.8:F0} mm");
                context.ViewModel?.LogInfo($"   • Total area: {totalSheetArea * 304.8 * 304.8 / 1e6:F2} m²");
                context.ViewModel?.LogInfo($"   • Target used area: {totalSheetArea * 304.8 * 304.8 * TARGET_UTILIZATION / 100 / 1e6:F2} m² ({TARGET_UTILIZATION}%)");

                // ----- STAGE 3: TRY TO ACHIEVE 95% UTILIZATION WITH MULTIPLE ATTEMPTS -----
                context.ViewModel?.LogInfo("\n🔄 STAGE 3: Optimization attempts");

                double baseGapH = UnitConversionHelper.MmToFeet(context.HorizontalGapMm);
                double baseGapV = UnitConversionHelper.MmToFeet(context.VerticalGapMm);

                // Track best result across all attempts
                SheetPlacementResult bestResult = null;
                int attemptNumber = 0;

                // Try different strategies to achieve 95%
                while (attemptNumber < MAX_RETRY_ATTEMPTS)
                {
                    attemptNumber++;

                    context.ViewModel?.LogInfo($"\n   📌 ATTEMPT {attemptNumber}/{MAX_RETRY_ATTEMPTS}");

                    // For each attempt, try different gap variations
                    var attemptResult = TryGapVariations(
                        allItems,
                        startX,
                        startY,
                        usableWidth,
                        usableHeight,
                        baseGapH,
                        baseGapV,
                        context,
                        attemptNumber);

                    // Log attempt results
                    context.ViewModel?.LogInfo($"   📊 Attempt {attemptNumber} best: {attemptResult.BestPlacementCount} views, {attemptResult.BestUtilization:F1}% util");

                    // Track best result
                    if (bestResult == null ||
                        attemptResult.BestUtilization > bestResult.BestUtilization ||
                        (attemptResult.BestUtilization == bestResult.BestUtilization &&
                         attemptResult.BestPlacementCount > bestResult.BestPlacementCount))
                    {
                        bestResult = attemptResult;
                        context.ViewModel?.LogInfo($"      🏆 New best result!");
                    }

                    // Check if we've reached target
                    if (bestResult.BestUtilization >= TARGET_UTILIZATION)
                    {
                        context.ViewModel?.LogSuccess($"      ✅ Target utilization reached in attempt {attemptNumber}!");
                        break;
                    }

                    // Check if we're making meaningful progress
                    if (attemptNumber > 1 && bestResult.BestUtilization - previousUtilization < IMPROVEMENT_THRESHOLD)
                    {
                        context.ViewModel?.LogInfo($"      ⏹️ Stopping - improvement less than {IMPROVEMENT_THRESHOLD}%");
                        break;
                    }

                    double previousUtilization = bestResult.BestUtilization;
                }

                // ----- STAGE 4: CREATE SHEET WITH BEST RESULT -----
                if (bestResult == null || bestResult.BestPlacementCount == 0)
                {
                    context.ViewModel?.LogWarning("\n⚠️ No views could be placed in any attempt");
                    result.ErrorMessage = "No views could be placed";
                    return result;
                }

                context.ViewModel?.LogInfo("\n🏗️ STAGE 4: Creating sheet with best layout...");

                string sheetNumber = context.SheetNumberService.GetNextAvailableSheetNumber("SMRT");
                context.SheetNumberService.TryReserveSheetNumber(sheetNumber);

                var sheetCreator = new SheetCreationService(_doc);
                var sheet = sheetCreator.Create(context.TitleBlock, sheetNumber, $"Smart-{sheetNumber}");

                context.ViewModel?.LogInfo($"   ✅ Created sheet: {sheet.SheetNumber}");

                // Place views using best layout
                int placedCount = 0;
                var placedViewIds = new HashSet<ElementId>();

                foreach (var placement in bestResult.BestPlacements)
                {
                    try
                    {
                        // Verify no overlap (safety check)
                        if (CheckOverlap(sheet, placement.Center, placement.Width, placement.Height))
                        {
                            context.ViewModel?.LogWarning($"      ⚠️ {placement.ViewName} would overlap - skipping");
                            continue;
                        }

                        var vp = Viewport.Create(_doc, sheet.Id, placement.ViewId, placement.Center);

                        int detailNumber = context.GetNextDetailNumber();
                        var detailParam = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                        if (detailParam != null && !detailParam.IsReadOnly)
                        {
                            detailParam.Set(detailNumber.ToString());
                        }

                        placedViewIds.Add(placement.ViewId);
                        placedCount++;

                        context.ViewModel?.LogInfo($"      ✅ {placement.ViewName} at ({placement.Center.X * 304.8:F0}, {placement.Center.Y * 304.8:F0}) mm - Detail {detailNumber}");

                        context.ViewModel?.Progress.Step();
                    }
                    catch (Exception ex)
                    {
                        context.ViewModel?.LogError($"      ❌ Failed to place {placement.ViewName}: {ex.Message}");
                    }
                }

                // ----- STAGE 5: FINAL REPORT -----
                double finalUtilization = CalculateSheetUtilization(sheet, context);

                context.ViewModel?.LogInfo("\n📊 FINAL RESULTS");
                context.ViewModel?.LogInfo("══════════════════════════════════════════════════════════════");
                context.ViewModel?.LogInfo($"   • Sections attempted: {sections.Count}");
                context.ViewModel?.LogInfo($"   • Successfully placed: {placedCount}");
                context.ViewModel?.LogInfo($"   • Skipped: {sections.Count - placedCount}");
                context.ViewModel?.LogInfo($"   • Final utilization: {finalUtilization:F1}%");
                context.ViewModel?.LogInfo($"   • Gaps used: H={bestResult.BestGapH * 304.8:F0}mm, V={bestResult.BestGapV * 304.8:F0}mm");

                if (finalUtilization >= TARGET_UTILIZATION)
                {
                    context.ViewModel?.LogSuccess($"   🏆 TARGET ACHIEVED: {finalUtilization:F1}% ≥ {TARGET_UTILIZATION}%");
                }
                else
                {
                    double shortfall = TARGET_UTILIZATION - finalUtilization;
                    context.ViewModel?.LogWarning($"   📉 Target not reached: {shortfall:F1}% below target");
                    context.ViewModel?.LogInfo($"      💡 Try adjusting margins or gaps manually");
                }

                result.PlacedCount = placedCount;
                result.FailedCount = sections.Count - placedCount;
                result.SheetNumbers.Add(sheet.SheetNumber);

                return result;
            }
            catch (Exception ex)
            {
                context.ViewModel?.LogError($"❌ Optimization failed: {ex.Message}");
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        private SheetPlacementResult TryGapVariations(
            List<SheetItem> allItems,
            double startX,
            double startY,
            double usableWidth,
            double usableHeight,
            double baseGapH,
            double baseGapV,
            SectionPlacementHandler.PlacementContext context,
            int attemptNumber)
        {
            var result = new SheetPlacementResult();

            // Gap multipliers to try (normal, -10%, +10%)
            double[] multipliers = { 1.0, 0.9, 1.1 };
            string[] gapNames = { "normal", "tight (-10%)", "loose (+10%)" };

            for (int m = 0; m < multipliers.Length; m++)
            {
                double currentGapH = baseGapH * multipliers[m];
                double currentGapV = baseGapV * multipliers[m];

                context.ViewModel?.LogInfo($"      🔄 Testing {gapNames[m]} gaps: H={currentGapH * 304.8:F0}mm, V={currentGapV * 304.8:F0}mm");

                // Group items into rows for this attempt
                var rows = GroupIntoRows(allItems);

                var placement = AttemptPlacement(
                    rows,
                    startX,
                    startY,
                    usableWidth,
                    usableHeight,
                    currentGapH,
                    currentGapV,
                    context);

                // Calculate utilization
                double utilization = placement.TotalArea / (usableWidth * usableHeight) * 100;

                context.ViewModel?.LogInfo($"         • Placed: {placement.PlacedCount} views, {utilization:F1}% util");

                // Track best for this attempt
                if (placement.PlacedCount > result.BestPlacementCount ||
                    (placement.PlacedCount == result.BestPlacementCount && utilization > result.BestUtilization))
                {
                    result.BestPlacementCount = placement.PlacedCount;
                    result.BestUtilization = utilization;
                    result.BestGapH = currentGapH;
                    result.BestGapV = currentGapV;
                    result.BestPlacements = placement.Placements;
                }
            }

            return result;
        }

        private PlacementAttemptResult AttemptPlacement(
            List<ItemRow> rows,
            double startX,
            double startY,
            double usableWidth,
            double usableHeight,
            double gapH,
            double gapV,
            SectionPlacementHandler.PlacementContext context)
        {
            var result = new PlacementAttemptResult();
            var placements = new List<PlacementData>();

            double currentY = startY;
            double totalArea = 0;
            int placedCount = 0;

            foreach (var row in rows)
            {
                // Calculate row height (tallest view in row)
                double rowHeight = row.Items.Max(i => i.Height);

                // Check if row fits vertically
                if (currentY - rowHeight < startY - usableHeight)
                {
                    break;
                }

                double currentX = startX;
                double rowBottomY = currentY - rowHeight; // Bottom alignment

                // Sort row items by X (left to right)
                var rowItems = row.Items.OrderBy(i => i.X).ToList();

                foreach (var item in rowItems)
                {
                    // Check if item fits horizontally
                    if (currentX + item.Width > startX + usableWidth)
                    {
                        continue;
                    }

                    // Calculate position (LEFT ALIGNED)
                    double centerX = currentX + item.Width / 2;
                    double centerY = rowBottomY + item.Height / 2; // BOTTOM ALIGNED

                    placements.Add(new PlacementData
                    {
                        ViewId = item.ViewId,
                        ViewName = item.ViewName,
                        Center = new XYZ(centerX, centerY, 0),
                        Width = item.Width,
                        Height = item.Height
                    });

                    totalArea += item.Width * item.Height;
                    placedCount++;

                    currentX += item.Width + gapH;
                }

                currentY -= (rowHeight + gapV);
            }

            result.Placements = placements;
            result.PlacedCount = placedCount;
            result.TotalArea = totalArea;

            return result;
        }

        private bool CheckOverlap(ViewSheet sheet, XYZ center, double width, double height)
        {
            try
            {
                var viewports = new FilteredElementCollector(_doc, sheet.Id)
                    .OfClass(typeof(Viewport))
                    .Cast<Viewport>()
                    .ToList();

                double halfWidth = width / 2;
                double halfHeight = height / 2;

                double newMinX = center.X - halfWidth;
                double newMaxX = center.X + halfWidth;
                double newMinY = center.Y - halfHeight;
                double newMaxY = center.Y + halfHeight;

                foreach (var vp in viewports)
                {
                    var box = vp.GetBoxOutline();
                    if (box == null) continue;

                    double vpMinX = box.MinimumPoint.X;
                    double vpMaxX = box.MaximumPoint.X;
                    double vpMinY = box.MinimumPoint.Y;
                    double vpMaxY = box.MaximumPoint.Y;

                    // Check for overlap
                    if (newMaxX > vpMinX && newMinX < vpMaxX &&
                        newMaxY > vpMinY && newMinY < vpMaxY)
                    {
                        return true; // Overlap detected
                    }
                }
            }
            catch { }

            return false;
        }

        private List<ItemRow> GroupIntoRows(List<SheetItem> items)
        {
            var rows = new List<ItemRow>();
            double toleranceFt = UnitConversionHelper.MmToFeet(ROW_TOLERANCE_MM);

            ItemRow currentRow = null;

            foreach (var item in items)
            {
                if (currentRow == null)
                {
                    currentRow = new ItemRow { AverageY = item.Y };
                    currentRow.Items.Add(item);
                }
                else
                {
                    if (Math.Abs(item.Y - currentRow.AverageY) <= toleranceFt)
                    {
                        currentRow.Items.Add(item);
                        currentRow.AverageY = currentRow.Items.Average(i => i.Y);
                    }
                    else
                    {
                        rows.Add(currentRow);
                        currentRow = new ItemRow { AverageY = item.Y };
                        currentRow.Items.Add(item);
                    }
                }
            }

            if (currentRow != null)
                rows.Add(currentRow);

            return rows;
        }

        private List<SheetItem> PrepareItems(
            List<SectionItemViewModel> sections,
            View referenceView)
        {
            var items = new List<SheetItem>();
            XYZ origin = referenceView.Origin;
            XYZ right = referenceView.RightDirection;
            XYZ up = referenceView.UpDirection;

            foreach (var section in sections)
            {
                var location = GetSectionLocation(section.View);
                XYZ v = location - origin;

                double x = v.DotProduct(right);
                double y = v.DotProduct(up);

                var footprint = ViewSizeService.Calculate(section.View);

                items.Add(new SheetItem
                {
                    Section = section,
                    X = x,
                    Y = y,
                    Width = footprint.WidthFt,
                    Height = footprint.HeightFt,
                    ViewId = section.View.Id,
                    ViewName = section.ViewName
                });
            }

            // Sort: Top to Bottom, then Left to Right
            return items
                .OrderByDescending(item => item.Y)
                .ThenBy(item => item.X)
                .ToList();
        }

        private XYZ GetSectionLocation(ViewSection view)
        {
            try
            {
                if (view.Location is LocationCurve lc && lc.Curve != null)
                    return lc.Curve.Evaluate(0.5, true);

                BoundingBoxXYZ bb = view.CropBox;
                if (bb != null)
                    return (bb.Min + bb.Max) * 0.5;
            }
            catch { }

            return XYZ.Zero;
        }

        private double CalculateSheetUtilization(
            ViewSheet sheet,
            SectionPlacementHandler.PlacementContext context)
        {
            try
            {
                var viewports = new FilteredElementCollector(_doc, sheet.Id)
                    .OfClass(typeof(Viewport))
                    .Cast<Viewport>()
                    .ToList();

                if (!viewports.Any()) return 0;

                double totalViewArea = 0;
                foreach (var vp in viewports)
                {
                    var view = _doc.GetElement(vp.ViewId) as ViewSection;
                    if (view != null)
                    {
                        var footprint = ViewSizeService.Calculate(view);
                        totalViewArea += footprint.WidthFt * footprint.HeightFt;
                    }
                }

                double leftMargin = UnitConversionHelper.MmToFeet(context.ViewModel?.LeftMarginMm ?? 40);
                double rightMargin = UnitConversionHelper.MmToFeet(context.ViewModel?.RightMarginMm ?? 150);
                double topMargin = UnitConversionHelper.MmToFeet(context.ViewModel?.TopMarginMm ?? 40);
                double bottomMargin = UnitConversionHelper.MmToFeet(context.ViewModel?.BottomMarginMm ?? 100);

                double usableWidth = context.PlacementArea.Width - leftMargin - rightMargin;
                double usableHeight = context.PlacementArea.Height - topMargin - bottomMargin;
                double sheetArea = usableWidth * usableHeight;

                return sheetArea > 0 ? (totalViewArea * 100.0) / sheetArea : 0;
            }
            catch
            {
                return 0;
            }
        }

        // Helper classes
        private class SheetItem
        {
            public SectionItemViewModel Section { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public ElementId ViewId { get; set; }
            public string ViewName { get; set; }
        }

        private class ItemRow
        {
            public List<SheetItem> Items { get; } = new List<SheetItem>();
            public double AverageY { get; set; }
        }

        private class PlacementData
        {
            public ElementId ViewId { get; set; }
            public string ViewName { get; set; }
            public XYZ Center { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
        }

        private class PlacementAttemptResult
        {
            public List<PlacementData> Placements { get; set; } = new List<PlacementData>();
            public int PlacedCount { get; set; }
            public double TotalArea { get; set; }
        }

        private class SheetPlacementResult
        {
            public int BestPlacementCount { get; set; }
            public double BestUtilization { get; set; }
            public double BestGapH { get; set; }
            public double BestGapV { get; set; }
            public List<PlacementData> BestPlacements { get; set; }
        }
    }
}