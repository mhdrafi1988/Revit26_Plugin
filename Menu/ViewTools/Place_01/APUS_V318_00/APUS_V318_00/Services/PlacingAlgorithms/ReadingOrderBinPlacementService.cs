// File: ReadingOrderBinPlacementService.cs
using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V318.ExternalEvents;
using Revit26_Plugin.APUS_V318.Helpers;
using Revit26_Plugin.APUS_V318.Models;
using Revit26_Plugin.APUS_V318.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V318.Services
{
    /// <summary>
    /// SHELF LAYOUT ALGORITHM:
    /// Step 1: Sort by reading order (Top→Bottom, Left→Right)
    /// Step 2: Group by Y-coordinate with tolerance
    /// Step 3: Apply SHELF layout - each row is a shelf
    /// Step 4: Bottom align per row, LEFT ALIGN within row
    /// Step 5: Maintain consistent horizontal & vertical gaps
    /// </summary>
    public class ReadingOrderBinPlacementService
    {
        private readonly Document _doc;
        private const double BUFFER_PERCENTAGE = 0.25;
        private const double GAP_TOLERANCE_MM = 10;
        private const double ROW_TOLERANCE_MM = 50; // Tolerance for grouping views into same row (mm)

        public ReadingOrderBinPlacementService(Document doc)
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

            context.ViewModel?.LogInfo("══════════════════════════════════════════════════════════════");
            context.ViewModel?.LogInfo("📚 SHELF LAYOUT ALGORITHM");
            context.ViewModel?.LogInfo("══════════════════════════════════════════════════════════════");
            context.ViewModel?.LogInfo("   • Step 1: Sort by reading order (Top→Bottom, Left→Right)");
            context.ViewModel?.LogInfo("   • Step 2: Group by Y-coordinate with tolerance");
            context.ViewModel?.LogInfo("   • Step 3: Apply SHELF layout - each row is a shelf");
            context.ViewModel?.LogInfo("   • Step 4: Bottom align per row, LEFT ALIGN within row");
            context.ViewModel?.LogInfo("   • Step 5: Maintain consistent gaps");

            try
            {
                // ----- STAGE 1: SORT IN READING ORDER -----
                context.ViewModel?.LogInfo("\n📏 STEP 1: Sorting sections in reading order...");

                var shelfItems = PrepareShelfItems(sections, referenceView, context);

                context.ViewModel?.LogInfo($"\n📋 INITIAL READING ORDER ({shelfItems.Count} views):");
                for (int i = 0; i < Math.Min(10, shelfItems.Count); i++)
                {
                    var item = shelfItems[i];
                    context.ViewModel?.LogInfo($"   #{i + 1,2}: {item.Section.ViewName,-30} - Y:{item.Y,6:F2}, X:{item.X,6:F2} ft");
                }

                // ----- STAGE 2: GROUP BY ROWS (Y-COORDINATE WITH TOLERANCE) -----
                context.ViewModel?.LogInfo("\n📏 STEP 2: Grouping by Y-coordinate with tolerance...");

                var rows = GroupIntoRows(shelfItems, context);

                context.ViewModel?.LogInfo($"\n📋 ROW GROUPS ({rows.Count} rows):");
                for (int i = 0; i < rows.Count; i++)
                {
                    context.ViewModel?.LogInfo($"   Row {i + 1}: {rows[i].Count} views, Y≈{rows[i].AverageY:F2} ft");
                }

                // ----- STAGE 3: CALCULATE SHEET DIMENSIONS -----
                double leftMargin = UnitConversionHelper.MmToFeet(context.ViewModel?.LeftMarginMm ?? 40);
                double rightMargin = UnitConversionHelper.MmToFeet(context.ViewModel?.RightMarginMm ?? 150);
                double topMargin = UnitConversionHelper.MmToFeet(context.ViewModel?.TopMarginMm ?? 40);
                double bottomMargin = UnitConversionHelper.MmToFeet(context.ViewModel?.BottomMarginMm ?? 100);

                double usableWidth = context.PlacementArea.Width - leftMargin - rightMargin;
                double usableHeight = context.PlacementArea.Height - topMargin - bottomMargin;

                double startX = context.PlacementArea.Origin.X + leftMargin;
                double startY = context.PlacementArea.Origin.Y - topMargin;

                // ----- STAGE 4: CREATE SHEET -----
                context.ViewModel?.LogInfo("\n📄 Creating sheet...");

                string sheetNumber = context.SheetNumberService.GetNextAvailableSheetNumber("SHF");
                context.SheetNumberService.TryReserveSheetNumber(sheetNumber);

                var sheetCreator = new SheetCreationService(_doc);
                var sheet = sheetCreator.Create(context.TitleBlock, sheetNumber, $"Shelf-{sheetNumber}");

                context.ViewModel?.LogInfo($"✅ Created sheet: {sheet.SheetNumber}");
                context.ViewModel?.LogInfo($"   • Usable area: {usableWidth:F2} × {usableHeight:F2} ft");

                // ----- STAGE 5: SHELF LAYOUT PLACEMENT -----
                context.ViewModel?.LogInfo("\n📦 STEP 3-5: Applying SHELF layout with gaps...");

                double horizontalGap = UnitConversionHelper.MmToFeet(context.HorizontalGapMm);
                double verticalGap = UnitConversionHelper.MmToFeet(context.VerticalGapMm);

                context.ViewModel?.LogInfo($"   • Gaps: H={horizontalGap * 304.8:F0}mm, V={verticalGap * 304.8:F0}mm");
                context.ViewModel?.LogInfo($"   • Alignment: Bottom-align per row, LEFT-ALIGN within row");

                var placementResult = PlaceShelfLayout(
                    sheet,
                    rows,
                    startX,
                    startY,
                    usableWidth,
                    usableHeight,
                    horizontalGap,
                    verticalGap,
                    context);

                // ----- STAGE 6: REPORT RESULTS -----
                context.ViewModel?.LogInfo("\n📋 PLACEMENT REPORT:");
                context.ViewModel?.LogInfo("══════════════════════════════════════════════════════════");
                context.ViewModel?.LogInfo($"   • Views placed: {placementResult.PlacedCount}");
                context.ViewModel?.LogInfo($"   • Views failed: {placementResult.FailedCount}");
                context.ViewModel?.LogInfo($"   • Rows used: {placementResult.RowsUsed}");
                context.ViewModel?.LogInfo($"   • Sheet utilization: {placementResult.Utilization:F1}%");
                context.ViewModel?.LogInfo($"   • 🛑 Stopping reason: {placementResult.StoppingReason}");

                if (placementResult.PlacedCount > 0)
                {
                    result.PlacedCount = placementResult.PlacedCount;
                    result.FailedCount = placementResult.FailedCount;
                    result.SheetNumbers.Add(sheet.SheetNumber);

                    context.ViewModel?.LogSuccess($"\n✅ SUCCESS: {placementResult.PlacedCount} views placed on {sheet.SheetNumber}");
                }
                else
                {
                    _doc.Delete(sheet.Id);
                    context.ViewModel?.LogWarning($"\n⚠️ No views could be placed");
                    result.ErrorMessage = "No views could be placed on sheet";
                }

                return result;
            }
            catch (Exception ex)
            {
                context.ViewModel?.LogError($"❌ Shelf layout failed: {ex.Message}");
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// STEP 1: Prepare items with coordinates in reference view space
        /// </summary>
        private List<ShelfItem> PrepareShelfItems(
            List<SectionItemViewModel> sections,
            View referenceView,
            SectionPlacementHandler.PlacementContext context)
        {
            var items = new List<ShelfItem>();
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

                items.Add(new ShelfItem
                {
                    Section = section,
                    X = x,
                    Y = y,
                    Width = footprint.WidthFt,
                    Height = footprint.HeightFt,
                    OriginalIndex = items.Count
                });
            }

            // Sort: Top to Bottom (descending Y), then Left to Right (ascending X)
            var sorted = items
                .OrderByDescending(item => item.Y)
                .ThenBy(item => item.X)
                .ToList();

            // Store sort positions
            for (int i = 0; i < sorted.Count; i++)
            {
                sorted[i].SortIndex = i + 1;
            }

            return sorted;
        }

        /// <summary>
        /// STEP 2: Group items into rows based on Y-coordinate with tolerance
        /// </summary>
        private List<ShelfRow> GroupIntoRows(
            List<ShelfItem> items,
            SectionPlacementHandler.PlacementContext context)
        {
            var rows = new List<ShelfRow>();
            double toleranceFt = UnitConversionHelper.MmToFeet(ROW_TOLERANCE_MM);

            ShelfRow currentRow = null;

            foreach (var item in items)
            {
                if (currentRow == null)
                {
                    // Start first row
                    currentRow = new ShelfRow { AverageY = item.Y };
                    currentRow.Items.Add(item);
                }
                else
                {
                    // Check if this item belongs in current row (within Y tolerance)
                    if (Math.Abs(item.Y - currentRow.AverageY) <= toleranceFt)
                    {
                        currentRow.Items.Add(item);
                        // Update average Y
                        currentRow.AverageY = currentRow.Items.Average(i => i.Y);
                    }
                    else
                    {
                        // Start new row
                        rows.Add(currentRow);
                        currentRow = new ShelfRow { AverageY = item.Y };
                        currentRow.Items.Add(item);
                    }
                }
            }

            if (currentRow != null)
            {
                rows.Add(currentRow);
            }

            return rows;
        }

        /// <summary>
        /// STEPS 3-5: Place views using SHELF LAYOUT
        /// - Each row is a shelf
        /// - Bottom-aligned within row
        /// - LEFT-ALIGNED within row
        /// - Consistent gaps
        /// </summary>
        private ShelfPlacementResult PlaceShelfLayout(
            ViewSheet sheet,
            List<ShelfRow> rows,
            double startX,
            double startY,
            double usableWidth,
            double usableHeight,
            double horizontalGap,
            double verticalGap,
            SectionPlacementHandler.PlacementContext context)
        {
            var result = new ShelfPlacementResult();

            double currentY = startY;
            int totalPlaced = 0;
            int totalFailed = 0;
            int rowsUsed = 0;

            context.ViewModel?.LogInfo($"\n   📊 SHELF LAYOUT PLACEMENT:");

            foreach (var row in rows)
            {
                if (context.ViewModel?.Progress.IsCancelled == true)
                {
                    result.StoppingReason = "Operation cancelled by user";
                    break;
                }

                // Calculate row height (tallest view in row)
                double rowHeight = row.Items.Max(i => i.Height);

                // Check if row fits vertically
                if (currentY - rowHeight < startY - usableHeight)
                {
                    context.ViewModel?.LogInfo($"   ⚠️ Row {rowsUsed + 1} doesn't fit vertically - stopping");
                    result.StoppingReason = "Sheet full - insufficient vertical space";
                    break;
                }

                // Place row with LEFT ALIGNMENT
                double currentX = startX;
                double rowBottomY = currentY - rowHeight; // Bottom alignment point

                context.ViewModel?.LogInfo($"\n   Row {rowsUsed + 1}: {row.Items.Count} views, height = {rowHeight:F2} ft");

                // Sort row items by X (left to right) for consistent ordering
                var rowItems = row.Items.OrderBy(i => i.X).ToList();
                int rowPlaced = 0;

                foreach (var item in rowItems)
                {
                    if (!CanPlaceView(item.Section.View, _doc, sheet.Id))
                    {
                        context.ViewModel?.LogInfo($"      ⚠️ [{item.SortIndex}] {item.Section.ViewName} - Cannot place (already placed elsewhere)");
                        totalFailed++;
                        continue;
                    }

                    // Check if item fits horizontally
                    if (currentX + item.Width > startX + usableWidth)
                    {
                        context.ViewModel?.LogInfo($"      ⚠️ [{item.SortIndex}] {item.Section.ViewName} - No horizontal space in row");
                        totalFailed++;
                        continue;
                    }

                    // Calculate position
                    double centerX = currentX + item.Width / 2;
                    double centerY = rowBottomY + item.Height / 2; // Bottom-aligned

                    try
                    {
                        // Create viewport
                        var vp = Viewport.Create(_doc, sheet.Id, item.Section.View.Id, new XYZ(centerX, centerY, 0));

                        // Set detail number
                        int detailNumber = context.GetNextDetailNumber();
                        var detailParam = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                        if (detailParam != null && !detailParam.IsReadOnly)
                        {
                            detailParam.Set(detailNumber.ToString());
                        }

                        context.ViewModel?.LogInfo($"      ✅ [{item.SortIndex}] {item.Section.ViewName,-30} - X:{currentX - startX:F2}, Detail:{detailNumber}");

                        context.ViewModel?.Progress.Step();
                        totalPlaced++;
                        rowPlaced++;

                        // Move X position for next item (LEFT ALIGNMENT with gap)
                        currentX += item.Width + horizontalGap;
                    }
                    catch (Exception ex)
                    {
                        context.ViewModel?.LogError($"      ❌ [{item.SortIndex}] {item.Section.ViewName} - Failed: {ex.Message}");
                        totalFailed++;
                    }
                }

                if (rowPlaced > 0)
                {
                    rowsUsed++;
                    // Move to next row
                    currentY -= (rowHeight + verticalGap);

                    context.ViewModel?.LogInfo($"   📊 Row {rowsUsed} complete: {rowPlaced} placed, next Y = {currentY - startY:F2} ft from top");
                }
                else
                {
                    context.ViewModel?.LogInfo($"   ⚠️ No views placed in this row - stopping");
                    result.StoppingReason = "No views could be placed in current row";
                    break;
                }
            }

            // Calculate utilization
            double utilization = CalculateSheetUtilization(sheet, context);

            result.PlacedCount = totalPlaced;
            result.FailedCount = totalFailed;
            result.RowsUsed = rowsUsed;
            result.Utilization = utilization;

            if (string.IsNullOrEmpty(result.StoppingReason))
            {
                if (totalPlaced == 0)
                    result.StoppingReason = "No views could be placed";
                else if (rowsUsed < rows.Count)
                    result.StoppingReason = "Sheet full - remaining rows don't fit";
                else
                    result.StoppingReason = "All rows placed successfully";
            }

            return result;
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
                if (sheetId == null)
                    return true;

                return Viewport.CanAddViewToSheet(doc, sheetId, view.Id);
            }
            catch
            {
                return false;
            }
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

                double sheetArea = context.PlacementArea.Width * context.PlacementArea.Height;
                return sheetArea > 0 ? (totalViewArea * 100.0) / sheetArea : 0;
            }
            catch
            {
                return 0;
            }
        }

        // Helper classes
        private class ShelfItem
        {
            public SectionItemViewModel Section { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public int SortIndex { get; set; }
            public int OriginalIndex { get; set; }
        }

        private class ShelfRow
        {
            public List<ShelfItem> Items { get; } = new List<ShelfItem>();
            public double AverageY { get; set; }
            public int Count => Items.Count;
        }

        private class ShelfPlacementResult
        {
            public int PlacedCount { get; set; }
            public int FailedCount { get; set; }
            public int RowsUsed { get; set; }
            public double Utilization { get; set; }
            public string StoppingReason { get; set; }
        }
    }
}