// File: Services/SimpleGridPlacementService.cs
using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V320.ExternalEvents;
using Revit26_Plugin.APUS_V320.Helpers;
using Revit26_Plugin.APUS_V320.Models;
using Revit26_Plugin.APUS_V320.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V320.Services
{
    /// <summary>
    /// SIMPLE GRID PLACEMENT SERVICE — adaptive fill layout, three-pass
    ///
    /// Algorithm
    /// ─────────
    /// Pass 1  Create all viewports at temporary stacked positions so
    ///         Revit can compute their real rendered sizes (crop + title strip).
    ///
    /// Pass 2  Read GetBoxOutline() for every viewport.
    ///         Pack viewports into rows greedily: add the next view to the
    ///         current row if it still fits horizontally; otherwise start a
    ///         new row.  Once all rows are known, scale row heights up
    ///         uniformly so the total grid exactly fills the usable height.
    ///         Within each row, distribute the inter-view gaps evenly so
    ///         the row spans the full usable width.
    ///
    /// Pass 3  Move every viewport to its final position:
    ///           Horizontal — left-edge of the view's slot in the row
    ///           Vertical   — bottom-aligned within the row
    /// </summary>
    public class SimpleGridPlacementService
    {
        private readonly Document _doc;

        public SimpleGridPlacementService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        // ── public entry point ────────────────────────────────────────
        public SectionPlacementHandler.PlacementResult Place(
            SectionPlacementHandler.PlacementContext context,
            List<SectionItemViewModel> sections,
            View referenceView,
            int gridRows,        // kept for multi-sheet capacity logic
            int gridColumns,     // kept for multi-sheet capacity logic
            bool placeToMultipleSheets)
        {
            var result = new SectionPlacementHandler.PlacementResult
            {
                SheetNumbers = new HashSet<string>()
            };

            if (sections == null || !sections.Any())
            {
                context.ViewModel?.LogWarning("⚠️ No sections to place.");
                result.ErrorMessage = "No sections to place";
                return result;
            }

            int maxViewsPerSheet = gridRows * gridColumns;
            LogInitialization(context, sections, gridRows, gridColumns, maxViewsPerSheet, placeToMultipleSheets);

            try
            {
                context.ViewModel?.LogInfo("\n📏 STAGE 1: Sorting sections in reading order...");
                var sortedItems = PrepareItemsInReadingOrder(sections, referenceView, context);
                if (!sortedItems.Any()) { result.ErrorMessage = "No valid items after sorting"; return result; }

                var dimensions = CalculateSheetDimensions(context);
                LogSheetDimensions(context, dimensions);

                double hGap = UnitConversionHelper.MmToFeet(context.HorizontalGapMm);
                double vGap = UnitConversionHelper.MmToFeet(context.VerticalGapMm);
                context.ViewModel?.LogInfo($"\n   📐 MIN GAPS: H={hGap * 304.8:F0}mm, V={vGap * 304.8:F0}mm");

                int totalPlaced = 0;
                int sheetCount = 0;
                int currentIndex = 0;

                while (currentIndex < sortedItems.Count)
                {
                    if (context.ViewModel?.Progress.IsCancelled == true) break;

                    sheetCount++;
                    int viewsForThisSheet = placeToMultipleSheets
                        ? Math.Min(maxViewsPerSheet, sortedItems.Count - currentIndex)
                        : sortedItems.Count - currentIndex; // fill the sheet completely

                    var itemsForSheet = sortedItems.Skip(currentIndex).Take(viewsForThisSheet).ToList();

                    context.ViewModel?.LogInfo($"\n{'═',0} SHEET {sheetCount} {'═',70}");
                    context.ViewModel?.LogInfo($"   • Placing views {currentIndex + 1} to {currentIndex + itemsForSheet.Count}");

                    var sheet = CreateSheet(context, sheetCount);
                    if (sheet == null) break;

                    DrawGridOutline(sheet, dimensions, context);

                    var sheetResult = PlaceViewsOnSheet(
                        sheet, itemsForSheet, dimensions,
                        hGap, vGap, context, currentIndex + 1);

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
                        _doc.Delete(sheet.Id);
                        context.ViewModel?.LogWarning($"   ⚠️ No views placed on sheet {sheetCount} - removing");
                        if (!placeToMultipleSheets) break;
                    }

                    if (!placeToMultipleSheets) break;
                }

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

        // ── three-pass adaptive fill ──────────────────────────────────
        private SheetResult PlaceViewsOnSheet(
            ViewSheet sheet,
            List<SheetItem> items,
            SheetDimensions dim,
            double minHGap,
            double minVGap,
            SectionPlacementHandler.PlacementContext context,
            int startNumber)
        {
            var result = new SheetResult();
            var placedViewIds = new HashSet<ElementId>();

            // ── PASS 1: create all viewports at temporary positions ───
            // Stack them in a single column just inside the left margin
            // so Revit accepts them (must be on-sheet) and can measure them.
            context.ViewModel?.LogInfo($"\n   📦 PASS 1 — Creating viewports at temp positions...");

            double tempX = dim.StartX + UnitConversionHelper.MmToFeet(5);
            double tempYInc = UnitConversionHelper.MmToFeet(30); // 30 mm step
            double tempY = dim.StartY - UnitConversionHelper.MmToFeet(15);

            // vpList keeps creation order aligned with items
            var vpList = new List<(Viewport vp, int itemIdx)>();

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                int globalNum = startNumber + i;

                try
                {
                    if (!CanPlaceView(item.Section.View, _doc, sheet.Id))
                    {
                        context.ViewModel?.LogWarning(
                            $"      ⚠️ [{globalNum}] {item.ViewName} — already placed, skipping");
                        vpList.Add((null, i));
                        continue;
                    }

                    var vp = Viewport.Create(_doc, sheet.Id, item.Section.View.Id,
                                             new XYZ(tempX, tempY - i * tempYInc, 0));

                    int det = context.GetNextDetailNumber();
                    var dp = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                    if (dp != null && !dp.IsReadOnly) dp.Set(det.ToString());

                    placedViewIds.Add(item.Section.View.Id);
                    vpList.Add((vp, i));

                    context.ViewModel?.LogInfo(
                        $"      ✅ [{globalNum}] {item.ViewName,-35} Detail: {det}");
                    context.ViewModel?.Progress.Step();
                }
                catch (Exception ex)
                {
                    context.ViewModel?.LogError($"      ❌ [{globalNum}] {item.ViewName} — {ex.Message}");
                    vpList.Add((null, i));
                }
            }

            // ── PASS 2: read real sizes, build adaptive row layout ───
            context.ViewModel?.LogInfo($"\n   📐 PASS 2 — Reading real sizes & computing layout...");

            // vpSize[i] = real rendered (w, h) in feet; (0,0) if viewport was null
            var vpSize = new (double w, double h)[items.Count];
            foreach (var (vp, i) in vpList)
            {
                if (vp == null) continue;
                var box = vp.GetBoxOutline();
                if (box == null) continue;
                vpSize[i] = (
                    box.MaximumPoint.X - box.MinimumPoint.X,
                    box.MaximumPoint.Y - box.MinimumPoint.Y);
            }

            // Greedy row packing ─────────────────────────────────────
            // A row is a list of item indices.
            // We add the next view to the current row if
            //   sum_of_widths + (n-1)*minHGap <= usableWidth
            // otherwise we start a new row.
            var rows = new List<List<int>>();  // rows[r] = list of item indices
            var currentRow = new List<int>();
            double currentRowWidth = 0;

            for (int i = 0; i < items.Count; i++)
            {
                if (vpList[i].vp == null) continue; // skip failed viewports

                double w = vpSize[i].w;
                double gapNeeded = currentRow.Count > 0 ? minHGap : 0;

                if (currentRow.Count > 0 && currentRowWidth + gapNeeded + w > dim.UsableWidth + 1e-9)
                {
                    // Finish current row, start a new one
                    rows.Add(currentRow);
                    currentRow = new List<int>();
                    currentRowWidth = 0;
                }

                currentRow.Add(i);
                currentRowWidth += (currentRow.Count > 1 ? minHGap : 0) + w;
            }
            if (currentRow.Count > 0) rows.Add(currentRow);

            int rowCount = rows.Count;

            context.ViewModel?.LogInfo($"      Packed {placedViewIds.Count} views into {rowCount} rows:");
            for (int r = 0; r < rowCount; r++)
            {
                double rowNatW = rows[r].Sum(i => vpSize[i].w) + minHGap * (rows[r].Count - 1);
                double rowNatH = rows[r].Max(i => vpSize[i].h);
                context.ViewModel?.LogInfo(
                    $"        Row {r + 1}: {rows[r].Count} views  " +
                    $"nat. {rowNatW * 304.8:F0}×{rowNatH * 304.8:F0} mm");
            }

            if (rowCount == 0)
            {
                result.PlacedCount = 0;
                result.PlacedViewIds = placedViewIds;
                return result;
            }

            // Natural row heights (tallest viewport in each row)
            double[] rowNatHeight = new double[rowCount];
            for (int r = 0; r < rowCount; r++)
                rowNatHeight[r] = rows[r].Max(i => vpSize[i].h);

            // Total natural height and available height
            double totalNatH = rowNatHeight.Sum() + minVGap * (rowCount - 1);
            double availH = dim.UsableHeight;

            // Scale row heights up (or down) so they exactly fill availH
            // Keep the proportional distribution between rows.
            double vScale = availH / totalNatH;
            double[] finalRowH = rowNatHeight.Select(h => h * vScale).ToArray();

            // Adjusted vGap: keep original if scaling up, shrink proportionally if scaling down
            double finalVGap = vScale >= 1.0 ? minVGap : minVGap * vScale;

            // Verify total fits (re-check after gap adjustment)
            double checkH = finalRowH.Sum() + finalVGap * (rowCount - 1);
            if (checkH > availH + 1e-6)
            {
                // Re-scale heights to absorb any rounding
                double adj = availH / checkH;
                for (int r = 0; r < rowCount; r++) finalRowH[r] *= adj;
                finalVGap *= adj;
            }

            // For each row: distribute horizontal gaps evenly so the row
            // spans the full usable width.
            // finalRowGap[r] = hGap between views in row r
            double[] finalRowHGap = new double[rowCount];
            // finalViewX[r][c] = left edge of view c in row r
            var finalViewX = new List<double[]>();

            for (int r = 0; r < rowCount; r++)
            {
                int n = rows[r].Count;
                double sumW = rows[r].Sum(i => vpSize[i].w);
                double gapSpace = dim.UsableWidth - sumW;

                double gap;
                double[] xs = new double[n];

                if (n == 1)
                {
                    // Single view in row: centre it
                    gap = 0;
                    xs[0] = dim.StartX + (dim.UsableWidth - vpSize[rows[r][0]].w) / 2.0;
                }
                else
                {
                    // Distribute gap evenly between views
                    gap = Math.Max(minHGap, gapSpace / (n - 1));
                    double x = dim.StartX;
                    for (int c = 0; c < n; c++)
                    {
                        xs[c] = x;
                        x += vpSize[rows[r][c]].w + gap;
                    }
                }

                finalRowHGap[r] = gap;
                finalViewX.Add(xs);
            }

            // Row top-edge origins (Y decreases downward on sheet)
            double[] rowTopY = new double[rowCount];
            double y = dim.StartY;
            for (int r = 0; r < rowCount; r++)
            {
                rowTopY[r] = y;
                y -= finalRowH[r] + finalVGap;
            }

            context.ViewModel?.LogInfo($"\n      Final layout:");
            for (int r = 0; r < rowCount; r++)
                context.ViewModel?.LogInfo(
                    $"        Row {r + 1}: height {finalRowH[r] * 304.8:F0} mm  " +
                    $"hGap {finalRowHGap[r] * 304.8:F0} mm  top-Y {rowTopY[r] * 304.8:F0} mm");

            // ── PASS 3: move every viewport to final position ─────────
            context.ViewModel?.LogInfo($"\n   🎯 PASS 3 — Repositioning viewports...");

            for (int r = 0; r < rowCount; r++)
            {
                double rowBottom = rowTopY[r] - finalRowH[r];

                for (int c = 0; c < rows[r].Count; c++)
                {
                    int itemIdx = rows[r][c];
                    var (vp, _) = vpList.First(x => x.itemIdx == itemIdx);
                    if (vp == null) continue;

                    var box = vp.GetBoxOutline();
                    if (box == null) continue;

                    double vpW = box.MaximumPoint.X - box.MinimumPoint.X;
                    double vpH = box.MaximumPoint.Y - box.MinimumPoint.Y;

                    // Target centre
                    //   X: left edge of this view's slot + half view width
                    //   Y: row bottom + half view height  (bottom-aligned)
                    double targetCX = finalViewX[r][c] + vpSize[itemIdx].w / 2.0;
                    double targetCY = rowBottom + vpH / 2.0;

                    double currentCX = (box.MinimumPoint.X + box.MaximumPoint.X) / 2.0;
                    double currentCY = (box.MinimumPoint.Y + box.MaximumPoint.Y) / 2.0;

                    double dx = targetCX - currentCX;
                    double dy = targetCY - currentCY;

                    if (Math.Abs(dx) > 1e-9 || Math.Abs(dy) > 1e-9)
                        ElementTransformUtils.MoveElement(_doc, vp.Id, new XYZ(dx, dy, 0));

                    context.ViewModel?.LogInfo(
                        $"      Row {r + 1} Col {c + 1}: " +
                        $"centre ({targetCX * 304.8:F0}, {targetCY * 304.8:F0}) mm  " +
                        $"size {vpW * 304.8:F0}×{vpH * 304.8:F0} mm");
                }
            }

            DrawRowSeparators(sheet, dim, rowTopY, finalRowH, finalVGap, rowCount, context);
            DrawViewportOutlines(sheet, context);

            result.PlacedCount = placedViewIds.Count;
            result.PlacedViewIds = placedViewIds;
            result.Utilization = CalculateUtilization(placedViewIds, dim, context);
            return result;
        }

        // ── reading order sort ────────────────────────────────────────
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
                var fp = ViewSizeService.Calculate(section.View);

                items.Add(new SheetItem
                {
                    Section = section,
                    X = x,
                    Y = y,
                    Width = fp.WidthFt,
                    Height = fp.HeightFt,
                    ViewId = section.View.Id,
                    ViewName = section.ViewName
                });
            }

            var sorted = items
                .OrderByDescending(i => i.Y)
                .ThenBy(i => i.X)
                .ToList();

            context.ViewModel?.LogInfo($"\n📋 READING ORDER (Top 10):");
            for (int i = 0; i < Math.Min(10, sorted.Count); i++)
            {
                var it = sorted[i];
                context.ViewModel?.LogInfo(
                    $"   #{i + 1,2}: {it.ViewName,-35}" +
                    $"  Pos: ({it.X * 304.8:F0}, {it.Y * 304.8:F0}) mm" +
                    $"  Footprint: {it.Width * 304.8:F0}×{it.Height * 304.8:F0} mm");
            }

            return sorted;
        }

        // ── sheet dimensions ──────────────────────────────────────────
        private SheetDimensions CalculateSheetDimensions(SectionPlacementHandler.PlacementContext context)
        {
            double left = UnitConversionHelper.MmToFeet(context.ViewModel?.LeftMarginMm ?? 40);
            double right = UnitConversionHelper.MmToFeet(context.ViewModel?.RightMarginMm ?? 150);
            double top = UnitConversionHelper.MmToFeet(context.ViewModel?.TopMarginMm ?? 40);
            double bottom = UnitConversionHelper.MmToFeet(context.ViewModel?.BottomMarginMm ?? 100);

            return new SheetDimensions
            {
                UsableWidth = context.PlacementArea.Width - left - right,
                UsableHeight = context.PlacementArea.Height - top - bottom,
                StartX = context.PlacementArea.Origin.X + left,
                StartY = context.PlacementArea.Origin.Y - top
            };
        }

        // ── sheet creation ────────────────────────────────────────────
        private ViewSheet CreateSheet(SectionPlacementHandler.PlacementContext context, int sheetNumber)
        {
            string num = context.SheetNumberService.GetNextAvailableSheetNumber($"GRID{sheetNumber}");
            context.SheetNumberService.TryReserveSheetNumber(num);
            return new SheetCreationService(_doc).Create(context.TitleBlock, num, $"Grid-{num}");
        }

        // ── helpers ───────────────────────────────────────────────────
        private XYZ GetSectionLocation(ViewSection view)
        {
            try
            {
                if (view.Location is LocationCurve lc && lc.Curve != null)
                    return lc.Curve.Evaluate(0.5, true);
                BoundingBoxXYZ bb = view.CropBox;
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

        private double CalculateUtilization(HashSet<ElementId> ids, SheetDimensions dim,
            SectionPlacementHandler.PlacementContext ctx)
        {
            try
            {
                double area = 0;
                foreach (var id in ids)
                    if (_doc.GetElement(id) is ViewSection v)
                    {
                        var fp = ViewSizeService.Calculate(v);
                        area += fp.WidthFt * fp.HeightFt;
                    }
                double sheetArea = dim.UsableWidth * dim.UsableHeight;
                return sheetArea > 0 ? area * 100.0 / sheetArea : 0;
            }
            catch { return 0; }
        }

        // ── drawing ───────────────────────────────────────────────────
        private void DrawGridOutline(ViewSheet sheet, SheetDimensions dim,
            SectionPlacementHandler.PlacementContext ctx)
        {
            try
            {
                using var t = new Transaction(_doc, "Draw Grid Outline");
                t.Start();
                var pts = new[]
                {
                    new XYZ(dim.StartX,                   dim.StartY,                   0),
                    new XYZ(dim.StartX + dim.UsableWidth,  dim.StartY,                   0),
                    new XYZ(dim.StartX + dim.UsableWidth,  dim.StartY - dim.UsableHeight, 0),
                    new XYZ(dim.StartX,                   dim.StartY - dim.UsableHeight, 0),
                    new XYZ(dim.StartX,                   dim.StartY,                   0)
                };
                for (int i = 0; i < pts.Length - 1; i++)
                    _doc.Create.NewDetailCurve(sheet, Line.CreateBound(pts[i], pts[i + 1]));
                t.Commit();
                ctx.ViewModel?.LogInfo("      📏 Grid outline drawn");
            }
            catch (Exception ex) { ctx.ViewModel?.LogWarning($"      ⚠️ Grid outline: {ex.Message}"); }
        }

        /// <summary>Draw horizontal separators between rows at the gap midpoints.</summary>
        private void DrawRowSeparators(
            ViewSheet sheet, SheetDimensions dim,
            double[] rowTopY, double[] rowH, double vGap,
            int rowCount, SectionPlacementHandler.PlacementContext ctx)
        {
            try
            {
                using var t = new Transaction(_doc, "Draw Row Separators");
                t.Start();

                double left = dim.StartX;
                double right = dim.StartX + dim.UsableWidth;

                for (int r = 1; r < rowCount; r++)
                {
                    // Separator at midpoint of the gap above row r
                    double sepY = rowTopY[r] + vGap / 2.0;
                    _doc.Create.NewDetailCurve(sheet,
                        Line.CreateBound(new XYZ(left, sepY, 0), new XYZ(right, sepY, 0)));
                }

                t.Commit();
                ctx.ViewModel?.LogInfo($"      📏 {rowCount - 1} row separator(s) drawn");
            }
            catch (Exception ex) { ctx.ViewModel?.LogWarning($"      ⚠️ Row separators: {ex.Message}"); }
        }

        private void DrawViewportOutlines(ViewSheet sheet, SectionPlacementHandler.PlacementContext ctx)
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
                ctx.ViewModel?.LogInfo("      📏 Viewport outlines drawn");
            }
            catch (Exception ex) { ctx.ViewModel?.LogWarning($"      ⚠️ Viewport outlines: {ex.Message}"); }
        }

        // ── logging ───────────────────────────────────────────────────
        private void LogInitialization(SectionPlacementHandler.PlacementContext ctx,
            List<SectionItemViewModel> sections, int rows, int cols, int max, bool multi)
        {
            ctx.ViewModel?.LogInfo("════════════════════════════════════════════════");
            ctx.ViewModel?.LogInfo("📚 SIMPLE GRID PLACEMENT SERVICE  (adaptive fill)");
            ctx.ViewModel?.LogInfo("════════════════════════════════════════════════");
            ctx.ViewModel?.LogInfo($"📊 Total sections: {sections.Count}");
            ctx.ViewModel?.LogInfo($"📐 Max per sheet:  {max}  (from {rows}×{cols} setting)");
            ctx.ViewModel?.LogInfo($"   • Mode:       {(multi ? "Multiple sheets" : "Fill one sheet")}");
            ctx.ViewModel?.LogInfo($"   • Order:      Left→Right, Top→Bottom");
            ctx.ViewModel?.LogInfo($"   • H-align:    distributed / centred within row");
            ctx.ViewModel?.LogInfo($"   • V-align:    bottom-aligned, rows scaled to fill height");
            ctx.ViewModel?.LogInfo($"   • Sizing:     real GetBoxOutline() — includes title strip");
        }

        private void LogSheetDimensions(SectionPlacementHandler.PlacementContext ctx, SheetDimensions d)
        {
            ctx.ViewModel?.LogInfo($"\n   📐 SHEET DIMENSIONS:");
            ctx.ViewModel?.LogInfo($"      • Usable: {d.UsableWidth * 304.8:F0} × {d.UsableHeight * 304.8:F0} mm");
            ctx.ViewModel?.LogInfo($"      • Origin: ({d.StartX * 304.8:F0}, {d.StartY * 304.8:F0}) mm");
        }

        private void LogSheetResult(SectionPlacementHandler.PlacementContext ctx,
            SheetResult r, int num, int remaining, bool multi)
        {
            ctx.ViewModel?.LogSuccess($"\n✅ Sheet {num} complete: {r.PlacedCount} views placed");
            ctx.ViewModel?.LogInfo($"   • Utilization: {r.Utilization:F1}%");
            if (multi) ctx.ViewModel?.LogInfo($"   • Remaining: {remaining}");
        }

        private void LogFinalReport(SectionPlacementHandler.PlacementContext ctx,
            List<SectionItemViewModel> sections, int placed, int sheets,
            SectionPlacementHandler.PlacementResult result)
        {
            ctx.ViewModel?.LogInfo("\n📊 FINAL PLACEMENT REPORT");
            ctx.ViewModel?.LogInfo("════════════════════════════════════════════════");
            ctx.ViewModel?.LogInfo($"   • Total:   {sections.Count}");
            ctx.ViewModel?.LogInfo($"   • Placed:  {placed}");
            ctx.ViewModel?.LogInfo($"   • Sheets:  {sheets}");
            if (placed < sections.Count)
            {
                result.FailedCount = sections.Count - placed;
                ctx.ViewModel?.LogWarning($"   • Skipped: {result.FailedCount}");
            }
            else ctx.ViewModel?.LogSuccess("   • All sections placed successfully!");
            if (result.SheetNumbers.Any())
                ctx.ViewModel?.LogInfo($"   • Sheet numbers: {string.Join(", ", result.SheetNumbers.OrderBy(s => s))}");
        }

        // ── inner types ───────────────────────────────────────────────
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
            public int PlacedCount { get; set; }
            public double Utilization { get; set; }
            public HashSet<ElementId> PlacedViewIds { get; set; } = new HashSet<ElementId>();
        }
    }
}