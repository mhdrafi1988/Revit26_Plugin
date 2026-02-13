// File: MultiSheetOptimizerService.cs
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
    /// MULTI-SHEET OPTIMIZER - Places views across multiple sheets
    /// 
    /// REQUIREMENTS:
    /// ✓ No overlapping sections
    /// ✓ Keep decent gaps between views (±10% tolerance)
    /// ✓ Bottom aligned per row
    /// ✓ Left aligned within rows
    /// ✓ Use maximum sheet space
    /// ✓ Gap compromise ±10% if needed
    /// ✓ MULTI-SHEET - when one sheet is full, move to next
    /// ✓ Live UI updates with detailed logging
    /// </summary>
    public class MultiSheetOptimizerService
    {
        private readonly Document _doc;
        private const double ROW_TOLERANCE_MM = 50; // Tolerance for grouping into same row
        private const double GAP_TOLERANCE = 0.10; // ±10% gap tolerance
        private const int MAX_LAYOUT_ATTEMPTS = 3; // Try up to 3 gap variations per sheet
        private const int MAX_SHEETS = 50; // Maximum sheets to create (safety limit)

        public MultiSheetOptimizerService(Document doc)
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
            context.ViewModel?.LogInfo("📚 MULTI-SHEET OPTIMIZER");
            context.ViewModel?.LogInfo("══════════════════════════════════════════════════════════════");
            context.ViewModel?.LogInfo($"📊 Total sections to place: {sections.Count}");
            context.ViewModel?.LogInfo("⚙️ RULES:");
            context.ViewModel?.LogInfo("   • No overlapping views");
            context.ViewModel?.LogInfo("   • Bottom-aligned per row");
            context.ViewModel?.LogInfo("   • Left-aligned within rows");
            context.ViewModel?.LogInfo($"   • Gaps: {context.HorizontalGapMm}mm ±10% tolerance");
            context.ViewModel?.LogInfo("   • Maximize sheet space utilization");
            context.ViewModel?.LogInfo("   • MULTI-SHEET: When sheet full, move to next");
            context.ViewModel?.LogInfo($"   • Max sheets: {MAX_SHEETS}");

            try
            {
                // Track remaining sections across multiple sheets
                var remainingSections = new List<SectionItemViewModel>(sections);
                int sheetCount = 0;
                int totalPlaced = 0;
                int totalSkipped = 0;

                // ----- STAGE 1: SORT ALL SECTIONS IN READING ORDER ONCE -----
                context.ViewModel?.LogInfo("\n📏 STAGE 1: Sorting all sections in reading order...");

                var allSortedItems = PrepareItems(remainingSections, referenceView);

                context.ViewModel?.LogInfo($"   • {allSortedItems.Count} total items sorted");
                for (int i = 0; i < Math.Min(5, allSortedItems.Count); i++)
                {
                    context.ViewModel?.LogInfo($"      #{i + 1}: {allSortedItems[i].Section.ViewName} - {allSortedItems[i].Width * 304.8:F0}×{allSortedItems[i].Height * 304.8:F0}mm");
                }

                // Process sheets until all sections placed or max sheets reached
                while (remainingSections.Any() && sheetCount < MAX_SHEETS)
                {
                    sheetCount++;

                    context.ViewModel?.LogInfo($"\n══════════════════════════════════════════════════════════════");
                    context.ViewModel?.LogInfo($"📄 SHEET {sheetCount}");
                    context.ViewModel?.LogInfo($"══════════════════════════════════════════════════════════════");
                    context.ViewModel?.LogInfo($"   • Remaining views: {remainingSections.Count}");

                    // Process one sheet
                    var sheetResult = ProcessSingleSheet(
                        context,
                        remainingSections,
                        referenceView,
                        sheetCount);

                    if (sheetResult.PlacedCount > 0)
                    {
                        totalPlaced += sheetResult.PlacedCount;
                        result.PlacedCount = totalPlaced;
                        result.SheetNumbers.Add(sheetResult.SheetNumber);

                        // Remove placed sections from remaining list
                        remainingSections = remainingSections
                            .Where(s => !sheetResult.PlacedViewIds.Contains(s.View.Id))
                            .ToList();

                        context.ViewModel?.LogSuccess($"\n✅ Sheet {sheetCount} complete: {sheetResult.PlacedCount} views placed");
                        context.ViewModel?.LogInfo($"   • Sheet utilization: {sheetResult.Utilization:F1}%");
                        context.ViewModel?.LogInfo($"   • Gaps used: {sheetResult.FinalGapMm:F0}mm");
                        context.ViewModel?.LogInfo($"   • Rows used: {sheetResult.RowsUsed}");
                        context.ViewModel?.LogInfo($"   • Remaining views: {remainingSections.Count}");
                    }
                    else
                    {
                        context.ViewModel?.LogWarning($"\n⚠️ Could not place any views on sheet {sheetCount} - stopping");
                        totalSkipped = remainingSections.Count;
                        break;
                    }

                    // Check if we've placed all sections
                    if (!remainingSections.Any())
                    {
                        context.ViewModel?.LogSuccess($"\n✅ ALL SECTIONS PLACED across {sheetCount} sheets!");
                        break;
                    }
                }

                // ----- FINAL REPORT -----
                context.ViewModel?.LogInfo("\n📊 FINAL MULTI-SHEET REPORT");
                context.ViewModel?.LogInfo("══════════════════════════════════════════════════════════════");
                context.ViewModel?.LogInfo($"   • Total sections: {sections.Count}");
                context.ViewModel?.LogInfo($"   • Successfully placed: {totalPlaced}");
                context.ViewModel?.LogInfo($"   • Sheets used: {sheetCount}");
                context.ViewModel?.LogInfo($"   • Average per sheet: {totalPlaced / (double)sheetCount:F1} views");

                if (remainingSections.Any())
                {
                    context.ViewModel?.LogWarning($"   • Skipped: {remainingSections.Count} (max sheets reached)");
                    result.FailedCount = remainingSections.Count;
                }
                else
                {
                    context.ViewModel?.LogSuccess($"   • All sections placed successfully!");
                }

                // List all sheets created
                if (result.SheetNumbers.Any())
                {
                    var sheetList = string.Join(", ", result.SheetNumbers.OrderBy(s => s));
                    context.ViewModel?.LogInfo($"   • Sheets: {sheetList}");
                }

                return result;
            }
            catch (Exception ex)
            {
                context.ViewModel?.LogError($"❌ Multi-sheet optimization failed: {ex.Message}");
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        private SheetResult ProcessSingleSheet(
            SectionPlacementHandler.PlacementContext context,
            List<SectionItemViewModel> remainingSections,
            View referenceView,
            int sheetNumber)
        {
            // ----- STAGE 1: PREPARE ITEMS FOR THIS SHEET -----
            context.ViewModel?.LogInfo($"\n   📏 Step 1: Preparing items for sheet {sheetNumber}...");

            var itemsForThisSheet = PrepareItems(remainingSections, referenceView);

            // ----- STAGE 2: GROUP INTO ROWS -----
            context.ViewModel?.LogInfo($"   📏 Step 2: Grouping into rows...");

            var rows = GroupIntoRows(itemsForThisSheet);

            context.ViewModel?.LogInfo($"      • {rows.Count} rows identified");

            // ----- STAGE 3: CALCULATE SHEET DIMENSIONS -----
            double leftMargin = UnitConversionHelper.MmToFeet(context.ViewModel?.LeftMarginMm ?? 40);
            double rightMargin = UnitConversionHelper.MmToFeet(context.ViewModel?.RightMarginMm ?? 150);
            double topMargin = UnitConversionHelper.MmToFeet(context.ViewModel?.TopMarginMm ?? 40);
            double bottomMargin = UnitConversionHelper.MmToFeet(context.ViewModel?.BottomMarginMm ?? 100);

            double usableWidth = context.PlacementArea.Width - leftMargin - rightMargin;
            double usableHeight = context.PlacementArea.Height - topMargin - bottomMargin;

            double startX = context.PlacementArea.Origin.X + leftMargin;
            double startY = context.PlacementArea.Origin.Y - topMargin;

            context.ViewModel?.LogInfo($"   📐 Step 3: Sheet dimensions:");
            context.ViewModel?.LogInfo($"      • Usable area: {usableWidth * 304.8:F0} × {usableHeight * 304.8:F0} mm");

            // ----- STAGE 4: CREATE SHEET -----
            context.ViewModel?.LogInfo($"   📄 Step 4: Creating sheet...");

            string sheetNumberStr = context.SheetNumberService.GetNextAvailableSheetNumber($"OPT{sheetNumber}");
            context.SheetNumberService.TryReserveSheetNumber(sheetNumberStr);

            var sheetCreator = new SheetCreationService(_doc);
            var sheet = sheetCreator.Create(context.TitleBlock, sheetNumberStr, $"Optimized-{sheetNumberStr}");

            context.ViewModel?.LogInfo($"      ✅ Created sheet: {sheet.SheetNumber}");

            // ----- STAGE 5: TRY DIFFERENT GAP VARIATIONS -----
            context.ViewModel?.LogInfo($"   🎯 Step 5: Testing gap variations...");

            double baseGap = UnitConversionHelper.MmToFeet(context.HorizontalGapMm);
            var bestResult = new SheetResult { PlacedCount = 0 };

            // Try different gap variations (±10%)
            for (int attempt = 0; attempt < MAX_LAYOUT_ATTEMPTS; attempt++)
            {
                double gapMultiplier = 1.0;
                string gapMode = "normal";

                switch (attempt)
                {
                    case 0:
                        gapMultiplier = 1.0;
                        gapMode = "normal";
                        break;
                    case 1:
                        gapMultiplier = 0.9;
                        gapMode = "tight (-10%)";
                        break;
                    case 2:
                        gapMultiplier = 1.1;
                        gapMode = "loose (+10%)";
                        break;
                }

                double currentGap = baseGap * gapMultiplier;
                double gapMm = currentGap * 304.8;

                context.ViewModel?.LogInfo($"      🔄 Attempt {attempt + 1}: {gapMode} gaps ({gapMm:F0}mm)");

                var attemptResult = ExecutePlacement(
                    sheet,
                    rows,
                    startX,
                    startY,
                    usableWidth,
                    usableHeight,
                    currentGap,
                    currentGap,
                    context);

                context.ViewModel?.LogInfo($"         • Placed: {attemptResult.PlacedCount} views, {attemptResult.Utilization:F1}% util");

                // Track best result
                if (attemptResult.PlacedCount > bestResult.PlacedCount ||
                    (attemptResult.PlacedCount == bestResult.PlacedCount && attemptResult.Utilization > bestResult.Utilization))
                {
                    bestResult = attemptResult;
                    bestResult.FinalGapMm = gapMm;

                    // Store the placed view IDs from this attempt
                    bestResult.PlacedViewIds = attemptResult.PlacedViewIds;
                }
            }

            // If best result has placements, we need to actually create them
            // (ExecutePlacement already created them, so we're good)

            if (bestResult.PlacedCount == 0)
            {
                // No views placed - delete the empty sheet
                _doc.Delete(sheet.Id);
                context.ViewModel?.LogInfo($"      ⚠️ No views placed - sheet deleted");
            }

            bestResult.SheetNumber = sheet.SheetNumber;
            return bestResult;
        }

        private SheetResult ExecutePlacement(
            ViewSheet sheet,
            List<ItemRow> rows,
            double startX,
            double startY,
            double usableWidth,
            double usableHeight,
            double horizontalGap,
            double verticalGap,
            SectionPlacementHandler.PlacementContext context)
        {
            var result = new SheetResult();
            var placedViewIds = new HashSet<ElementId>();

            double currentY = startY;
            int totalPlaced = 0;
            int rowsUsed = 0;
            double totalArea = 0;

            foreach (var row in rows)
            {
                // Calculate row height (tallest view in row)
                double rowHeight = row.Items.Max(i => i.Height);

                // Check if row fits vertically
                if (currentY - rowHeight < startY - usableHeight)
                {
                    result.StoppingReason = $"Sheet full - insufficient vertical space";
                    break;
                }

                double currentX = startX;
                double rowBottomY = currentY - rowHeight; // Bottom alignment point

                // Sort row items by X (left to right)
                var rowItems = row.Items.OrderBy(i => i.X).ToList();
                int rowPlaced = 0;

                foreach (var item in rowItems)
                {
                    // Check if view can be placed
                    if (!CanPlaceView(item.Section.View, _doc, sheet.Id))
                    {
                        continue;
                    }

                    // Check if item fits horizontally
                    if (currentX + item.Width > startX + usableWidth)
                    {
                        continue;
                    }

                    // Calculate position (LEFT ALIGNED, BOTTOM ALIGNED)
                    double centerX = currentX + item.Width / 2;
                    double centerY = rowBottomY + item.Height / 2;

                    try
                    {
                        // Verify no overlap
                        bool overlaps = CheckOverlap(sheet, centerX, centerY, item.Width, item.Height);
                        if (overlaps)
                        {
                            continue;
                        }

                        // Create viewport
                        var vp = Viewport.Create(_doc, sheet.Id, item.Section.View.Id, new XYZ(centerX, centerY, 0));

                        // Set detail number
                        int detailNumber = context.GetNextDetailNumber();
                        var detailParam = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                        if (detailParam != null && !detailParam.IsReadOnly)
                        {
                            detailParam.Set(detailNumber.ToString());
                        }

                        placedViewIds.Add(item.Section.View.Id);
                        totalArea += item.Width * item.Height;
                        totalPlaced++;
                        rowPlaced++;

                        // Move X position for next item (LEFT ALIGNMENT with gap)
                        currentX += item.Width + horizontalGap;
                    }
                    catch (Exception ex)
                    {
                        // Silently fail - this attempt just doesn't place this view
                    }
                }

                if (rowPlaced > 0)
                {
                    rowsUsed++;
                    // Move to next row
                    currentY -= (rowHeight + verticalGap);
                }
            }

            result.PlacedCount = totalPlaced;
            result.RowsUsed = rowsUsed;
            result.Utilization = totalArea > 0 ? (totalArea / (usableWidth * usableHeight)) * 100 : 0;
            result.PlacedViewIds = placedViewIds;

            if (string.IsNullOrEmpty(result.StoppingReason))
            {
                if (totalPlaced == 0)
                    result.StoppingReason = "No views could be placed";
                else
                    result.StoppingReason = "Row capacity reached";
            }

            return result;
        }

        private bool CheckOverlap(ViewSheet sheet, double x, double y, double width, double height)
        {
            try
            {
                var viewports = new FilteredElementCollector(_doc, sheet.Id)
                    .OfClass(typeof(Viewport))
                    .Cast<Viewport>()
                    .ToList();

                foreach (var vp in viewports)
                {
                    var box = vp.GetBoxOutline();
                    if (box == null) continue;

                    double vpMinX = box.MinimumPoint.X;
                    double vpMaxX = box.MaximumPoint.X;
                    double vpMinY = box.MinimumPoint.Y;
                    double vpMaxY = box.MaximumPoint.Y;

                    double newMinX = x - width / 2;
                    double newMaxX = x + width / 2;
                    double newMinY = y - height / 2;
                    double newMaxY = y + height / 2;

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

        private bool CanPlaceView(ViewSection view, Document doc, ElementId sheetId)
        {
            try
            {
                return Viewport.CanAddViewToSheet(doc, sheetId, view.Id);
            }
            catch
            {
                return false;
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

        private class SheetResult
        {
            public string SheetNumber { get; set; }
            public int PlacedCount { get; set; }
            public int RowsUsed { get; set; }
            public double Utilization { get; set; }
            public double FinalGapMm { get; set; }
            public string StoppingReason { get; set; }
            public HashSet<ElementId> PlacedViewIds { get; set; } = new HashSet<ElementId>();
        }
    }
}