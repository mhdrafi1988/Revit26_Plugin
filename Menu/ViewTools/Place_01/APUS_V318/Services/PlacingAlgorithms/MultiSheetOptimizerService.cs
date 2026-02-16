// File: MultiSheetOptimizerService.cs
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
    /// MULTI-SHEET OPTIMIZER - Places views across multiple sheets with visual outlines
    /// 
    /// ENHANCED FEATURES:
    /// ✓ Detail line outline showing placement area on sheet
    /// ✓ Fill empty areas with views from sorted list (top order)
    /// ✓ Try first 20 items for optimal batch placement
    /// ✓ Dynamic row grouping and view rearrangement
    /// ✓ Flexible gap adjustment within ±10% tolerance
    /// ✓ Comprehensive space utilization logging
    /// </summary>
    public class MultiSheetOptimizerService
    {
        private readonly Document _doc;
        private const double MIN_GAP_MM = 3;
        private const double GAP_TOLERANCE = 0.10;
        private const int MAX_SHEETS = 50;
        private const int GRID_SCAN_STEP_MM = 2;
        private const int INITIAL_BATCH_SIZE = 20;

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

            // ===== LIVE UI LOGGING WITH SPACE ANALYSIS =====
            LogInitialization(context, sections);
            LogSheetDimensions(context);

            try
            {
                var remainingSections = new List<SectionItemViewModel>(sections);
                int sheetCount = 0;
                int totalPlaced = 0;

                // Prepare all items with their dimensions
                var allItems = PrepareItems(remainingSections, referenceView);
                LogSortedItems(context, allItems);
                LogTotalViewsArea(context, allItems);

                while (remainingSections.Any() && sheetCount < MAX_SHEETS)
                {
                    sheetCount++;
                    context.ViewModel?.LogInfo($"\n══════════════════════════════════════════════════════════════");
                    context.ViewModel?.LogInfo($"📄 SHEET {sheetCount} - Remaining: {remainingSections.Count} views");
                    context.ViewModel?.LogInfo($"══════════════════════════════════════════════════════════════");

                    var sheetResult = ProcessSingleSheet(context, remainingSections, referenceView, sheetCount);

                    if (sheetResult.PlacedCount > 0)
                    {
                        totalPlaced += sheetResult.PlacedCount;
                        result.PlacedCount = totalPlaced;
                        result.SheetNumbers.Add(sheetResult.SheetNumber);

                        remainingSections = remainingSections
                            .Where(s => !sheetResult.PlacedViewIds.Contains(s.View.Id))
                            .ToList();

                        LogSheetSuccess(context, sheetResult, sheetCount, remainingSections.Count);
                    }
                    else
                    {
                        context.ViewModel?.LogWarning($"\n⚠️ Could not place any views on sheet {sheetCount} - stopping");
                        break;
                    }

                    if (!remainingSections.Any())
                    {
                        context.ViewModel?.LogSuccess($"\n✅ ALL SECTIONS PLACED across {sheetCount} sheets!");
                        break;
                    }
                }

                LogFinalReport(context, sections, totalPlaced, sheetCount, remainingSections, result);
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
            // Calculate sheet dimensions with detailed logging
            var sheetDimensions = CalculateSheetDimensions(context);
            var allItems = PrepareItems(remainingSections, referenceView);

            LogDetailedSheetDimensions(context, sheetDimensions);

            // Create sheet
            var sheet = CreateSheet(context, sheetNumber);
            if (sheet == null) return new SheetResult { PlacedCount = 0 };

            // Draw placement area outline
            DrawPlacementAreaOutline(sheet, sheetDimensions, context);

            // Try with initial batch of 20
            var optimalLayout = FindOptimalLayoutWithBatch(allItems, sheetDimensions, context, INITIAL_BATCH_SIZE);

            // If 20 doesn't work, try decreasing batch sizes
            if (optimalLayout == null || optimalLayout.PlacedCount < 5)
            {
                context.ViewModel?.LogInfo($"   🔄 Batch size {INITIAL_BATCH_SIZE} not optimal, trying decreasing sizes...");

                for (int batchSize = 19; batchSize >= 10; batchSize -= 2)
                {
                    optimalLayout = FindOptimalLayoutWithBatch(allItems, sheetDimensions, context, batchSize);
                    if (optimalLayout != null && optimalLayout.PlacedCount >= batchSize * 0.8)
                    {
                        context.ViewModel?.LogInfo($"   ✓ Found good layout with batch size {batchSize}");
                        break;
                    }
                }
            }

            // If still no good layout, try filling with whatever fits
            if (optimalLayout == null || !optimalLayout.PlacedItems.Any())
            {
                context.ViewModel?.LogInfo($"   🔄 Trying to fill with available views...");
                optimalLayout = FillWithAvailableViews(allItems, sheetDimensions, context);
            }

            if (optimalLayout == null || !optimalLayout.PlacedItems.Any())
            {
                _doc.Delete(sheet.Id);
                context.ViewModel?.LogInfo($"      ⚠️ No views placed - sheet deleted");
                return new SheetResult { SheetNumber = sheet.SheetNumber, PlacedCount = 0 };
            }

            // Log space utilization before placement
            LogPrePlacementAnalysis(context, optimalLayout, sheetDimensions);

            // Execute placement
            var result = ExecutePlacement(sheet, optimalLayout, context);
            result.SheetNumber = sheet.SheetNumber;

            // Draw viewport outlines after placement
            DrawViewportOutlines(sheet, context);

            // Log final space utilization
            LogPostPlacementAnalysis(context, sheet, sheetDimensions);

            LogPlacementResult(context, result, optimalLayout);
            return result;
        }

        private void LogSheetDimensions(SectionPlacementHandler.PlacementContext context)
        {
            context.ViewModel?.LogInfo("\n📐 SHEET DIMENSIONS ANALYSIS");
            context.ViewModel?.LogInfo("══════════════════════════════════════════════════════════════");

            // Total sheet area from Revit
            double totalWidthMm = context.PlacementArea.Width * 304.8;
            double totalHeightMm = context.PlacementArea.Height * 304.8;

            context.ViewModel?.LogInfo($"📍 Total sheet from Revit:");
            context.ViewModel?.LogInfo($"   • Width: {totalWidthMm:F0} mm");
            context.ViewModel?.LogInfo($"   • Height: {totalHeightMm:F0} mm");
            context.ViewModel?.LogInfo($"   • Total area: {totalWidthMm * totalHeightMm / 1000000:F2} m²");
        }

        private void LogDetailedSheetDimensions(SectionPlacementHandler.PlacementContext context, SheetDimensions dims)
        {
            context.ViewModel?.LogInfo($"\n   📐 DETAILED SPACE ANALYSIS:");

            // Convert all to mm for clarity
            double totalWidthMm = context.PlacementArea.Width * 304.8;
            double totalHeightMm = context.PlacementArea.Height * 304.8;
            double leftMarginMm = (context.ViewModel?.LeftMarginMm ?? 40);
            double rightMarginMm = (context.ViewModel?.RightMarginMm ?? 150);
            double topMarginMm = (context.ViewModel?.TopMarginMm ?? 40);
            double bottomMarginMm = (context.ViewModel?.BottomMarginMm ?? 100);
            double usableWidthMm = dims.UsableWidth * 304.8;
            double usableHeightMm = dims.UsableHeight * 304.8;

            // Calculate what's consuming the space
            double titleBlockLeftLoss = Math.Abs(context.PlacementArea.Origin.X * 304.8);
            double titleBlockRightLoss = totalWidthMm - usableWidthMm - leftMarginMm - rightMarginMm - titleBlockLeftLoss;

            context.ViewModel?.LogInfo($"      📏 RAW SHEET DIMENSIONS:");
            context.ViewModel?.LogInfo($"         • Revit total width: {totalWidthMm:F0} mm");
            context.ViewModel?.LogInfo($"         • Revit total height: {totalHeightMm:F0} mm");

            context.ViewModel?.LogInfo($"      📏 MARGINS (from UI):");
            context.ViewModel?.LogInfo($"         • Left: {leftMarginMm:F0} mm");
            context.ViewModel?.LogInfo($"         • Right: {rightMarginMm:F0} mm");
            context.ViewModel?.LogInfo($"         • Top: {topMarginMm:F0} mm");
            context.ViewModel?.LogInfo($"         • Bottom: {bottomMarginMm:F0} mm");
            context.ViewModel?.LogInfo($"         • Total horizontal margin loss: {leftMarginMm + rightMarginMm:F0} mm");
            context.ViewModel?.LogInfo($"         • Total vertical margin loss: {topMarginMm + bottomMarginMm:F0} mm");

            context.ViewModel?.LogInfo($"      📏 TITLE BLOCK IMPACT:");
            context.ViewModel?.LogInfo($"         • Left side non-usable: {titleBlockLeftLoss:F0} mm (title block border)");
            context.ViewModel?.LogInfo($"         • Right side non-usable: {titleBlockRightLoss:F0} mm (title block border)");
            context.ViewModel?.LogInfo($"         • Total title block loss: {titleBlockLeftLoss + titleBlockRightLoss:F0} mm");

            context.ViewModel?.LogInfo($"      📏 USABLE SPACE:");
            context.ViewModel?.LogInfo($"         • Width: {usableWidthMm:F0} mm");
            context.ViewModel?.LogInfo($"         • Height: {usableHeightMm:F0} mm");
            context.ViewModel?.LogInfo($"         • Area: {usableWidthMm * usableHeightMm / 1000000:F2} m²");

            // Calculate total loss
            double totalWidthLoss = totalWidthMm - usableWidthMm;
            double totalHeightLoss = totalHeightMm - usableHeightMm;
            double widthLossPercent = (totalWidthLoss / totalWidthMm) * 100;
            double heightLossPercent = (totalHeightLoss / totalHeightMm) * 100;

            context.ViewModel?.LogInfo($"      📉 SPACE LOSS SUMMARY:");
            context.ViewModel?.LogInfo($"         • Width lost: {totalWidthLoss:F0} mm ({widthLossPercent:F1}%)");
            context.ViewModel?.LogInfo($"         • Height lost: {totalHeightLoss:F0} mm ({heightLossPercent:F1}%)");
            context.ViewModel?.LogInfo($"         • Total area lost: {(totalWidthMm * totalHeightMm - usableWidthMm * usableHeightMm) / 1000000:F2} m²");
        }

        private void LogTotalViewsArea(SectionPlacementHandler.PlacementContext context, List<SheetItem> items)
        {
            double totalViewsArea = items.Sum(i => i.Width * i.Height) * 304.8 * 304.8 / 1000000; // Convert to m²
            double avgViewArea = totalViewsArea / items.Count;

            context.ViewModel?.LogInfo($"\n📊 VIEWS AREA ANALYSIS:");
            context.ViewModel?.LogInfo($"   • Total views to place: {items.Count}");
            context.ViewModel?.LogInfo($"   • Combined views area: {totalViewsArea:F2} m²");
            context.ViewModel?.LogInfo($"   • Average view size: {avgViewArea:F2} m²");
        }

        private void LogPrePlacementAnalysis(SectionPlacementHandler.PlacementContext context, OptimalLayout layout, SheetDimensions dims)
        {
            double usableArea = dims.UsableWidth * dims.UsableHeight * 304.8 * 304.8 / 1000000;
            double viewsArea = layout.PlacedItems.Sum(i => i.Item.Width * i.Item.Height) * 304.8 * 304.8 / 1000000;
            double gapArea = (layout.PlacedItems.Count - 1) * (layout.HorizontalGapUsed * layout.VerticalGapUsed) * 304.8 * 304.8 / 1000000;

            context.ViewModel?.LogInfo($"\n   📊 PRE-PLACEMENT ANALYSIS:");
            context.ViewModel?.LogInfo($"      • Views to place: {layout.PlacedCount}");
            context.ViewModel?.LogInfo($"      • Views total area: {viewsArea:F2} m²");
            context.ViewModel?.LogInfo($"      • Gap area needed: {gapArea:F2} m²");
            context.ViewModel?.LogInfo($"      • Total needed area: {viewsArea + gapArea:F2} m²");
            context.ViewModel?.LogInfo($"      • Available area: {usableArea:F2} m²");
            context.ViewModel?.LogInfo($"      • Projected utilization: {layout.Utilization:F1}%");
        }

        private void LogPostPlacementAnalysis(SectionPlacementHandler.PlacementContext context, ViewSheet sheet, SheetDimensions dims)
        {
            try
            {
                var viewports = new FilteredElementCollector(_doc, sheet.Id)
                    .OfClass(typeof(Viewport))
                    .Cast<Viewport>()
                    .ToList();

                double actualViewsArea = 0;
                foreach (var vp in viewports)
                {
                    var box = vp.GetBoxOutline();
                    if (box != null)
                    {
                        double width = (box.MaximumPoint.X - box.MinimumPoint.X) * 304.8;
                        double height = (box.MaximumPoint.Y - box.MinimumPoint.Y) * 304.8;
                        actualViewsArea += width * height / 1000000;
                    }
                }

                double usableArea = dims.UsableWidth * dims.UsableHeight * 304.8 * 304.8 / 1000000;
                double actualUtilization = (actualViewsArea / usableArea) * 100;

                context.ViewModel?.LogInfo($"\n   📊 POST-PLACEMENT ANALYSIS:");
                context.ViewModel?.LogInfo($"      • Actual views placed: {viewports.Count}");
                context.ViewModel?.LogInfo($"      • Actual views area: {actualViewsArea:F2} m²");
                context.ViewModel?.LogInfo($"      • Actual utilization: {actualUtilization:F1}%");

                // Calculate unused space
                double unusedArea = usableArea - actualViewsArea;
                context.ViewModel?.LogInfo($"      • Unused area: {unusedArea:F2} m²");

                if (unusedArea > 0.1) // More than 0.1 m² unused
                {
                    context.ViewModel?.LogInfo($"      • Could potentially fit {Math.Floor(unusedArea / 0.5)} more small views");
                }
            }
            catch { }
        }

        private OptimalLayout FindOptimalLayoutWithBatch(
            List<SheetItem> allItems,
            SheetDimensions dimensions,
            SectionPlacementHandler.PlacementContext context,
            int targetBatchSize)
        {
            context.ViewModel?.LogInfo($"      🔄 Testing batch size: {targetBatchSize}...");

            var itemsToTest = allItems.Take(targetBatchSize).ToList();
            var bestLayout = new OptimalLayout { PlacedCount = 0, Utilization = 0 };

            // Try different arrangement strategies
            var strategies = new List<Func<List<SheetItem>, List<ArrangementStrategy>>>
            {
                (items) => GenerateSizeBasedArrangements(items),
                (items) => GenerateHeightBalancedArrangements(items),
                (items) => GenerateWidthOptimizedArrangements(items),
                (items) => GenerateOriginalOrderArrangements(items)
            };

            foreach (var strategyGenerator in strategies)
            {
                var arrangements = strategyGenerator(itemsToTest);

                foreach (var arrangement in arrangements)
                {
                    // Try different gap adjustments
                    for (double gapMultiplier = 0.9; gapMultiplier <= 1.1; gapMultiplier += 0.02)
                    {
                        double currentHGap = UnitConversionHelper.MmToFeet(context.HorizontalGapMm) * gapMultiplier;
                        double currentVGap = UnitConversionHelper.MmToFeet(context.VerticalGapMm) * gapMultiplier;

                        // Ensure minimum gap
                        if (currentHGap * 304.8 < MIN_GAP_MM) currentHGap = UnitConversionHelper.MmToFeet(MIN_GAP_MM);
                        if (currentVGap * 304.8 < MIN_GAP_MM) currentVGap = UnitConversionHelper.MmToFeet(MIN_GAP_MM);

                        var layout = TryArrangeViews(
                            arrangement.Rows,
                            dimensions,
                            currentHGap,
                            currentVGap);

                        if (layout != null && layout.PlacedItems.Count > bestLayout.PlacedCount)
                        {
                            bestLayout = new OptimalLayout
                            {
                                PlacedCount = layout.PlacedItems.Count,
                                PlacedItems = layout.PlacedItems,
                                RowsUsed = layout.RowsUsed,
                                ColumnsPerRow = layout.ColumnsPerRow,
                                Utilization = layout.Utilization,
                                HorizontalGapUsed = currentHGap,
                                VerticalGapUsed = currentVGap,
                                ArrangementType = arrangement.Type
                            };

                            if (bestLayout.PlacedCount >= targetBatchSize * 0.7)
                            {
                                var filledLayout = FillRemainingArea(
                                    bestLayout,
                                    allItems.Skip(bestLayout.PlacedCount).ToList(),
                                    dimensions,
                                    context);

                                if (filledLayout.PlacedCount > bestLayout.PlacedCount)
                                {
                                    bestLayout = filledLayout;
                                    context.ViewModel?.LogInfo($"            • After area filling: {bestLayout.PlacedCount} views");
                                }
                            }
                        }
                    }
                }
            }

            return bestLayout.PlacedCount > 0 ? bestLayout : null;
        }

        private OptimalLayout FillRemainingArea(
            OptimalLayout currentLayout,
            List<SheetItem> remainingItems,
            SheetDimensions dimensions,
            SectionPlacementHandler.PlacementContext context)
        {
            var result = new OptimalLayout
            {
                PlacedCount = currentLayout.PlacedCount,
                PlacedItems = new List<PlacedItem>(currentLayout.PlacedItems),
                RowsUsed = currentLayout.RowsUsed,
                ColumnsPerRow = new List<int>(currentLayout.ColumnsPerRow),
                Utilization = currentLayout.Utilization,
                HorizontalGapUsed = currentLayout.HorizontalGapUsed,
                VerticalGapUsed = currentLayout.VerticalGapUsed,
                ArrangementType = currentLayout.ArrangementType + " + Filled"
            };

            var occupiedSpaces = currentLayout.PlacedItems.Select(p => new BoundingBox
            {
                MinX = p.X,
                MaxX = p.X + p.Item.Width,
                MinY = p.Y,
                MaxY = p.Y + p.Item.Height
            }).ToList();

            foreach (var item in remainingItems)
            {
                var position = FindBestPositionInRemainingArea(
                    item,
                    occupiedSpaces,
                    dimensions,
                    currentLayout.HorizontalGapUsed,
                    currentLayout.VerticalGapUsed);

                if (position != null)
                {
                    result.PlacedItems.Add(new PlacedItem
                    {
                        Item = item,
                        X = position.X,
                        Y = position.Y,
                        CenterX = position.X + item.Width / 2,
                        CenterY = position.Y + item.Height / 2
                    });

                    occupiedSpaces.Add(new BoundingBox
                    {
                        MinX = position.X,
                        MaxX = position.X + item.Width,
                        MinY = position.Y,
                        MaxY = position.Y + item.Height
                    });

                    result.PlacedCount++;
                }
            }

            result.Utilization = CalculateUtilization(result.PlacedItems, dimensions);
            return result;
        }

        private OptimalLayout FillWithAvailableViews(
            List<SheetItem> allItems,
            SheetDimensions dimensions,
            SectionPlacementHandler.PlacementContext context)
        {
            var result = new OptimalLayout
            {
                PlacedItems = new List<PlacedItem>(),
                HorizontalGapUsed = UnitConversionHelper.MmToFeet(context.HorizontalGapMm),
                VerticalGapUsed = UnitConversionHelper.MmToFeet(context.VerticalGapMm),
                ArrangementType = "Available Views Fill"
            };

            var occupiedSpaces = new List<BoundingBox>();

            foreach (var item in allItems)
            {
                var position = FindBestPositionInRemainingArea(
                    item,
                    occupiedSpaces,
                    dimensions,
                    result.HorizontalGapUsed,
                    result.VerticalGapUsed);

                if (position != null)
                {
                    result.PlacedItems.Add(new PlacedItem
                    {
                        Item = item,
                        X = position.X,
                        Y = position.Y,
                        CenterX = position.X + item.Width / 2,
                        CenterY = position.Y + item.Height / 2
                    });

                    occupiedSpaces.Add(new BoundingBox
                    {
                        MinX = position.X,
                        MaxX = position.X + item.Width,
                        MinY = position.Y,
                        MaxY = position.Y + item.Height
                    });

                    result.PlacedCount++;
                }
            }

            if (result.PlacedItems.Any())
            {
                result.RowsUsed = 1;
                result.Utilization = CalculateUtilization(result.PlacedItems, dimensions);
            }

            return result;
        }

        private Position FindBestPositionInRemainingArea(
            SheetItem item,
            List<BoundingBox> occupiedSpaces,
            SheetDimensions dimensions,
            double hGap,
            double vGap)
        {
            double scanStep = UnitConversionHelper.MmToFeet(GRID_SCAN_STEP_MM);

            for (double y = dimensions.StartY; y > dimensions.StartY - dimensions.UsableHeight; y -= scanStep)
            {
                for (double x = dimensions.StartX; x < dimensions.StartX + dimensions.UsableWidth; x += scanStep)
                {
                    if (x + item.Width > dimensions.StartX + dimensions.UsableWidth ||
                        y - item.Height < dimensions.StartY - dimensions.UsableHeight)
                        continue;

                    bool overlaps = false;
                    foreach (var space in occupiedSpaces)
                    {
                        if (x < space.MaxX + hGap && x + item.Width + hGap > space.MinX &&
                            y - item.Height < space.MaxY + vGap && y + vGap > space.MinY)
                        {
                            overlaps = true;
                            break;
                        }
                    }

                    if (!overlaps)
                    {
                        return new Position
                        {
                            X = x,
                            Y = y - item.Height
                        };
                    }
                }
            }

            return null;
        }

        private void DrawPlacementAreaOutline(ViewSheet sheet, SheetDimensions dimensions, SectionPlacementHandler.PlacementContext context)
        {
            try
            {
                using (Transaction t = new Transaction(_doc, "Draw Placement Area Outline"))
                {
                    t.Start();

                    var lineStyle = GetOrCreateDetailLineStyle();

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
                        var detailLine = _doc.Create.NewDetailCurve(sheet, line);
                        if (lineStyle != null)
                        {
                            detailLine.LineStyle = lineStyle;
                        }
                    }

                    t.Commit();
                }

                context.ViewModel?.LogInfo($"      📏 Placement area outline drawn");
            }
            catch (Exception ex)
            {
                context.ViewModel?.LogWarning($"      ⚠️ Could not draw placement outline: {ex.Message}");
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

                    int count = 0;
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

                        count++;
                    }

                    t.Commit();
                    context.ViewModel?.LogInfo($"      📏 Outlines drawn for {count} viewports");
                }
            }
            catch (Exception ex)
            {
                context.ViewModel?.LogWarning($"      ⚠️ Could not draw viewport outlines: {ex.Message}");
            }
        }

        private GraphicsStyle GetOrCreateDetailLineStyle()
        {
            try
            {
                var collector = new FilteredElementCollector(_doc);
                var lineStyles = collector.OfClass(typeof(GraphicsStyle))
                    .Cast<GraphicsStyle>()
                    .Where(g => g.GraphicsStyleCategory != null &&
                           g.GraphicsStyleCategory.Name.Contains("Dashed"))
                    .ToList();

                if (lineStyles.Any())
                    return lineStyles.First();

                return null;
            }
            catch
            {
                return null;
            }
        }

        private List<ArrangementStrategy> GenerateOriginalOrderArrangements(List<SheetItem> items)
        {
            var arrangements = new List<ArrangementStrategy>();
            arrangements.Add(CreateArrangementFromSorted(items, "Original order"));
            return arrangements;
        }

        private List<ArrangementStrategy> GenerateSizeBasedArrangements(List<SheetItem> items)
        {
            var arrangements = new List<ArrangementStrategy>();
            var sizeSorted = items.OrderByDescending(i => i.Width * i.Height).ToList();
            arrangements.Add(CreateArrangementFromSorted(sizeSorted, "Size-based (largest first)"));
            return arrangements;
        }

        private List<ArrangementStrategy> GenerateHeightBalancedArrangements(List<SheetItem> items)
        {
            var arrangements = new List<ArrangementStrategy>();
            var heightGroups = items
                .GroupBy(i => Math.Round(i.Height * 304.8 / 50) * 50)
                .OrderByDescending(g => g.Key)
                .SelectMany(g => g.OrderByDescending(i => i.Width))
                .ToList();
            arrangements.Add(CreateArrangementFromSorted(heightGroups, "Height-balanced"));
            return arrangements;
        }

        private List<ArrangementStrategy> GenerateWidthOptimizedArrangements(List<SheetItem> items)
        {
            var arrangements = new List<ArrangementStrategy>();
            var wideFirst = items.OrderByDescending(i => i.Width).ToList();
            arrangements.Add(CreateArrangementFromSorted(wideFirst, "Width-based"));
            return arrangements;
        }

        private ArrangementStrategy CreateArrangementFromSorted(List<SheetItem> sortedItems, string type)
        {
            var rows = new List<ArrangementRow>();
            var currentRow = new List<SheetItem>();

            foreach (var item in sortedItems)
            {
                currentRow.Add(item);
            }

            rows.Add(new ArrangementRow { Items = new List<SheetItem>(currentRow) });

            return new ArrangementStrategy
            {
                Rows = rows,
                Type = type
            };
        }

        private LayoutResult TryArrangeViews(
            List<ArrangementRow> rows,
            SheetDimensions dimensions,
            double hGap,
            double vGap)
        {
            var result = new LayoutResult();
            double currentY = dimensions.StartY;
            var placedItems = new List<PlacedItem>();
            var columnsPerRow = new List<int>();

            foreach (var row in rows)
            {
                if (!row.Items.Any()) continue;

                double rowHeight = row.Items.Max(i => i.Height);

                if (currentY - rowHeight < dimensions.StartY - dimensions.UsableHeight)
                    break;

                double currentX = dimensions.StartX;
                double rowBottomY = currentY - rowHeight;
                int columnsInRow = 0;

                foreach (var item in row.Items)
                {
                    if (currentX + item.Width <= dimensions.StartX + dimensions.UsableWidth)
                    {
                        var placedItem = new PlacedItem
                        {
                            Item = item,
                            X = currentX,
                            Y = rowBottomY,
                            CenterX = currentX + item.Width / 2,
                            CenterY = rowBottomY + item.Height / 2
                        };

                        placedItems.Add(placedItem);
                        columnsInRow++;

                        currentX += item.Width + hGap;
                    }
                }

                if (columnsInRow > 0)
                {
                    columnsPerRow.Add(columnsInRow);
                    currentY -= (rowHeight + vGap);
                }
            }

            if (placedItems.Any())
            {
                result.PlacedItems = placedItems;
                result.RowsUsed = columnsPerRow.Count;
                result.ColumnsPerRow = columnsPerRow;
                result.Utilization = CalculateUtilization(placedItems, dimensions);
            }

            return result;
        }

        private double CalculateUtilization(List<PlacedItem> placedItems, SheetDimensions dimensions)
        {
            double totalArea = placedItems.Sum(i => i.Item.Width * i.Item.Height);
            return totalArea / (dimensions.UsableWidth * dimensions.UsableHeight) * 100;
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
            string sheetNumberStr = context.SheetNumberService.GetNextAvailableSheetNumber($"OPT{sheetNumber}");
            context.SheetNumberService.TryReserveSheetNumber(sheetNumberStr);

            var sheetCreator = new SheetCreationService(_doc);
            return sheetCreator.Create(context.TitleBlock, sheetNumberStr, $"Optimized-{sheetNumberStr}");
        }

        private SheetResult ExecutePlacement(
            ViewSheet sheet,
            OptimalLayout layout,
            SectionPlacementHandler.PlacementContext context)
        {
            var result = new SheetResult();
            var placedViewIds = new HashSet<ElementId>();

            foreach (var placedItem in layout.PlacedItems)
            {
                try
                {
                    if (!CanPlaceView(placedItem.Item.Section.View, _doc, sheet.Id))
                        continue;

                    var vp = Viewport.Create(_doc, sheet.Id, placedItem.Item.Section.View.Id,
                        new XYZ(placedItem.CenterX, placedItem.CenterY, 0));

                    int detailNumber = context.GetNextDetailNumber();
                    var detailParam = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                    if (detailParam != null && !detailParam.IsReadOnly)
                    {
                        detailParam.Set(detailNumber.ToString());
                    }

                    placedViewIds.Add(placedItem.Item.Section.View.Id);
                }
                catch { }
            }

            result.PlacedCount = placedViewIds.Count;
            result.RowsUsed = layout.RowsUsed;
            result.ColumnsPerRow = layout.ColumnsPerRow;
            result.Utilization = layout.Utilization;
            result.PlacedViewIds = placedViewIds;
            result.BatchSize = layout.PlacedCount;
            result.ArrangementType = layout.ArrangementType;

            return result;
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

            return items;
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

        #region Logging Methods

        private void LogInitialization(SectionPlacementHandler.PlacementContext context, List<SectionItemViewModel> sections)
        {
            context.ViewModel?.LogInfo("══════════════════════════════════════════════════════════════");
            context.ViewModel?.LogInfo("📚 MULTI-SHEET OPTIMIZER - WITH SPACE UTILIZATION ANALYSIS");
            context.ViewModel?.LogInfo("══════════════════════════════════════════════════════════════");
            context.ViewModel?.LogInfo($"📊 Total sections to place: {sections.Count}");
            context.ViewModel?.LogInfo("⚙️ OPTIMIZATION FEATURES:");
            context.ViewModel?.LogInfo("   • Detail line outline of placement area");
            context.ViewModel?.LogInfo("   • Fill empty areas with views (top order)");
            context.ViewModel?.LogInfo($"   • Initial batch: {INITIAL_BATCH_SIZE} views");
            context.ViewModel?.LogInfo("   • Dynamic row grouping");
            context.ViewModel?.LogInfo($"   • Flexible gaps: {context.HorizontalGapMm}mm ±10% (min {MIN_GAP_MM}mm)");
            context.ViewModel?.LogInfo($"   • Max sheets: {MAX_SHEETS}");
        }

        private void LogSortedItems(SectionPlacementHandler.PlacementContext context, List<SheetItem> items)
        {
            context.ViewModel?.LogInfo("\n📏 Views prepared for placement (top 5):");
            for (int i = 0; i < Math.Min(5, items.Count); i++)
            {
                context.ViewModel?.LogInfo($"   #{i + 1}: {items[i].ViewName} - {items[i].Width * 304.8:F0}×{items[i].Height * 304.8:F0}mm");
            }
        }

        private void LogSheetSuccess(SectionPlacementHandler.PlacementContext context, SheetResult result, int sheetNumber, int remaining)
        {
            context.ViewModel?.LogSuccess($"\n✅ Sheet {sheetNumber} complete: {result.PlacedCount} views placed");
            context.ViewModel?.LogInfo($"   • Arrangement: {result.ArrangementType}");
            context.ViewModel?.LogInfo($"   • Utilization: {result.Utilization:F1}%");
            context.ViewModel?.LogInfo($"   • Rows: {result.RowsUsed}, Columns: {string.Join(", ", result.ColumnsPerRow)}");
            context.ViewModel?.LogInfo($"   • Remaining: {remaining}");
        }

        private void LogPlacementResult(SectionPlacementHandler.PlacementContext context, SheetResult result, OptimalLayout layout)
        {
            context.ViewModel?.LogInfo($"   📊 Final layout:");
            context.ViewModel?.LogInfo($"      • Views placed: {result.PlacedCount}");
            context.ViewModel?.LogInfo($"      • Utilization: {result.Utilization:F1}%");
            context.ViewModel?.LogInfo($"      • Rows: {result.RowsUsed}");
            context.ViewModel?.LogInfo($"      • Gaps used: H={layout.HorizontalGapUsed * 304.8:F0}mm, V={layout.VerticalGapUsed * 304.8:F0}mm");
        }

        private void LogFinalReport(
            SectionPlacementHandler.PlacementContext context,
            List<SectionItemViewModel> sections,
            int totalPlaced,
            int sheetCount,
            List<SectionItemViewModel> remaining,
            SectionPlacementHandler.PlacementResult result)
        {
            context.ViewModel?.LogInfo("\n📊 FINAL MULTI-SHEET REPORT");
            context.ViewModel?.LogInfo("══════════════════════════════════════════════════════════════");
            context.ViewModel?.LogInfo($"   • Total sections: {sections.Count}");
            context.ViewModel?.LogInfo($"   • Successfully placed: {totalPlaced}");
            context.ViewModel?.LogInfo($"   • Sheets used: {sheetCount}");
            context.ViewModel?.LogInfo($"   • Average per sheet: {totalPlaced / (double)sheetCount:F1}");

            if (remaining.Any())
            {
                context.ViewModel?.LogWarning($"   • Skipped: {remaining.Count} (max sheets reached)");
                result.FailedCount = remaining.Count;
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

        private class ArrangementStrategy
        {
            public List<ArrangementRow> Rows { get; set; }
            public string Type { get; set; }
        }

        private class ArrangementRow
        {
            public List<SheetItem> Items { get; set; } = new List<SheetItem>();
        }

        private class LayoutResult
        {
            public List<PlacedItem> PlacedItems { get; set; } = new List<PlacedItem>();
            public int RowsUsed { get; set; }
            public List<int> ColumnsPerRow { get; set; } = new List<int>();
            public double Utilization { get; set; }
        }

        private class OptimalLayout : LayoutResult
        {
            public double HorizontalGapUsed { get; set; }
            public double VerticalGapUsed { get; set; }
            public string ArrangementType { get; set; }
            public int PlacedCount { get; set; }
        }

        private class PlacedItem
        {
            public SheetItem Item { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public double CenterX { get; set; }
            public double CenterY { get; set; }
        }

        private class SheetResult
        {
            public string SheetNumber { get; set; }
            public int PlacedCount { get; set; }
            public int RowsUsed { get; set; }
            public List<int> ColumnsPerRow { get; set; } = new List<int>();
            public double Utilization { get; set; }
            public int BatchSize { get; set; }
            public string ArrangementType { get; set; }
            public HashSet<ElementId> PlacedViewIds { get; set; } = new HashSet<ElementId>();
        }

        private class BoundingBox
        {
            public double MinX { get; set; }
            public double MaxX { get; set; }
            public double MinY { get; set; }
            public double MaxY { get; set; }
        }

        private class Position
        {
            public double X { get; set; }
            public double Y { get; set; }
        }

        #endregion
    }
}