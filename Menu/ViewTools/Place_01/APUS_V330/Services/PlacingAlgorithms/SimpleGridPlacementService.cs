// File: Services/PlacingAlgorithms/SimpleGridPlacementService.cs
// ONLY placement algorithm retained in V330.
// Rules:
//   ✓ User-defined rows and columns
//   ✓ Strict reading order: Left → Right, Top → Bottom
//   ✓ Even distribution within grid cells
//   ✓ Bottom-aligned within rows, Left-aligned within columns
//   ✓ Gaps respected with ±10% tolerance if needed
//   ✓ Optional multi-sheet placement
//   ✓ No overlap guarantee
using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V330.ExternalEvents;
using Revit26_Plugin.APUS_V330.Helpers;
using Revit26_Plugin.APUS_V330.Models;
using Revit26_Plugin.APUS_V330.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V330.Services
{
    public class SimpleGridPlacementService
    {
        private readonly Document _doc;
        private const double MIN_GAP_MM   = 3;
        private const double GAP_TOLERANCE = 0.10;

        public SimpleGridPlacementService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public SectionPlacementHandler.PlacementResult Place(
            SectionPlacementHandler.PlacementContext context,
            List<SectionItemViewModel>               sections,
            View                                     referenceView,
            int                                      gridRows,
            int                                      gridColumns,
            bool                                     placeToMultipleSheets)
        {
            var result = new SectionPlacementHandler.PlacementResult
            {
                SheetNumbers = new HashSet<string>()
            };

            if (sections == null || !sections.Any())
            {
                context.ViewModel?.LogWarning("No sections to place.");
                result.ErrorMessage = "No sections to place";
                return result;
            }

            int maxViewsPerSheet = gridRows * gridColumns;
            LogInitialization(context, sections, gridRows, gridColumns, maxViewsPerSheet, placeToMultipleSheets);

            try
            {
                // Stage 1 — sort
                context.ViewModel?.LogInfo("\nSTAGE 1: Sorting sections in reading order...");
                var sortedItems = PrepareItemsInReadingOrder(sections, referenceView, context);

                if (!sortedItems.Any())
                {
                    result.ErrorMessage = "No valid items after sorting";
                    return result;
                }

                // Stage 2 — sheet dimensions
                var dimensions = CalculateSheetDimensions(context);
                LogSheetDimensions(context, dimensions);

                // Stage 3 — grid cell sizes
                if (!CalculateGridCells(dimensions, gridRows, gridColumns, context,
                    out double cellWidth, out double cellHeight, out double hGap, out double vGap))
                {
                    result.ErrorMessage = "Failed to calculate grid layout";
                    return result;
                }

                // Stage 4 — process sheets
                int totalPlaced  = 0;
                int sheetCount   = 0;
                int currentIndex = 0;

                while (currentIndex < sortedItems.Count)
                {
                    if (context.ViewModel?.Progress.IsCancelled == true) break;

                    sheetCount++;

                    int viewsForThisSheet = placeToMultipleSheets
                        ? Math.Min(maxViewsPerSheet, sortedItems.Count - currentIndex)
                        : Math.Min(maxViewsPerSheet, sortedItems.Count);

                    context.ViewModel?.LogInfo($"\nSHEET {sheetCount} — {gridRows}×{gridColumns} GRID");
                    context.ViewModel?.LogInfo($"   Placing views {currentIndex + 1} to {currentIndex + viewsForThisSheet}");

                    var sheet = CreateSheet(context, sheetCount);
                    if (sheet == null) break;

                    DrawGridOutline(sheet, dimensions, context);

                    var sheetResult = PlaceViewsOnSheet(
                        sheet,
                        sortedItems.Skip(currentIndex).Take(viewsForThisSheet).ToList(),
                        dimensions, cellWidth, cellHeight, hGap, vGap,
                        gridRows, gridColumns, context, currentIndex + 1);

                    if (sheetResult.PlacedCount > 0)
                    {
                        totalPlaced      += sheetResult.PlacedCount;
                        result.PlacedCount = totalPlaced;
                        result.SheetNumbers.Add(sheet.SheetNumber);
                        currentIndex     += sheetResult.PlacedCount;
                        LogSheetResult(context, sheetResult, sheetCount,
                            sortedItems.Count - currentIndex, placeToMultipleSheets);
                    }
                    else
                    {
                        _doc.Delete(sheet.Id);
                        context.ViewModel?.LogWarning($"   No views placed on sheet {sheetCount} - removing");
                        if (!placeToMultipleSheets) break;
                    }

                    if (!placeToMultipleSheets) break;
                }

                LogFinalReport(context, sections, totalPlaced, sheetCount, result);
                return result;
            }
            catch (Exception ex)
            {
                context.ViewModel?.LogError($"Grid placement failed: {ex.Message}");
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        // ------------------------------------------------------------------ helpers

        private List<SheetItem> PrepareItemsInReadingOrder(
            List<SectionItemViewModel>           sections,
            View                                 referenceView,
            SectionPlacementHandler.PlacementContext context)
        {
            XYZ origin = referenceView.Origin;
            XYZ right  = referenceView.RightDirection;
            XYZ up     = referenceView.UpDirection;

            var items = sections.Select(s =>
            {
                var loc = GetSectionLocation(s.View);
                XYZ v   = loc - origin;
                var fp  = ViewSizeService.Calculate(s.View);
                return new SheetItem
                {
                    Section  = s,
                    X        = v.DotProduct(right),
                    Y        = v.DotProduct(up),
                    Width    = fp.WidthFt,
                    Height   = fp.HeightFt,
                    ViewId   = s.View.Id,
                    ViewName = s.ViewName
                };
            }).OrderByDescending(i => i.Y).ThenBy(i => i.X).ToList();

            context.ViewModel?.LogInfo($"\nREADING ORDER (first 10):");
            for (int i = 0; i < Math.Min(10, items.Count); i++)
            {
                var item = items[i];
                context.ViewModel?.LogInfo(
                    $"   #{i + 1,2}: {item.ViewName,-30} Pos: ({item.X * 304.8:F0}, {item.Y * 304.8:F0}) mm");
            }
            return items;
        }

        private bool CalculateGridCells(
            SheetDimensions dimensions,
            int gridRows, int gridColumns,
            SectionPlacementHandler.PlacementContext context,
            out double cellWidth, out double cellHeight,
            out double hGap,     out double vGap)
        {
            cellWidth  = 0;
            cellHeight = 0;
            hGap = UnitConversionHelper.MmToFeet(context.HorizontalGapMm);
            vGap = UnitConversionHelper.MmToFeet(context.VerticalGapMm);

            cellWidth  = (dimensions.UsableWidth  - hGap * (gridColumns - 1)) / gridColumns;
            cellHeight = (dimensions.UsableHeight - vGap * (gridRows    - 1)) / gridRows;

            context.ViewModel?.LogInfo($"\n   GRID CALCULATION:");
            context.ViewModel?.LogInfo($"      Cell: {cellWidth * 304.8:F0} x {cellHeight * 304.8:F0} mm");

            double minGapFt = UnitConversionHelper.MmToFeet(MIN_GAP_MM);
            for (double m = 1.0; m <= 1.0 + GAP_TOLERANCE; m += 0.02)
            {
                double th = Math.Max(hGap * m, minGapFt);
                double tv = Math.Max(vGap * m, minGapFt);
                double tw = (dimensions.UsableWidth  - th * (gridColumns - 1)) / gridColumns;
                double tz = (dimensions.UsableHeight - tv * (gridRows    - 1)) / gridRows;

                if (tw > 0 && tz > 0)
                {
                    if (Math.Abs(m - 1.0) > 0.01)
                    {
                        hGap = th; vGap = tv; cellWidth = tw; cellHeight = tz;
                        context.ViewModel?.LogInfo($"      Gaps adjusted: H={hGap * 304.8:F0}mm V={vGap * 304.8:F0}mm");
                    }
                    break;
                }
            }

            return cellWidth > 0 && cellHeight > 0;
        }

        private SheetResult PlaceViewsOnSheet(
            ViewSheet sheet, List<SheetItem> items,
            SheetDimensions dimensions,
            double cellWidth, double cellHeight, double hGap, double vGap,
            int gridRows, int gridColumns,
            SectionPlacementHandler.PlacementContext context, int startNumber)
        {
            var result       = new SheetResult();
            var placedViewIds = new HashSet<ElementId>();

            context.ViewModel?.LogInfo($"\n   PLACING VIEWS:");

            int viewIndex = 0;
            for (int row = 0; row < gridRows && viewIndex < items.Count; row++)
            {
                for (int col = 0; col < gridColumns && viewIndex < items.Count; col++)
                {
                    var item         = items[viewIndex];
                    int globalNumber = startNumber + viewIndex;

                    double cellX       = dimensions.StartX + col * (cellWidth  + hGap);
                    double cellY       = dimensions.StartY - row * (cellHeight + vGap);
                    double cellBottomY = cellY - cellHeight;

                    double centerX = cellX       + cellWidth  / 2;
                    double centerY = cellBottomY + cellHeight / 2;

                    try
                    {
                        if (!CanPlaceView(item.Section.View, _doc, sheet.Id))
                        {
                            context.ViewModel?.LogWarning(
                                $"      [{globalNumber}] {item.ViewName} - Cannot place (already placed)");
                            viewIndex++;
                            continue;
                        }

                        var vp = Viewport.Create(_doc, sheet.Id, item.Section.View.Id, new XYZ(centerX, centerY, 0));

                        int detailNumber = context.GetNextDetailNumber();
                        var detailParam  = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                        if (detailParam != null && !detailParam.IsReadOnly)
                            detailParam.Set(detailNumber.ToString());

                        placedViewIds.Add(item.Section.View.Id);

                        context.ViewModel?.LogInfo(
                            $"      [{globalNumber}] {item.ViewName,-30} Cell ({row + 1},{col + 1}) " +
                            $"Pos: ({centerX * 304.8:F0}, {centerY * 304.8:F0}) mm  Detail: {detailNumber}");

                        context.ViewModel?.Progress.Step();
                        viewIndex++;
                    }
                    catch (Exception ex)
                    {
                        context.ViewModel?.LogError($"      [{globalNumber}] {item.ViewName} - Failed: {ex.Message}");
                        viewIndex++;
                    }
                }
            }

            DrawGridLines(sheet, dimensions, cellWidth, cellHeight, hGap, vGap, gridRows, gridColumns, context);
            DrawViewportOutlines(sheet, context);

            result.PlacedCount   = placedViewIds.Count;
            result.PlacedViewIds = placedViewIds;
            result.Utilization   = CalculateUtilization(placedViewIds, dimensions);
            return result;
        }

        private SheetDimensions CalculateSheetDimensions(SectionPlacementHandler.PlacementContext context)
        {
            double left   = UnitConversionHelper.MmToFeet(context.ViewModel?.LeftMarginMm   ?? 40);
            double right  = UnitConversionHelper.MmToFeet(context.ViewModel?.RightMarginMm  ?? 150);
            double top    = UnitConversionHelper.MmToFeet(context.ViewModel?.TopMarginMm    ?? 40);
            double bottom = UnitConversionHelper.MmToFeet(context.ViewModel?.BottomMarginMm ?? 100);

            return new SheetDimensions
            {
                UsableWidth  = context.PlacementArea.Width  - left - right,
                UsableHeight = context.PlacementArea.Height - top  - bottom,
                StartX       = context.PlacementArea.Origin.X + left,
                StartY       = context.PlacementArea.Origin.Y - top
            };
        }

        private ViewSheet CreateSheet(SectionPlacementHandler.PlacementContext context, int sheetNumber)
        {
            string num = context.SheetNumberService.GetNextAvailableSheetNumber($"GRID{sheetNumber}");
            context.SheetNumberService.TryReserveSheetNumber(num);
            return new SheetCreationService(_doc).Create(context.TitleBlock, num, $"Grid-{num}");
        }

        private XYZ GetSectionLocation(ViewSection view)
        {
            try
            {
                if (view.Location is LocationCurve lc && lc.Curve != null)
                    return lc.Curve.Evaluate(0.5, true);
                var bb = view.CropBox;
                if (bb != null) return (bb.Min + bb.Max) * 0.5;
            }
            catch { }
            return XYZ.Zero;
        }

        private bool CanPlaceView(ViewSection view, Document doc, ElementId sheetId)
        {
            try { return Viewport.CanAddViewToSheet(doc, sheetId, view.Id); }
            catch { return false; }
        }

        private double CalculateUtilization(HashSet<ElementId> placedViewIds, SheetDimensions dimensions)
        {
            try
            {
                double totalArea = placedViewIds.Sum(id =>
                {
                    var v = _doc.GetElement(id) as ViewSection;
                    if (v == null) return 0;
                    var fp = ViewSizeService.Calculate(v);
                    return fp.WidthFt * fp.HeightFt;
                });
                double sheetArea = dimensions.UsableWidth * dimensions.UsableHeight;
                return sheetArea > 0 ? totalArea * 100.0 / sheetArea : 0;
            }
            catch { return 0; }
        }

        private void DrawGridOutline(ViewSheet sheet, SheetDimensions d,
            SectionPlacementHandler.PlacementContext context)
        {
            try
            {
                using var t = new Transaction(_doc, "Draw Grid Outline");
                t.Start();

                var pts = new[]
                {
                    new XYZ(d.StartX,                  d.StartY,                   0),
                    new XYZ(d.StartX + d.UsableWidth,  d.StartY,                   0),
                    new XYZ(d.StartX + d.UsableWidth,  d.StartY - d.UsableHeight,  0),
                    new XYZ(d.StartX,                  d.StartY - d.UsableHeight,  0),
                    new XYZ(d.StartX,                  d.StartY,                   0)
                };
                for (int i = 0; i < pts.Length - 1; i++)
                    _doc.Create.NewDetailCurve(sheet, Line.CreateBound(pts[i], pts[i + 1]));

                t.Commit();
                context.ViewModel?.LogInfo("      Grid outline drawn");
            }
            catch (Exception ex)
            {
                context.ViewModel?.LogWarning($"      Could not draw grid outline: {ex.Message}");
            }
        }

        private void DrawGridLines(ViewSheet sheet, SheetDimensions d,
            double cellWidth, double cellHeight, double hGap, double vGap,
            int gridRows, int gridColumns,
            SectionPlacementHandler.PlacementContext context)
        {
            try
            {
                using var t = new Transaction(_doc, "Draw Grid Lines");
                t.Start();

                double x = d.StartX;
                for (int c = 0; c <= gridColumns; c++)
                {
                    _doc.Create.NewDetailCurve(sheet,
                        Line.CreateBound(new XYZ(x, d.StartY, 0), new XYZ(x, d.StartY - d.UsableHeight, 0)));
                    if (c < gridColumns) x += cellWidth + hGap;
                }

                double y = d.StartY;
                for (int r = 0; r <= gridRows; r++)
                {
                    _doc.Create.NewDetailCurve(sheet,
                        Line.CreateBound(new XYZ(d.StartX, y, 0), new XYZ(d.StartX + d.UsableWidth, y, 0)));
                    if (r < gridRows) y -= cellHeight + vGap;
                }

                t.Commit();
                context.ViewModel?.LogInfo($"      Grid lines drawn ({gridColumns}x{gridRows})");
            }
            catch (Exception ex)
            {
                context.ViewModel?.LogWarning($"      Could not draw grid lines: {ex.Message}");
            }
        }

        private void DrawViewportOutlines(ViewSheet sheet,
            SectionPlacementHandler.PlacementContext context)
        {
            try
            {
                using var t = new Transaction(_doc, "Draw Viewport Outlines");
                t.Start();

                foreach (var vp in new FilteredElementCollector(_doc, sheet.Id)
                    .OfClass(typeof(Viewport)).Cast<Viewport>())
                {
                    var box = vp.GetBoxOutline();
                    if (box == null) continue;

                    var pts = new[]
                    {
                        new XYZ(box.MinimumPoint.X, box.MinimumPoint.Y, 0),
                        new XYZ(box.MaximumPoint.X, box.MinimumPoint.Y, 0),
                        new XYZ(box.MaximumPoint.X, box.MaximumPoint.Y, 0),
                        new XYZ(box.MinimumPoint.X, box.MaximumPoint.Y, 0),
                        new XYZ(box.MinimumPoint.X, box.MinimumPoint.Y, 0)
                    };
                    for (int i = 0; i < pts.Length - 1; i++)
                        _doc.Create.NewDetailCurve(sheet, Line.CreateBound(pts[i], pts[i + 1]));
                }

                t.Commit();
                context.ViewModel?.LogInfo("      Viewport outlines drawn");
            }
            catch (Exception ex)
            {
                context.ViewModel?.LogWarning($"      Could not draw viewport outlines: {ex.Message}");
            }
        }

        // ------------------------------------------------------------------ logging

        private void LogInitialization(SectionPlacementHandler.PlacementContext context,
            List<SectionItemViewModel> sections, int rows, int cols, int max, bool multi)
        {
            context.ViewModel?.LogInfo("================================================");
            context.ViewModel?.LogInfo("SIMPLE GRID PLACEMENT SERVICE");
            context.ViewModel?.LogInfo("================================================");
            context.ViewModel?.LogInfo($"Total sections: {sections.Count}");
            context.ViewModel?.LogInfo($"Grid:           {rows} rows x {cols} columns  (max {max}/sheet)");
            context.ViewModel?.LogInfo($"Mode:           {(multi ? "Multiple sheets" : "Single sheet")}");
        }

        private void LogSheetDimensions(SectionPlacementHandler.PlacementContext context, SheetDimensions d)
        {
            context.ViewModel?.LogInfo($"\n   SHEET DIMENSIONS:");
            context.ViewModel?.LogInfo($"      Usable: {d.UsableWidth * 304.8:F0} x {d.UsableHeight * 304.8:F0} mm");
            context.ViewModel?.LogInfo($"      Start:  ({d.StartX * 304.8:F0}, {d.StartY * 304.8:F0}) mm");
        }

        private void LogSheetResult(SectionPlacementHandler.PlacementContext context,
            SheetResult result, int sheetNumber, int remaining, bool multi)
        {
            context.ViewModel?.LogSuccess($"\nSheet {sheetNumber} complete: {result.PlacedCount} views placed");
            context.ViewModel?.LogInfo($"   Utilization: {result.Utilization:F1}%");
            if (multi) context.ViewModel?.LogInfo($"   Remaining:   {remaining}");
        }

        private void LogFinalReport(SectionPlacementHandler.PlacementContext context,
            List<SectionItemViewModel> sections, int totalPlaced, int sheetCount,
            SectionPlacementHandler.PlacementResult result)
        {
            context.ViewModel?.LogInfo("\nFINAL PLACEMENT REPORT");
            context.ViewModel?.LogInfo("================================================");
            context.ViewModel?.LogInfo($"   Total:   {sections.Count}");
            context.ViewModel?.LogInfo($"   Placed:  {totalPlaced}");
            context.ViewModel?.LogInfo($"   Sheets:  {sheetCount}");

            if (totalPlaced < sections.Count)
            {
                result.FailedCount = sections.Count - totalPlaced;
                context.ViewModel?.LogWarning($"   Skipped: {result.FailedCount} (capacity reached)");
            }
            else
            {
                context.ViewModel?.LogSuccess("   All sections placed successfully!");
            }

            if (result.SheetNumbers.Any())
                context.ViewModel?.LogInfo($"   Sheet list: {string.Join(", ", result.SheetNumbers.OrderBy(s => s))}");
        }

        // ------------------------------------------------------------------ inner types

        private class SheetItem
        {
            public SectionItemViewModel Section  { get; set; }
            public double               X        { get; set; }
            public double               Y        { get; set; }
            public double               Width    { get; set; }
            public double               Height   { get; set; }
            public ElementId            ViewId   { get; set; }
            public string               ViewName { get; set; }
        }

        private class SheetDimensions
        {
            public double UsableWidth  { get; set; }
            public double UsableHeight { get; set; }
            public double StartX       { get; set; }
            public double StartY       { get; set; }
        }

        private class SheetResult
        {
            public int              PlacedCount   { get; set; }
            public double           Utilization   { get; set; }
            public HashSet<ElementId> PlacedViewIds { get; set; } = new();
        }
    }
}
