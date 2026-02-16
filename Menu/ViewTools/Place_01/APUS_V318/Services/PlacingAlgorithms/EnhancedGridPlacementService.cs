// File: Services/EnhancedGridPlacementService.cs
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
    /// ENHANCED GRID PLACEMENT SERVICE
    /// Places views in user-defined grid pattern with smart row packing
    /// Features:
    /// ✓ User-defined grid dimensions (1-8 columns, 1-8 rows)
    /// ✓ Tallest-first row packing
    /// ✓ Even distribution across grid cells
    /// ✓ Automatic gap optimization
    /// ✓ Visual grid outline and cell boundaries
    /// </summary>
    public class EnhancedGridPlacementService
    {
        private readonly Document _doc;
        private const double MIN_GAP_MM = 3;

        public EnhancedGridPlacementService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public SectionPlacementHandler.PlacementResult Place(
            SectionPlacementHandler.PlacementContext context,
            List<SectionItemViewModel> sections,
            View referenceView,
            int gridColumns,
            int gridRows)
        {
            var result = new SectionPlacementHandler.PlacementResult();
            result.SheetNumbers = new HashSet<string>();

            if (sections == null || !sections.Any())
            {
                context.ViewModel?.LogWarning("⚠️ No sections to place.");
                result.ErrorMessage = "No sections to place";
                return result;
            }

            int maxViewsPerSheet = gridColumns * gridRows;

            // Log initialization
            LogInitialization(context, sections, gridColumns, gridRows, maxViewsPerSheet);

            try
            {
                var remainingSections = new List<SectionItemViewModel>(sections);
                int sheetCount = 0;
                int totalPlaced = 0;

                // Prepare items with dimensions
                var allItems = PrepareItems(remainingSections, referenceView);

                while (remainingSections.Any() && sheetCount < 50)
                {
                    sheetCount++;
                    context.ViewModel?.LogInfo($"\n{'═',0} SHEET {sheetCount} - {gridColumns}×{gridRows} GRID {'═',60}");

                    var sheetResult = ProcessSheetWithGrid(
                        context,
                        allItems,
                        remainingSections,
                        sheetCount,
                        gridColumns,
                        gridRows,
                        maxViewsPerSheet);

                    if (sheetResult.PlacedCount > 0)
                    {
                        totalPlaced += sheetResult.PlacedCount;
                        result.PlacedCount = totalPlaced;
                        result.SheetNumbers.Add(sheetResult.SheetNumber);

                        // Remove placed views
                        remainingSections = remainingSections
                            .Where(s => !sheetResult.PlacedViewIds.Contains(s.View.Id))
                            .ToList();

                        allItems = allItems
                            .Where(i => !sheetResult.PlacedViewIds.Contains(i.ViewId))
                            .ToList();

                        LogSheetResult(context, sheetResult, sheetCount, remainingSections.Count);
                    }
                    else
                    {
                        context.ViewModel?.LogWarning($"\n⚠️ Could not place views on sheet {sheetCount} - stopping");
                        break;
                    }
                }

                LogFinalReport(context, sections, totalPlaced, sheetCount, remainingSections, result);
                return result;
            }
            catch (Exception ex)
            {
                context.ViewModel?.LogError($"❌ Grid placement failed: {ex.Message}");
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        private SheetResult ProcessSheetWithGrid(
            SectionPlacementHandler.PlacementContext context,
            List<SheetItem> allItems,
            List<SectionItemViewModel> remainingSections,
            int sheetNumber,
            int gridColumns,
            int gridRows,
            int maxViewsPerSheet)
        {
            // Calculate sheet dimensions
            var dimensions = CalculateSheetDimensions(context);
            LogSheetDimensions(context, dimensions, gridColumns, gridRows);

            // Create sheet
            var sheet = CreateSheet(context, sheetNumber);
            if (sheet == null)
                return new SheetResult { PlacedCount = 0 };

            // Draw placement grid outline
            DrawGridOutline(sheet, dimensions, context);

            // Determine how many views to place on this sheet
            int viewsToPlaceCount = Math.Min(allItems.Count, maxViewsPerSheet);

            if (viewsToPlaceCount == 0)
            {
                _doc.Delete(sheet.Id);
                return new SheetResult { PlacedCount = 0 };
            }

            // Calculate optimal grid layout with smart row packing
            var gridLayout = CalculateGridLayout(
                allItems.Take(viewsToPlaceCount).ToList(),
                dimensions,
                context,
                gridColumns,
                gridRows);

            if (gridLayout == null || !gridLayout.PlacedItems.Any())
            {
                _doc.Delete(sheet.Id);
                context.ViewModel?.LogInfo($"   ⚠️ No views fit in grid - sheet deleted");
                return new SheetResult { SheetNumber = sheet.SheetNumber, PlacedCount = 0 };
            }

            // Log grid analysis
            LogGridAnalysis(context, gridLayout, dimensions);

            // Execute placement
            var result = ExecuteGridPlacement(sheet, gridLayout, context);
            result.SheetNumber = sheet.SheetNumber;

            // Draw viewport outlines and grid lines
            DrawViewportOutlines(sheet, context);
            DrawGridLines(sheet, dimensions, gridLayout, context);

            LogPlacementDetails(context, result, gridLayout);
            return result;
        }

        private GridLayout CalculateGridLayout(
            List<SheetItem> items,
            SheetDimensions dimensions,
            SectionPlacementHandler.PlacementContext context,
            int gridColumns,
            int gridRows)
        {
            int totalViews = Math.Min(items.Count, gridColumns * gridRows);
            int columns = Math.Min(gridColumns, totalViews);
            int rows = (int)Math.Ceiling((double)totalViews / columns);

            // Ensure we don't exceed max rows
            if (rows > gridRows)
            {
                rows = gridRows;
                columns = gridColumns;
                totalViews = gridColumns * gridRows;
            }

            context.ViewModel?.LogInfo($"\n   📐 GRID CONFIGURATION:");
            context.ViewModel?.LogInfo($"      • Grid size: {columns} columns × {rows} rows");
            context.ViewModel?.LogInfo($"      • Target views: {totalViews}");

            // Analyze view sizes
            var viewAnalysis = AnalyzeViewSizes(items.Take(totalViews).ToList());

            // Calculate optimal gaps
            double optimalHGap = CalculateOptimalHorizontalGap(viewAnalysis, dimensions, columns, context);
            double optimalVGap = CalculateOptimalVerticalGap(viewAnalysis, dimensions, rows, context);

            context.ViewModel?.LogInfo($"\n   📏 GAP OPTIMIZATION:");
            context.ViewModel?.LogInfo($"      • Horizontal gap: {optimalHGap * 304.8:F1} mm");
            context.ViewModel?.LogInfo($"      • Vertical gap: {optimalVGap * 304.8:F1} mm");

            // Calculate column widths based on view sizes
            var columnWidths = CalculateColumnWidths(viewAnalysis, dimensions.UsableWidth, columns, optimalHGap);

            // Calculate row heights based on tallest views in each row
            var rowHeights = CalculateRowHeightsWithSmartPacking(
                items.Take(totalViews).ToList(),
                dimensions.UsableHeight,
                rows,
                optimalVGap,
                columns);

            // Distribute views to grid cells with smart packing
            var placedItems = DistributeViewsToGridCells(
                items.Take(totalViews).ToList(),
                dimensions,
                columnWidths,
                rowHeights,
                optimalHGap,
                optimalVGap,
                columns,
                rows);

            if (!placedItems.Any())
                return null;

            return new GridLayout
            {
                PlacedItems = placedItems,
                Columns = columns,
                Rows = rows,
                ColumnWidths = columnWidths,
                RowHeights = rowHeights,
                HorizontalGap = optimalHGap,
                VerticalGap = optimalVGap,
                Utilization = CalculateUtilization(placedItems, dimensions)
            };
        }

        private ViewSizeAnalysis AnalyzeViewSizes(List<SheetItem> items)
        {
            var analysis = new ViewSizeAnalysis
            {
                ViewCount = items.Count,
                Widths = items.Select(i => i.Width).ToList(),
                Heights = items.Select(i => i.Height).ToList()
            };

            analysis.MaxWidth = analysis.Widths.Max();
            analysis.MaxHeight = analysis.Heights.Max();
            analysis.MinWidth = analysis.Widths.Min();
            analysis.MinHeight = analysis.Heights.Min();
            analysis.AvgWidth = analysis.Widths.Average();
            analysis.AvgHeight = analysis.Heights.Average();

            return analysis;
        }

        private double CalculateOptimalHorizontalGap(
            ViewSizeAnalysis analysis,
            SheetDimensions dimensions,
            int columns,
            SectionPlacementHandler.PlacementContext context)
        {
            double totalViewWidth = analysis.AvgWidth * columns;
            double availableWidth = dimensions.UsableWidth;

            // Calculate gap needed to fill width
            double neededGap = columns > 1 ? (availableWidth - totalViewWidth) / (columns - 1) : 0;

            // Constrain gap within reasonable limits
            double minGap = UnitConversionHelper.MmToFeet(MIN_GAP_MM);
            double maxGap = UnitConversionHelper.MmToFeet(50); // Max 50mm gap

            double optimalGap = Math.Max(minGap, Math.Min(maxGap, neededGap));

            // Allow adjustment within ±10% of user preference
            double userGap = UnitConversionHelper.MmToFeet(context.HorizontalGapMm);
            double minUserGap = userGap * 0.9;
            double maxUserGap = userGap * 1.1;

            if (optimalGap >= minUserGap && optimalGap <= maxUserGap)
                return optimalGap;
            else if (optimalGap < minUserGap)
                return minUserGap;
            else
                return maxUserGap;
        }

        private double CalculateOptimalVerticalGap(
            ViewSizeAnalysis analysis,
            SheetDimensions dimensions,
            int rows,
            SectionPlacementHandler.PlacementContext context)
        {
            double totalViewHeight = analysis.AvgHeight * rows;
            double availableHeight = dimensions.UsableHeight;

            // Calculate gap needed to fill height
            double neededGap = rows > 1 ? (availableHeight - totalViewHeight) / (rows - 1) : 0;

            // Constrain gap within reasonable limits
            double minGap = UnitConversionHelper.MmToFeet(MIN_GAP_MM);
            double maxGap = UnitConversionHelper.MmToFeet(50); // Max 50mm gap

            double optimalGap = Math.Max(minGap, Math.Min(maxGap, neededGap));

            // Allow adjustment within ±10% of user preference
            double userGap = UnitConversionHelper.MmToFeet(context.VerticalGapMm);
            double minUserGap = userGap * 0.9;
            double maxUserGap = userGap * 1.1;

            if (optimalGap >= minUserGap && optimalGap <= maxUserGap)
                return optimalGap;
            else if (optimalGap < minUserGap)
                return minUserGap;
            else
                return maxUserGap;
        }

        private List<double> CalculateColumnWidths(
            ViewSizeAnalysis analysis,
            double totalWidth,
            int columns,
            double hGap)
        {
            var columnWidths = new List<double>();
            double totalGapsWidth = hGap * Math.Max(0, columns - 1);
            double availableWidthForViews = totalWidth - totalGapsWidth;

            // Distribute width proportionally based on view sizes
            if (analysis.ViewCount >= columns)
            {
                var sortedWidths = analysis.Widths.OrderByDescending(w => w).ToList();
                double totalWidths = sortedWidths.Sum();

                for (int i = 0; i < columns; i++)
                {
                    if (i < sortedWidths.Count)
                    {
                        double proportion = sortedWidths[i] / totalWidths;
                        columnWidths.Add(availableWidthForViews * proportion);
                    }
                    else
                    {
                        columnWidths.Add(availableWidthForViews / columns);
                    }
                }
            }
            else
            {
                // Equal distribution if not enough views
                for (int i = 0; i < columns; i++)
                {
                    columnWidths.Add(availableWidthForViews / columns);
                }
            }

            return columnWidths;
        }

        private List<double> CalculateRowHeightsWithSmartPacking(
            List<SheetItem> items,
            double totalHeight,
            int rows,
            double vGap,
            int columns)
        {
            var rowHeights = new List<double>();
            double totalGapsHeight = vGap * Math.Max(0, rows - 1);
            double availableHeightForViews = totalHeight - totalGapsHeight;

            // Group items into rows (tallest first packing)
            var itemsCopy = new List<SheetItem>(items);
            var rowsItems = new List<List<SheetItem>>();

            for (int r = 0; r < rows; r++)
            {
                if (!itemsCopy.Any()) break;

                var rowItems = new List<SheetItem>();
                int itemsForThisRow = Math.Min(columns, itemsCopy.Count);

                // Take items for this row (tallest first)
                for (int i = 0; i < itemsForThisRow; i++)
                {
                    if (itemsCopy.Any())
                    {
                        var tallest = itemsCopy.OrderByDescending(x => x.Height).First();
                        rowItems.Add(tallest);
                        itemsCopy.Remove(tallest);
                    }
                }

                rowsItems.Add(rowItems);
            }

            // Calculate height for each row based on tallest item in that row
            double totalRowHeights = 0;
            foreach (var rowItems in rowsItems)
            {
                double maxHeight = rowItems.Any() ? rowItems.Max(i => i.Height) : 0;
                rowHeights.Add(maxHeight);
                totalRowHeights += maxHeight;
            }

            // Scale heights to fit available space
            if (totalRowHeights > 0)
            {
                double scaleFactor = availableHeightForViews / totalRowHeights;
                for (int i = 0; i < rowHeights.Count; i++)
                {
                    rowHeights[i] *= scaleFactor;
                }
            }

            // Fill remaining rows if needed
            while (rowHeights.Count < rows)
            {
                rowHeights.Add(availableHeightForViews / rows);
            }

            return rowHeights;
        }

        private List<PlacedItem> DistributeViewsToGridCells(
            List<SheetItem> items,
            SheetDimensions dimensions,
            List<double> columnWidths,
            List<double> rowHeights,
            double hGap,
            double vGap,
            int columns,
            int rows)
        {
            var placedItems = new List<PlacedItem>();
            var itemsCopy = new List<SheetItem>(items);

            double startX = dimensions.StartX;
            double startY = dimensions.StartY;

            for (int r = 0; r < rows; r++)
            {
                if (!itemsCopy.Any()) break;

                double rowY = startY - (r * (rowHeights[r] + vGap));
                double rowBottomY = rowY - rowHeights[r];

                // Get items for this row (tallest first)
                var rowItems = new List<SheetItem>();
                int itemsForThisRow = Math.Min(columns, itemsCopy.Count);

                for (int i = 0; i < itemsForThisRow; i++)
                {
                    if (itemsCopy.Any())
                    {
                        var tallest = itemsCopy.OrderByDescending(x => x.Height).First();
                        rowItems.Add(tallest);
                        itemsCopy.Remove(tallest);
                    }
                }

                // Place items in this row
                for (int c = 0; c < rowItems.Count; c++)
                {
                    double cellX = startX + (c * (columnWidths[c] + hGap));
                    var item = rowItems[c];

                    // Center the view in its cell
                    double centerX = cellX + columnWidths[c] / 2;
                    double centerY = rowBottomY + rowHeights[r] / 2;

                    placedItems.Add(new PlacedItem
                    {
                        Item = item,
                        X = cellX,
                        Y = rowBottomY,
                        CenterX = centerX,
                        CenterY = centerY
                    });
                }
            }

            return placedItems;
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

        private SheetResult ExecuteGridPlacement(
            ViewSheet sheet,
            GridLayout layout,
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
                catch (Exception ex)
                {
                    context.ViewModel?.LogWarning($"   ⚠️ Failed to place {placedItem.Item.ViewName}: {ex.Message}");
                }
            }

            result.PlacedCount = placedViewIds.Count;
            result.PlacedViewIds = placedViewIds;
            result.Utilization = layout.Utilization;
            result.GridColumns = layout.Columns;
            result.GridRows = layout.Rows;

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

        private void DrawGridLines(ViewSheet sheet, SheetDimensions dimensions, GridLayout layout, SectionPlacementHandler.PlacementContext context)
        {
            try
            {
                using (Transaction t = new Transaction(_doc, "Draw Grid Lines"))
                {
                    t.Start();

                    // Draw vertical grid lines
                    double x = dimensions.StartX;
                    for (int c = 0; c <= layout.Columns; c++)
                    {
                        var start = new XYZ(x, dimensions.StartY, 0);
                        var end = new XYZ(x, dimensions.StartY - dimensions.UsableHeight, 0);
                        var line = Line.CreateBound(start, end);
                        _doc.Create.NewDetailCurve(sheet, line);

                        if (c < layout.Columns)
                        {
                            x += layout.ColumnWidths[c] + layout.HorizontalGap;
                        }
                    }

                    // Draw horizontal grid lines
                    double y = dimensions.StartY;
                    for (int r = 0; r <= layout.Rows; r++)
                    {
                        var start = new XYZ(dimensions.StartX, y, 0);
                        var end = new XYZ(dimensions.StartX + dimensions.UsableWidth, y, 0);
                        var line = Line.CreateBound(start, end);
                        _doc.Create.NewDetailCurve(sheet, line);

                        if (r < layout.Rows)
                        {
                            y -= layout.RowHeights[r] + layout.VerticalGap;
                        }
                    }

                    t.Commit();
                }

                context.ViewModel?.LogInfo($"      📏 Grid lines drawn for {layout.Columns}×{layout.Rows} grid");
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

        private double CalculateUtilization(List<PlacedItem> placedItems, SheetDimensions dimensions)
        {
            double totalArea = placedItems.Sum(i => i.Item.Width * i.Item.Height);
            return totalArea / (dimensions.UsableWidth * dimensions.UsableHeight) * 100;
        }

        #region Logging Methods

        private void LogInitialization(
            SectionPlacementHandler.PlacementContext context,
            List<SectionItemViewModel> sections,
            int gridColumns,
            int gridRows,
            int maxViewsPerSheet)
        {
            context.ViewModel?.LogInfo("════════════════════════════════════════════════");
            context.ViewModel?.LogInfo("📚 ENHANCED GRID PLACEMENT SERVICE");
            context.ViewModel?.LogInfo("════════════════════════════════════════════════");
            context.ViewModel?.LogInfo($"📊 Total sections to place: {sections.Count}");
            context.ViewModel?.LogInfo($"📐 Grid configuration: {gridColumns}×{gridRows}");
            context.ViewModel?.LogInfo($"   • Max views per sheet: {maxViewsPerSheet}");
            context.ViewModel?.LogInfo($"   • Smart row packing: Enabled");
            context.ViewModel?.LogInfo($"   • Gap optimization: ±10% tolerance");
        }

        private void LogSheetDimensions(
            SectionPlacementHandler.PlacementContext context,
            SheetDimensions dimensions,
            int gridColumns,
            int gridRows)
        {
            context.ViewModel?.LogInfo($"\n   📐 SHEET DIMENSIONS:");
            context.ViewModel?.LogInfo($"      • Usable width: {dimensions.UsableWidth * 304.8:F0} mm");
            context.ViewModel?.LogInfo($"      • Usable height: {dimensions.UsableHeight * 304.8:F0} mm");
            context.ViewModel?.LogInfo($"      • Grid cells: {gridColumns}×{gridRows}");
        }

        private void LogGridAnalysis(
            SectionPlacementHandler.PlacementContext context,
            GridLayout layout,
            SheetDimensions dimensions)
        {
            double cellWidth = dimensions.UsableWidth / layout.Columns;
            double cellHeight = dimensions.UsableHeight / layout.Rows;

            context.ViewModel?.LogInfo($"\n   📊 GRID ANALYSIS:");
            context.ViewModel?.LogInfo($"      • Average cell size: {cellWidth * 304.8:F0}×{cellHeight * 304.8:F0} mm");
            context.ViewModel?.LogInfo($"      • Views to place: {layout.PlacedItems.Count}");
            context.ViewModel?.LogInfo($"      • Projected utilization: {layout.Utilization:F1}%");
        }

        private void LogSheetResult(
            SectionPlacementHandler.PlacementContext context,
            SheetResult result,
            int sheetNumber,
            int remaining)
        {
            context.ViewModel?.LogSuccess($"\n✅ Sheet {sheetNumber} complete: {result.PlacedCount} views placed");
            context.ViewModel?.LogInfo($"   • Utilization: {result.Utilization:F1}%");
            context.ViewModel?.LogInfo($"   • Remaining: {remaining}");
        }

        private void LogPlacementDetails(
            SectionPlacementHandler.PlacementContext context,
            SheetResult result,
            GridLayout layout)
        {
            context.ViewModel?.LogInfo($"   📊 Final layout:");
            context.ViewModel?.LogInfo($"      • Views placed: {result.PlacedCount}");
            context.ViewModel?.LogInfo($"      • Utilization: {result.Utilization:F1}%");
            context.ViewModel?.LogInfo($"      • Grid: {layout.Columns}×{layout.Rows}");
        }

        private void LogFinalReport(
            SectionPlacementHandler.PlacementContext context,
            List<SectionItemViewModel> sections,
            int totalPlaced,
            int sheetCount,
            List<SectionItemViewModel> remaining,
            SectionPlacementHandler.PlacementResult result)
        {
            context.ViewModel?.LogInfo("\n📊 FINAL GRID PLACEMENT REPORT");
            context.ViewModel?.LogInfo("════════════════════════════════════════════════");
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

        private class ViewSizeAnalysis
        {
            public int ViewCount { get; set; }
            public List<double> Widths { get; set; }
            public List<double> Heights { get; set; }
            public double MaxWidth { get; set; }
            public double MaxHeight { get; set; }
            public double MinWidth { get; set; }
            public double MinHeight { get; set; }
            public double AvgWidth { get; set; }
            public double AvgHeight { get; set; }
        }

        private class GridLayout
        {
            public List<PlacedItem> PlacedItems { get; set; } = new List<PlacedItem>();
            public int Columns { get; set; }
            public int Rows { get; set; }
            public List<double> ColumnWidths { get; set; } = new List<double>();
            public List<double> RowHeights { get; set; } = new List<double>();
            public double HorizontalGap { get; set; }
            public double VerticalGap { get; set; }
            public double Utilization { get; set; }
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
            public double Utilization { get; set; }
            public int GridColumns { get; set; }
            public int GridRows { get; set; }
            public HashSet<ElementId> PlacedViewIds { get; set; } = new HashSet<ElementId>();
        }

        #endregion
    }
}