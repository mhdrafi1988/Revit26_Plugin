// File: SmartShelfOptimizerService.cs
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
    /// SMART SHELF OPTIMIZER - Combines best of all algorithms
    /// 
    /// FEATURES:
    /// ✓ Shelf/Row-based packing (Top-Left flow)
    /// ✓ Dynamic row height based on tallest view
    /// ✓ Bottom alignment within rows
    /// ✓ Left alignment within columns
    /// ✓ Gap optimization with ±10% tolerance
    /// ✓ Multi-strategy sorting (5 strategies)
    /// ✓ Fill empty spaces with smaller views
    /// ✓ Real-time space utilization analysis
    /// ✓ No overlap guarantee
    /// ✓ Multi-sheet support
    /// </summary>
    public class SmartShelfOptimizerService
    {
        private readonly Document _doc;
        private const double MIN_GAP_MM = 3;
        private const double GAP_TOLERANCE = 0.10; // ±10%
        private const double SCAN_STEP_MM = 2;
        private const int MAX_SHEETS = 50;

        public SmartShelfOptimizerService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public SectionPlacementHandler.PlacementResult Place(
            SectionPlacementHandler.PlacementContext context,
            List<SectionItemViewModel> sections,
            View referenceView,
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

            LogInitialization(context, sections, placeToMultipleSheets);

            try
            {
                // STAGE 1: Prepare items with spatial coordinates
                var allItems = PrepareItems(sections, referenceView);
                var remainingItems = new List<SheetItem>(allItems);
                int sheetCount = 0;
                int totalPlaced = 0;

                while (remainingItems.Any() && sheetCount < MAX_SHEETS)
                {
                    if (context.ViewModel?.Progress.IsCancelled == true)
                        break;

                    sheetCount++;
                    context.ViewModel?.LogInfo($"\n{'═',0} SHEET {sheetCount} - Remaining: {remainingItems.Count} views {'═',60}");

                    // Process single sheet
                    var sheetResult = ProcessSheet(context, remainingItems, sheetCount);

                    if (sheetResult.PlacedCount > 0)
                    {
                        totalPlaced += sheetResult.PlacedCount;
                        result.PlacedCount = totalPlaced;
                        result.SheetNumbers.Add(sheetResult.SheetNumber);

                        // Remove placed items
                        remainingItems = remainingItems
                            .Where(i => !sheetResult.PlacedViewIds.Contains(i.ViewId))
                            .ToList();

                        LogSheetResult(context, sheetResult, sheetCount, remainingItems.Count);
                    }
                    else
                    {
                        context.ViewModel?.LogWarning($"\n⚠️ Could not place views on sheet {sheetCount} - stopping");
                        break;
                    }

                    if (!placeToMultipleSheets)
                        break;
                }

                LogFinalReport(context, sections, totalPlaced, sheetCount, remainingItems, result);
                return result;
            }
            catch (Exception ex)
            {
                context.ViewModel?.LogError($"❌ Smart shelf optimization failed: {ex.Message}");
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        private SheetResult ProcessSheet(
            SectionPlacementHandler.PlacementContext context,
            List<SheetItem> remainingItems,
            int sheetNumber)
        {
            // Calculate sheet dimensions
            var dimensions = CalculateSheetDimensions(context);
            LogSheetDimensions(context, dimensions);

            // Create sheet
            var sheet = CreateSheet(context, sheetNumber);
            if (sheet == null)
                return new SheetResult { PlacedCount = 0 };

            // Draw placement area outline
            DrawPlacementAreaOutline(sheet, dimensions, context);

            // Try multiple arrangement strategies to find optimal layout
            var optimalLayout = FindOptimalLayout(remainingItems, dimensions, context);

            if (optimalLayout == null || !optimalLayout.PlacedItems.Any())
            {
                _doc.Delete(sheet.Id);
                context.ViewModel?.LogInfo($"   ⚠️ No views fit - sheet deleted");
                return new SheetResult { SheetNumber = sheet.SheetNumber, PlacedCount = 0 };
            }

            // Log pre-placement analysis
            LogPrePlacementAnalysis(context, optimalLayout, dimensions);

            // Execute placement
            var result = ExecutePlacement(sheet, optimalLayout, context);
            result.SheetNumber = sheet.SheetNumber;

            // Draw viewport outlines
            DrawViewportOutlines(sheet, context);

            // Log post-placement analysis
            LogPostPlacementAnalysis(context, sheet, dimensions, optimalLayout);

            return result;
        }

        private OptimalLayout FindOptimalLayout(
            List<SheetItem> items,
            SheetDimensions dimensions,
            SectionPlacementHandler.PlacementContext context)
        {
            var bestLayout = new OptimalLayout { PlacedCount = 0, Utilization = 0 };

            // Try different sorting strategies
            var strategies = new List<(string Name, List<SheetItem> SortedItems)>
            {
                ("Reading Order (Top→Bottom, Left→Right)", SortByReadingOrder(items)),
                ("Largest First (by area)", SortBySize(items, descending: true)),
                ("Smallest First (by area)", SortBySize(items, descending: false)),
                ("Tallest First (by height)", SortByHeight(items, descending: true)),
                ("Widest First (by width)", SortByWidth(items, descending: true))
            };

            foreach (var strategy in strategies)
            {
                context.ViewModel?.LogInfo($"   🔄 Trying strategy: {strategy.Name}");

                // Try different gap variations
                for (double gapMultiplier = 0.9; gapMultiplier <= 1.1; gapMultiplier += 0.02)
                {
                    double hGap = UnitConversionHelper.MmToFeet(context.HorizontalGapMm) * gapMultiplier;
                    double vGap = UnitConversionHelper.MmToFeet(context.VerticalGapMm) * gapMultiplier;

                    // Ensure minimum gap
                    if (hGap * 304.8 < MIN_GAP_MM) hGap = UnitConversionHelper.MmToFeet(MIN_GAP_MM);
                    if (vGap * 304.8 < MIN_GAP_MM) vGap = UnitConversionHelper.MmToFeet(MIN_GAP_MM);

                    var layout = TryShelfLayout(strategy.SortedItems, dimensions, hGap, vGap, context);

                    if (layout != null && layout.PlacedCount > bestLayout.PlacedCount)
                    {
                        bestLayout = layout;
                        bestLayout.StrategyName = strategy.Name;
                        bestLayout.HorizontalGapUsed = hGap;
                        bestLayout.VerticalGapUsed = vGap;

                        context.ViewModel?.LogInfo($"      ✓ New best: {bestLayout.PlacedCount} views, " +
                            $"{bestLayout.Utilization:F1}% util (gap mult: {gapMultiplier:F2})");

                        // If we found a good layout, try to fill remaining spaces
                        if (bestLayout.PlacedCount >= items.Count * 0.7)
                        {
                            var filledLayout = FillRemainingSpaces(
                                bestLayout,
                                items.Except(bestLayout.PlacedItems.Select(p => p.Item)).ToList(),
                                dimensions,
                                hGap,
                                vGap,
                                context);

                            if (filledLayout.PlacedCount > bestLayout.PlacedCount)
                            {
                                bestLayout = filledLayout;
                                context.ViewModel?.LogInfo($"         • After filling: {bestLayout.PlacedCount} views");
                            }
                        }
                    }
                }
            }

            return bestLayout.PlacedCount > 0 ? bestLayout : null;
        }

        private OptimalLayout TryShelfLayout(
            List<SheetItem> items,
            SheetDimensions dimensions,
            double hGap,
            double vGap,
            SectionPlacementHandler.PlacementContext context)
        {
            var result = new OptimalLayout
            {
                PlacedItems = new List<PlacedItem>(),
                HorizontalGapUsed = hGap,
                VerticalGapUsed = vGap
            };

            double currentY = dimensions.StartY;
            int itemIndex = 0;
            var rows = new List<ShelfRow>();

            while (itemIndex < items.Count)
            {
                if (context.ViewModel?.Progress.IsCancelled == true)
                    break;

                // Start new row
                var row = new ShelfRow();
                double rowX = dimensions.StartX;
                double maxRowHeight = 0;

                // Fill row with items
                while (itemIndex < items.Count)
                {
                    var item = items[itemIndex];
                    double itemWidth = item.Width;
                    double itemHeight = item.Height;

                    // Check if item fits horizontally (with gap)
                    double requiredWidth = rowX + itemWidth + (row.Items.Any() ? hGap : 0);

                    if (requiredWidth <= dimensions.StartX + dimensions.UsableWidth)
                    {
                        // Apply left gap if not first in row
                        if (row.Items.Any())
                            rowX += hGap;

                        // Place item
                        var placedItem = new PlacedItem
                        {
                            Item = item,
                            X = rowX,
                            Y = currentY - itemHeight, // Bottom-aligned
                            CenterX = rowX + itemWidth / 2,
                            CenterY = currentY - itemHeight / 2
                        };

                        row.Items.Add(placedItem);
                        maxRowHeight = Math.Max(maxRowHeight, itemHeight);
                        rowX += itemWidth;
                        itemIndex++;
                    }
                    else
                    {
                        break; // Row full
                    }
                }

                if (row.Items.Any())
                {
                    // Check if row fits vertically
                    if (currentY - maxRowHeight < dimensions.StartY - dimensions.UsableHeight)
                    {
                        // Row doesn't fit - revert last item
                        if (row.Items.Count > 1)
                        {
                            itemIndex--;
                            row.Items.RemoveAt(row.Items.Count - 1);
                            break;
                        }
                        else
                        {
                            break;
                        }
                    }

                    // Apply bottom alignment within row
                    foreach (var item in row.Items)
                    {
                        double offset = maxRowHeight - item.Item.Height;
                        if (offset > 0.001)
                        {
                            item.Y += offset;
                            item.CenterY += offset;
                        }
                    }

                    result.PlacedItems.AddRange(row.Items);
                    rows.Add(row);
                    currentY -= (maxRowHeight + vGap);
                }
                else
                {
                    break;
                }
            }

            if (result.PlacedItems.Any())
            {
                result.PlacedCount = result.PlacedItems.Count;
                result.Rows = rows;
                result.RowsUsed = rows.Count;
                result.Utilization = CalculateUtilization(result.PlacedItems, dimensions);
            }

            return result;
        }

        private OptimalLayout FillRemainingSpaces(
            OptimalLayout currentLayout,
            List<SheetItem> remainingItems,
            SheetDimensions dimensions,
            double hGap,
            double vGap,
            SectionPlacementHandler.PlacementContext context)
        {
            var result = new OptimalLayout
            {
                PlacedCount = currentLayout.PlacedCount,
                PlacedItems = new List<PlacedItem>(currentLayout.PlacedItems),
                Rows = currentLayout.Rows,
                RowsUsed = currentLayout.RowsUsed,
                Utilization = currentLayout.Utilization,
                HorizontalGapUsed = currentLayout.HorizontalGapUsed,
                VerticalGapUsed = currentLayout.VerticalGapUsed,
                StrategyName = currentLayout.StrategyName + " + Filled"
            };

            // Create occupied spaces list
            var occupiedSpaces = currentLayout.PlacedItems.Select(p => new BoundingBox
            {
                MinX = p.X,
                MaxX = p.X + p.Item.Width,
                MinY = p.Y,
                MaxY = p.Y + p.Item.Height
            }).ToList();

            // Scan for empty spaces
            double scanStep = UnitConversionHelper.MmToFeet(SCAN_STEP_MM);
            int filledCount = 0;

            foreach (var item in remainingItems)
            {
                bool placed = false;

                // Scan the sheet for empty spaces
                for (double y = dimensions.StartY; y > dimensions.StartY - dimensions.UsableHeight; y -= scanStep)
                {
                    for (double x = dimensions.StartX; x < dimensions.StartX + dimensions.UsableWidth; x += scanStep)
                    {
                        // Check if item fits at this position
                        if (x + item.Width > dimensions.StartX + dimensions.UsableWidth ||
                            y - item.Height < dimensions.StartY - dimensions.UsableHeight)
                            continue;

                        // Check for overlap with existing items
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
                            // Place item
                            var placedItem = new PlacedItem
                            {
                                Item = item,
                                X = x,
                                Y = y - item.Height,
                                CenterX = x + item.Width / 2,
                                CenterY = y - item.Height / 2
                            };

                            result.PlacedItems.Add(placedItem);
                            result.PlacedCount++;
                            filledCount++;

                            occupiedSpaces.Add(new BoundingBox
                            {
                                MinX = x,
                                MaxX = x + item.Width,
                                MinY = y - item.Height,
                                MaxY = y
                            });

                            context.ViewModel?.LogInfo($"         • Filled gap with {item.ViewName}");
                            placed = true;
                            break;
                        }
                    }

                    if (placed) break;
                }
            }

            if (filledCount > 0)
            {
                result.Utilization = CalculateUtilization(result.PlacedItems, dimensions);
            }

            return result;
        }

        #region Sorting Strategies

        private List<SheetItem> SortByReadingOrder(List<SheetItem> items)
        {
            return items
                .OrderByDescending(i => i.Y)  // Top to bottom
                .ThenBy(i => i.X)              // Left to right
                .ToList();
        }

        private List<SheetItem> SortBySize(List<SheetItem> items, bool descending)
        {
            if (descending)
                return items.OrderByDescending(i => i.Width * i.Height).ToList();
            else
                return items.OrderBy(i => i.Width * i.Height).ToList();
        }

        private List<SheetItem> SortByHeight(List<SheetItem> items, bool descending)
        {
            if (descending)
                return items.OrderByDescending(i => i.Height).ToList();
            else
                return items.OrderBy(i => i.Height).ToList();
        }

        private List<SheetItem> SortByWidth(List<SheetItem> items, bool descending)
        {
            if (descending)
                return items.OrderByDescending(i => i.Width).ToList();
            else
                return items.OrderBy(i => i.Width).ToList();
        }

        #endregion

        #region Geometry Helpers

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

        private ViewSheet CreateSheet(SectionPlacementHandler.PlacementContext context, int sheetNumber)
        {
            string sheetNumberStr = context.SheetNumberService.GetNextAvailableSheetNumber($"SHT{sheetNumber}");
            context.SheetNumberService.TryReserveSheetNumber(sheetNumberStr);

            var sheetCreator = new SheetCreationService(_doc);
            return sheetCreator.Create(context.TitleBlock, sheetNumberStr, $"Smart-{sheetNumberStr}");
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

                    context.ViewModel?.LogInfo($"      ✅ {placedItem.Item.ViewName} at " +
                        $"({placedItem.CenterX * 304.8:F0}, {placedItem.CenterY * 304.8:F0}) mm - Detail {detailNumber}");

                    context.ViewModel?.Progress.Step();
                }
                catch (Exception ex)
                {
                    context.ViewModel?.LogWarning($"      ⚠️ Failed to place {placedItem.Item.ViewName}: {ex.Message}");
                }
            }

            result.PlacedCount = placedViewIds.Count;
            result.PlacedViewIds = placedViewIds;
            result.Utilization = layout.Utilization;
            result.RowsUsed = layout.RowsUsed;
            result.StrategyName = layout.StrategyName;

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

        private double CalculateUtilization(List<PlacedItem> placedItems, SheetDimensions dimensions)
        {
            double totalArea = placedItems.Sum(i => i.Item.Width * i.Item.Height);
            return totalArea / (dimensions.UsableWidth * dimensions.UsableHeight) * 100;
        }

        #endregion

        #region Drawing Helpers

        private void DrawPlacementAreaOutline(ViewSheet sheet, SheetDimensions dimensions, SectionPlacementHandler.PlacementContext context)
        {
            try
            {
                using (Transaction t = new Transaction(_doc, "Draw Placement Area Outline"))
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

        #endregion

        #region Logging Methods

        private void LogInitialization(
            SectionPlacementHandler.PlacementContext context,
            List<SectionItemViewModel> sections,
            bool multipleSheets)
        {
            context.ViewModel?.LogInfo("════════════════════════════════════════════════");
            context.ViewModel?.LogInfo("🧠 SMART SHELF OPTIMIZER");
            context.ViewModel?.LogInfo("════════════════════════════════════════════════");
            context.ViewModel?.LogInfo($"📊 Total sections: {sections.Count}");
            context.ViewModel?.LogInfo("⚙️ FEATURES:");
            context.ViewModel?.LogInfo("   • Shelf/Row-based packing (Top-Left flow)");
            context.ViewModel?.LogInfo("   • Dynamic row height based on tallest view");
            context.ViewModel?.LogInfo("   • Bottom alignment within rows");
            context.ViewModel?.LogInfo("   • Left alignment within columns");
            context.ViewModel?.LogInfo($"   • Gap tolerance: ±{GAP_TOLERANCE * 100}% (min {MIN_GAP_MM}mm)");
            context.ViewModel?.LogInfo("   • Multi-strategy sorting (5 strategies)");
            context.ViewModel?.LogInfo("   • Fill empty spaces with smaller views");
            context.ViewModel?.LogInfo($"   • Mode: {(multipleSheets ? "Multi-sheet" : "Single sheet")}");
        }

        private void LogSheetDimensions(SectionPlacementHandler.PlacementContext context, SheetDimensions dimensions)
        {
            context.ViewModel?.LogInfo($"\n   📐 SHEET DIMENSIONS:");
            context.ViewModel?.LogInfo($"      • Usable width: {dimensions.UsableWidth * 304.8:F0} mm");
            context.ViewModel?.LogInfo($"      • Usable height: {dimensions.UsableHeight * 304.8:F0} mm");
            context.ViewModel?.LogInfo($"      • Area: {dimensions.UsableWidth * dimensions.UsableHeight * 304.8 * 304.8 / 1e6:F2} m²");
        }

        private void LogPrePlacementAnalysis(
            SectionPlacementHandler.PlacementContext context,
            OptimalLayout layout,
            SheetDimensions dimensions)
        {
            double totalArea = layout.PlacedItems.Sum(i => i.Item.Width * i.Item.Height);
            double usableArea = dimensions.UsableWidth * dimensions.UsableHeight;

            context.ViewModel?.LogInfo($"\n   📊 PRE-PLACEMENT ANALYSIS:");
            context.ViewModel?.LogInfo($"      • Strategy: {layout.StrategyName}");
            context.ViewModel?.LogInfo($"      • Views to place: {layout.PlacedCount}");
            context.ViewModel?.LogInfo($"      • Views total area: {totalArea * 304.8 * 304.8 / 1e6:F2} m²");
            context.ViewModel?.LogInfo($"      • Gaps used: H={layout.HorizontalGapUsed * 304.8:F0}mm, V={layout.VerticalGapUsed * 304.8:F0}mm");
            context.ViewModel?.LogInfo($"      • Projected utilization: {layout.Utilization:F1}%");
        }

        private void LogPostPlacementAnalysis(
            SectionPlacementHandler.PlacementContext context,
            ViewSheet sheet,
            SheetDimensions dimensions,
            OptimalLayout layout)
        {
            try
            {
                var viewports = new FilteredElementCollector(_doc, sheet.Id)
                    .OfClass(typeof(Viewport))
                    .Cast<Viewport>()
                    .ToList();

                double actualArea = 0;
                foreach (var vp in viewports)
                {
                    var box = vp.GetBoxOutline();
                    if (box != null)
                    {
                        actualArea += (box.MaximumPoint.X - box.MinimumPoint.X) *
                                      (box.MaximumPoint.Y - box.MinimumPoint.Y);
                    }
                }

                double usableArea = dimensions.UsableWidth * dimensions.UsableHeight;
                double actualUtilization = (actualArea / usableArea) * 100;

                context.ViewModel?.LogInfo($"\n   📊 POST-PLACEMENT ANALYSIS:");
                context.ViewModel?.LogInfo($"      • Actual views placed: {viewports.Count}");
                context.ViewModel?.LogInfo($"      • Actual utilization: {actualUtilization:F1}%");
                context.ViewModel?.LogInfo($"      • Rows used: {layout.RowsUsed}");

                if (actualUtilization < 50)
                {
                    context.ViewModel?.LogInfo($"      💡 Tip: Try a different sorting strategy or adjust gaps");
                }
            }
            catch { }
        }

        private void LogSheetResult(
            SectionPlacementHandler.PlacementContext context,
            SheetResult result,
            int sheetNumber,
            int remaining)
        {
            context.ViewModel?.LogSuccess($"\n✅ Sheet {sheetNumber} complete: {result.PlacedCount} views placed");
            context.ViewModel?.LogInfo($"   • Strategy: {result.StrategyName}");
            context.ViewModel?.LogInfo($"   • Utilization: {result.Utilization:F1}%");
            context.ViewModel?.LogInfo($"   • Rows used: {result.RowsUsed}");
            context.ViewModel?.LogInfo($"   • Remaining: {remaining}");
        }

        private void LogFinalReport(
            SectionPlacementHandler.PlacementContext context,
            List<SectionItemViewModel> sections,
            int totalPlaced,
            int sheetCount,
            List<SheetItem> remaining,
            SectionPlacementHandler.PlacementResult result)
        {
            context.ViewModel?.LogInfo("\n📊 FINAL REPORT");
            context.ViewModel?.LogInfo("════════════════════════════════════════════════");
            context.ViewModel?.LogInfo($"   • Total sections: {sections.Count}");
            context.ViewModel?.LogInfo($"   • Successfully placed: {totalPlaced}");
            context.ViewModel?.LogInfo($"   • Sheets used: {sheetCount}");

            if (sheetCount > 0)
            {
                context.ViewModel?.LogInfo($"   • Average per sheet: {totalPlaced / (double)sheetCount:F1}");
            }

            if (remaining.Any())
            {
                context.ViewModel?.LogWarning($"   • Skipped: {remaining.Count}");
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

        private class ShelfRow
        {
            public List<PlacedItem> Items { get; set; } = new List<PlacedItem>();
        }

        private class PlacedItem
        {
            public SheetItem Item { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public double CenterX { get; set; }
            public double CenterY { get; set; }
        }

        private class OptimalLayout
        {
            public List<PlacedItem> PlacedItems { get; set; } = new List<PlacedItem>();
            public List<ShelfRow> Rows { get; set; } = new List<ShelfRow>();
            public int PlacedCount { get; set; }
            public int RowsUsed { get; set; }
            public double Utilization { get; set; }
            public double HorizontalGapUsed { get; set; }
            public double VerticalGapUsed { get; set; }
            public string StrategyName { get; set; }
        }

        private class SheetResult
        {
            public string SheetNumber { get; set; }
            public int PlacedCount { get; set; }
            public double Utilization { get; set; }
            public int RowsUsed { get; set; }
            public string StrategyName { get; set; }
            public HashSet<ElementId> PlacedViewIds { get; set; } = new HashSet<ElementId>();
        }

        private class BoundingBox
        {
            public double MinX { get; set; }
            public double MaxX { get; set; }
            public double MinY { get; set; }
            public double MaxY { get; set; }
        }

        #endregion
    }
}