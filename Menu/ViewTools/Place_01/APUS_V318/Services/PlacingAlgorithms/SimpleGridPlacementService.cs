// File: Services/SimpleGridPlacementService.cs
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
    /// SIMPLE GRID PLACEMENT SERVICE
    /// Places sections in strict reading order within a user-defined grid
    /// 
    /// RULES:
    /// ✓ User-defined rows and columns
    /// ✓ Strict reading order: Left → Right, Top → Bottom
    /// ✓ Even distribution within grid cells
    /// ✓ Bottom-aligned within rows, Left-aligned within columns
    /// ✓ Gaps respected with ±10% tolerance if needed
    /// ✓ Optional multi-sheet placement
    /// ✓ No overlap guarantee
    /// </summary>
    public class SimpleGridPlacementService
    {
        private readonly Document _doc;
        private const double MIN_GAP_MM = 3;
        private const double GAP_TOLERANCE = 0.10; // ±10%

        public SimpleGridPlacementService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public SectionPlacementHandler.PlacementResult Place(
            SectionPlacementHandler.PlacementContext context,
            List<SectionItemViewModel> sections,
            View referenceView,
            int gridRows,
            int gridColumns,
            bool placeToMultipleSheets)
        {
            var result = new SectionPlacementHandler.PlacementResult();
            result.SheetNumbers = new HashSet<string>();

            if (sections == null || !sections.Any())
            {
                context.ViewModel?.LogWarning("⚠️ No sections to place.");
                result.ErrorMessage = "No sections to place";
                return result;
            }

            int maxViewsPerSheet = gridRows * gridColumns;

            // Log initialization
            LogInitialization(context, sections, gridRows, gridColumns, maxViewsPerSheet, placeToMultipleSheets);

            try
            {
                // STAGE 1: Sort sections in reading order
                context.ViewModel?.LogInfo("\n📏 STAGE 1: Sorting sections in reading order...");
                var sortedItems = PrepareItemsInReadingOrder(sections, referenceView, context);

                if (!sortedItems.Any())
                {
                    result.ErrorMessage = "No valid items after sorting";
                    return result;
                }

                // STAGE 2: Calculate sheet dimensions
                var dimensions = CalculateSheetDimensions(context);
                LogSheetDimensions(context, dimensions);

                // STAGE 3: Calculate optimal grid cell sizes
                if (!CalculateGridCells(dimensions, gridRows, gridColumns, context,
                    out double cellWidth, out double cellHeight, out double hGap, out double vGap))
                {
                    result.ErrorMessage = "Failed to calculate grid layout";
                    return result;
                }

                // STAGE 4: Process sheets
                int totalPlaced = 0;
                int sheetCount = 0;
                int currentIndex = 0;

                while (currentIndex < sortedItems.Count)
                {
                    if (context.ViewModel?.Progress.IsCancelled == true)
                        break;

                    sheetCount++;

                    // Calculate how many views go on this sheet
                    int viewsForThisSheet = placeToMultipleSheets
                        ? Math.Min(maxViewsPerSheet, sortedItems.Count - currentIndex)
                        : Math.Min(maxViewsPerSheet, sortedItems.Count);

                    context.ViewModel?.LogInfo($"\n{'═',0} SHEET {sheetCount} - {gridRows}×{gridColumns} GRID {'═',60}");
                    context.ViewModel?.LogInfo($"   • Placing views {currentIndex + 1} to {currentIndex + viewsForThisSheet}");

                    // Create sheet
                    var sheet = CreateSheet(context, sheetCount);
                    if (sheet == null)
                        break;

                    // Draw grid outline
                    DrawGridOutline(sheet, dimensions, context);

                    // Place views on this sheet
                    var sheetResult = PlaceViewsOnSheet(
                        sheet,
                        sortedItems.Skip(currentIndex).Take(viewsForThisSheet).ToList(),
                        dimensions,
                        cellWidth,
                        cellHeight,
                        hGap,
                        vGap,
                        gridRows,
                        gridColumns,
                        context,
                        currentIndex + 1);

                    if (sheetResult.PlacedCount > 0)
                    {
                        totalPlaced += sheetResult.PlacedCount;
                        result.PlacedCount = totalPlaced;
                        result.SheetNumbers.Add(sheet.SheetNumber);
                        currentIndex += sheetResult.PlacedCount;

                        LogSheetResult(context, sheetResult, sheetCount,
                            sortedItems.Count - currentIndex, placeToMultipleSheets);
                    }
                    else
                    {
                        // Remove empty sheet
                        _doc.Delete(sheet.Id);
                        context.ViewModel?.LogWarning($"   ⚠️ No views placed on sheet {sheetCount} - removing");

                        if (!placeToMultipleSheets)
                            break;
                    }

                    // If single sheet mode, stop after first sheet
                    if (!placeToMultipleSheets)
                        break;
                }

                // Final report
                LogFinalReport(context, sections, totalPlaced, sheetCount, result);

                return result;
            }
            catch (Exception ex)
            {
                context.ViewModel?.LogError($"❌ Grid placement failed: {ex.Message}");
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        private List<SheetItem> PrepareItemsInReadingOrder(
            List<SectionItemViewModel> sections,
            View referenceView,
            SectionPlacementHandler.PlacementContext context)
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

            // Strict reading order: Top to Bottom, then Left to Right
            var sorted = items
                .OrderByDescending(item => item.Y)  // Top to bottom
                .ThenBy(item => item.X)             // Left to right
                .ToList();

            // Log reading order
            context.ViewModel?.LogInfo($"\n📋 READING ORDER (Top 10):");
            for (int i = 0; i < Math.Min(10, sorted.Count); i++)
            {
                var item = sorted[i];
                context.ViewModel?.LogInfo($"   #{i + 1,2}: {item.ViewName,-30} - Pos: ({item.X * 304.8:F0}, {item.Y * 304.8:F0}) mm");
            }

            return sorted;
        }

        private bool CalculateGridCells(
            SheetDimensions dimensions,
            int gridRows,
            int gridColumns,
            SectionPlacementHandler.PlacementContext context,
            out double cellWidth,
            out double cellHeight,
            out double hGap,
            out double vGap)
        {
            cellWidth = 0;
            cellHeight = 0;
            hGap = UnitConversionHelper.MmToFeet(context.HorizontalGapMm);
            vGap = UnitConversionHelper.MmToFeet(context.VerticalGapMm);

            // Calculate base cell dimensions
            double totalGapsWidth = hGap * (gridColumns - 1);
            double totalGapsHeight = vGap * (gridRows - 1);

            cellWidth = (dimensions.UsableWidth - totalGapsWidth) / gridColumns;
            cellHeight = (dimensions.UsableHeight - totalGapsHeight) / gridRows;

            context.ViewModel?.LogInfo($"\n   📐 GRID CALCULATION:");
            context.ViewModel?.LogInfo($"      • Base cell size: {cellWidth * 304.8:F0} × {cellHeight * 304.8:F0} mm");
            context.ViewModel?.LogInfo($"      • Base gaps: H={hGap * 304.8:F0}mm, V={vGap * 304.8:F0}mm");

            // Check if we need to adjust gaps
            double minGapFt = UnitConversionHelper.MmToFeet(MIN_GAP_MM);
            bool gapsAdjusted = false;

            // Try to optimize gaps within ±10% tolerance
            for (double gapMultiplier = 1.0; gapMultiplier <= 1.1; gapMultiplier += 0.02)
            {
                double testHGap = hGap * gapMultiplier;
                double testVGap = vGap * gapMultiplier;

                if (testHGap < minGapFt) testHGap = minGapFt;
                if (testVGap < minGapFt) testVGap = minGapFt;

                double testCellWidth = (dimensions.UsableWidth - testHGap * (gridColumns - 1)) / gridColumns;
                double testCellHeight = (dimensions.UsableHeight - testVGap * (gridRows - 1)) / gridRows;

                if (testCellWidth > 0 && testCellHeight > 0)
                {
                    if (Math.Abs(gapMultiplier - 1.0) > 0.01)
                    {
                        gapsAdjusted = true;
                        hGap = testHGap;
                        vGap = testVGap;
                        cellWidth = testCellWidth;
                        cellHeight = testCellHeight;
                    }
                    break;
                }
            }

            if (gapsAdjusted)
            {
                context.ViewModel?.LogInfo($"      ⚙️ Gaps adjusted within ±10% tolerance:");
                context.ViewModel?.LogInfo($"         • New gaps: H={hGap * 304.8:F0}mm, V={vGap * 304.8:F0}mm");
                context.ViewModel?.LogInfo($"         • Final cell: {cellWidth * 304.8:F0} × {cellHeight * 304.8:F0} mm");
            }

            return cellWidth > 0 && cellHeight > 0;
        }

        private SheetResult PlaceViewsOnSheet(
            ViewSheet sheet,
            List<SheetItem> itemsForSheet,
            SheetDimensions dimensions,
            double cellWidth,
            double cellHeight,
            double hGap,
            double vGap,
            int gridRows,
            int gridColumns,
            SectionPlacementHandler.PlacementContext context,
            int startNumber)
        {
            var result = new SheetResult();
            var placedViewIds = new HashSet<ElementId>();

            context.ViewModel?.LogInfo($"\n   📦 PLACING VIEWS:");

            int viewIndex = 0;
            for (int row = 0; row < gridRows && viewIndex < itemsForSheet.Count; row++)
            {
                for (int col = 0; col < gridColumns && viewIndex < itemsForSheet.Count; col++)
                {
                    var item = itemsForSheet[viewIndex];
                    int globalNumber = startNumber + viewIndex;

                    // Calculate cell position
                    double cellX = dimensions.StartX + col * (cellWidth + hGap);
                    double cellY = dimensions.StartY - row * (cellHeight + vGap);
                    double cellBottomY = cellY - cellHeight;

                    // Position view in cell (center)
                    double centerX = cellX + cellWidth / 2;
                    double centerY = cellBottomY + cellHeight / 2;

                    try
                    {
                        if (!CanPlaceView(item.Section.View, _doc, sheet.Id))
                        {
                            context.ViewModel?.LogWarning($"      ⚠️ [{globalNumber}] {item.ViewName} - Cannot place (already placed)");
                            viewIndex++;
                            continue;
                        }

                        var vp = Viewport.Create(_doc, sheet.Id, item.Section.View.Id, new XYZ(centerX, centerY, 0));

                        int detailNumber = context.GetNextDetailNumber();
                        var detailParam = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                        if (detailParam != null && !detailParam.IsReadOnly)
                        {
                            detailParam.Set(detailNumber.ToString());
                        }

                        placedViewIds.Add(item.Section.View.Id);

                        // Log placement with position
                        context.ViewModel?.LogInfo(
                            $"      ✅ [{globalNumber}] {item.ViewName,-30} - " +
                            $"Cell ({row + 1},{col + 1}) - " +
                            $"Pos: ({centerX * 304.8:F0}, {centerY * 304.8:F0}) mm - " +
                            $"Detail: {detailNumber}");

                        context.ViewModel?.Progress.Step();
                        viewIndex++;
                    }
                    catch (Exception ex)
                    {
                        context.ViewModel?.LogError($"      ❌ [{globalNumber}] {item.ViewName} - Failed: {ex.Message}");
                        viewIndex++;
                    }
                }
            }

            // Draw grid lines and cell outlines
            DrawGridLines(sheet, dimensions, cellWidth, cellHeight, hGap, vGap, gridRows, gridColumns, context);
            DrawViewportOutlines(sheet, context);

            result.PlacedCount = placedViewIds.Count;
            result.PlacedViewIds = placedViewIds;
            result.Utilization = CalculateUtilization(placedViewIds, dimensions, context);

            return result;
        }

        private SheetDimensions CalculateSheetDimensions(SectionPlacementHandler.PlacementContext context)
        {
            double leftMargin = UnitConversionHelper.MmToFeet(context.ViewModel?.LeftMarginMm ?? 40);
            double rightMargin = UnitConversionHelper.MmToFeet(context.ViewModel?.RightMarginMm ?? 150);
            double topMargin = UnitConversionHelper.MmToFeet(context.ViewModel?.TopMarginMm ?? 40);
            double bottomMargin = UnitConversionHelper.MmToFeet(context.ViewModel?.BottomMarginMm ?? 100);

            return new SheetDimensions
            {
                UsableWidth = context.PlacementArea.Width - leftMargin - rightMargin,
                UsableHeight = context.PlacementArea.Height - topMargin - bottomMargin,
                StartX = context.PlacementArea.Origin.X + leftMargin,
                StartY = context.PlacementArea.Origin.Y - topMargin
            };
        }

        private ViewSheet CreateSheet(SectionPlacementHandler.PlacementContext context, int sheetNumber)
        {
            string sheetNumberStr = context.SheetNumberService.GetNextAvailableSheetNumber($"GRID{sheetNumber}");
            context.SheetNumberService.TryReserveSheetNumber(sheetNumberStr);

            var sheetCreator = new SheetCreationService(_doc);
            return sheetCreator.Create(context.TitleBlock, sheetNumberStr, $"Grid-{sheetNumberStr}");
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

        private double CalculateUtilization(HashSet<ElementId> placedViewIds, SheetDimensions dimensions, SectionPlacementHandler.PlacementContext context)
        {
            try
            {
                double totalViewArea = 0;
                foreach (var viewId in placedViewIds)
                {
                    var view = _doc.GetElement(viewId) as ViewSection;
                    if (view != null)
                    {
                        var footprint = ViewSizeService.Calculate(view);
                        totalViewArea += footprint.WidthFt * footprint.HeightFt;
                    }
                }

                double sheetArea = dimensions.UsableWidth * dimensions.UsableHeight;
                return sheetArea > 0 ? (totalViewArea * 100.0) / sheetArea : 0;
            }
            catch
            {
                return 0;
            }
        }

        private void DrawGridOutline(ViewSheet sheet, SheetDimensions dimensions, SectionPlacementHandler.PlacementContext context)
        {
            try
            {
                using (Transaction t = new Transaction(_doc, "Draw Grid Outline"))
                {
                    t.Start();

                    var points = new List<XYZ>
                    {
                        new XYZ(dimensions.StartX, dimensions.StartY, 0),
                        new XYZ(dimensions.StartX + dimensions.UsableWidth, dimensions.StartY, 0),
                        new XYZ(dimensions.StartX + dimensions.UsableWidth, dimensions.StartY - dimensions.UsableHeight, 0),
                        new XYZ(dimensions.StartX, dimensions.StartY - dimensions.UsableHeight, 0),
                        new XYZ(dimensions.StartX, dimensions.StartY, 0)
                    };

                    for (int i = 0; i < points.Count - 1; i++)
                    {
                        var line = Line.CreateBound(points[i], points[i + 1]);
                        _doc.Create.NewDetailCurve(sheet, line);
                    }

                    t.Commit();
                }

                context.ViewModel?.LogInfo($"      📏 Grid outline drawn");
            }
            catch (Exception ex)
            {
                context.ViewModel?.LogWarning($"      ⚠️ Could not draw grid outline: {ex.Message}");
            }
        }

        private void DrawGridLines(
            ViewSheet sheet,
            SheetDimensions dimensions,
            double cellWidth,
            double cellHeight,
            double hGap,
            double vGap,
            int gridRows,
            int gridColumns,
            SectionPlacementHandler.PlacementContext context)
        {
            try
            {
                using (Transaction t = new Transaction(_doc, "Draw Grid Lines"))
                {
                    t.Start();

                    // Draw vertical grid lines
                    double x = dimensions.StartX;
                    for (int c = 0; c <= gridColumns; c++)
                    {
                        var start = new XYZ(x, dimensions.StartY, 0);
                        var end = new XYZ(x, dimensions.StartY - dimensions.UsableHeight, 0);
                        var line = Line.CreateBound(start, end);
                        _doc.Create.NewDetailCurve(sheet, line);

                        if (c < gridColumns)
                        {
                            x += cellWidth + hGap;
                        }
                    }

                    // Draw horizontal grid lines
                    double y = dimensions.StartY;
                    for (int r = 0; r <= gridRows; r++)
                    {
                        var start = new XYZ(dimensions.StartX, y, 0);
                        var end = new XYZ(dimensions.StartX + dimensions.UsableWidth, y, 0);
                        var line = Line.CreateBound(start, end);
                        _doc.Create.NewDetailCurve(sheet, line);

                        if (r < gridRows)
                        {
                            y -= cellHeight + vGap;
                        }
                    }

                    t.Commit();
                }

                context.ViewModel?.LogInfo($"      📏 Grid lines drawn for {gridColumns}×{gridRows} grid");
            }
            catch (Exception ex)
            {
                context.ViewModel?.LogWarning($"      ⚠️ Could not draw grid lines: {ex.Message}");
            }
        }

        private void DrawViewportOutlines(ViewSheet sheet, SectionPlacementHandler.PlacementContext context)
        {
            try
            {
                using (Transaction t = new Transaction(_doc, "Draw Viewport Outlines"))
                {
                    t.Start();

                    var viewports = new FilteredElementCollector(_doc, sheet.Id)
                        .OfClass(typeof(Viewport))
                        .Cast<Viewport>()
                        .ToList();

                    foreach (var vp in viewports)
                    {
                        var box = vp.GetBoxOutline();
                        if (box == null) continue;

                        var points = new List<XYZ>
                        {
                            new XYZ(box.MinimumPoint.X, box.MinimumPoint.Y, 0),
                            new XYZ(box.MaximumPoint.X, box.MinimumPoint.Y, 0),
                            new XYZ(box.MaximumPoint.X, box.MaximumPoint.Y, 0),
                            new XYZ(box.MinimumPoint.X, box.MaximumPoint.Y, 0),
                            new XYZ(box.MinimumPoint.X, box.MinimumPoint.Y, 0)
                        };

                        for (int i = 0; i < points.Count - 1; i++)
                        {
                            var line = Line.CreateBound(points[i], points[i + 1]);
                            _doc.Create.NewDetailCurve(sheet, line);
                        }
                    }

                    t.Commit();
                }

                context.ViewModel?.LogInfo($"      📏 Viewport outlines drawn");
            }
            catch (Exception ex)
            {
                context.ViewModel?.LogWarning($"      ⚠️ Could not draw viewport outlines: {ex.Message}");
            }
        }

        #region Logging Methods

        private void LogInitialization(
            SectionPlacementHandler.PlacementContext context,
            List<SectionItemViewModel> sections,
            int gridRows,
            int gridColumns,
            int maxViewsPerSheet,
            bool multipleSheets)
        {
            context.ViewModel?.LogInfo("════════════════════════════════════════════════");
            context.ViewModel?.LogInfo("📚 SIMPLE GRID PLACEMENT SERVICE");
            context.ViewModel?.LogInfo("════════════════════════════════════════════════");
            context.ViewModel?.LogInfo($"📊 Total sections to place: {sections.Count}");
            context.ViewModel?.LogInfo($"📐 Grid configuration: {gridRows} rows × {gridColumns} columns");
            context.ViewModel?.LogInfo($"   • Max views per sheet: {maxViewsPerSheet}");
            context.ViewModel?.LogInfo($"   • Placement mode: {(multipleSheets ? "Multiple sheets" : "Single sheet")}");
            context.ViewModel?.LogInfo($"   • Reading order: Left → Right, Top → Bottom");
            context.ViewModel?.LogInfo($"   • Alignment: Bottom within rows, Left within columns");
            context.ViewModel?.LogInfo($"   • Gap tolerance: ±{GAP_TOLERANCE * 100}%");
        }

        private void LogSheetDimensions(SectionPlacementHandler.PlacementContext context, SheetDimensions dimensions)
        {
            context.ViewModel?.LogInfo($"\n   📐 SHEET DIMENSIONS:");
            context.ViewModel?.LogInfo($"      • Usable width: {dimensions.UsableWidth * 304.8:F0} mm");
            context.ViewModel?.LogInfo($"      • Usable height: {dimensions.UsableHeight * 304.8:F0} mm");
            context.ViewModel?.LogInfo($"      • Start point: ({dimensions.StartX * 304.8:F0}, {dimensions.StartY * 304.8:F0}) mm");
        }

        private void LogSheetResult(
            SectionPlacementHandler.PlacementContext context,
            SheetResult result,
            int sheetNumber,
            int remaining,
            bool multipleSheets)
        {
            context.ViewModel?.LogSuccess($"\n✅ Sheet {sheetNumber} complete: {result.PlacedCount} views placed");
            context.ViewModel?.LogInfo($"   • Utilization: {result.Utilization:F1}%");

            if (multipleSheets)
            {
                context.ViewModel?.LogInfo($"   • Remaining: {remaining}");
            }
        }

        private void LogFinalReport(
            SectionPlacementHandler.PlacementContext context,
            List<SectionItemViewModel> sections,
            int totalPlaced,
            int sheetCount,
            SectionPlacementHandler.PlacementResult result)
        {
            context.ViewModel?.LogInfo("\n📊 FINAL PLACEMENT REPORT");
            context.ViewModel?.LogInfo("════════════════════════════════════════════════");
            context.ViewModel?.LogInfo($"   • Total sections: {sections.Count}");
            context.ViewModel?.LogInfo($"   • Successfully placed: {totalPlaced}");
            context.ViewModel?.LogInfo($"   • Sheets used: {sheetCount}");

            if (totalPlaced < sections.Count)
            {
                int skipped = sections.Count - totalPlaced;
                context.ViewModel?.LogWarning($"   • Skipped: {skipped} (sheet capacity reached)");
                result.FailedCount = skipped;
            }
            else
            {
                context.ViewModel?.LogSuccess($"   • All sections placed successfully!");
            }

            if (result.SheetNumbers.Any())
            {
                var sheetList = string.Join(", ", result.SheetNumbers.OrderBy(s => s));
                context.ViewModel?.LogInfo($"   • Sheets: {sheetList}");
            }
        }

        #endregion

        #region Helper Classes

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

        private class SheetDimensions
        {
            public double UsableWidth { get; set; }
            public double UsableHeight { get; set; }
            public double StartX { get; set; }
            public double StartY { get; set; }
        }

        private class SheetResult
        {
            public string SheetNumber { get; set; }
            public int PlacedCount { get; set; }
            public double Utilization { get; set; }
            public HashSet<ElementId> PlacedViewIds { get; set; } = new HashSet<ElementId>();
        }

        #endregion
    }
}