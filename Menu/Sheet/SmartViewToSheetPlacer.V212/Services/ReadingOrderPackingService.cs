using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.SmartViewToSheetPlacer.V212.Models;

namespace Revit26_Plugin.SmartViewToSheetPlacer.V212.Services
{
    /// <summary>
    /// Pure calculation service: packs selected views onto suggested sheets
    /// using reading-order sort + two-phase row packing (ported conceptually
    /// from APUS_V321_01's EvenGapPlacementService, adapted from section
    /// markers to View crop-box centers). Placement is always non-mixed:
    /// each Revit ViewType group is packed onto its own independent set of
    /// sheets; a sheet never contains more than one ViewType. No Revit API
    /// writes happen here — this is a stateless, deterministic transform
    /// (aside from reading pre-resolved geometry off ViewInfo) used directly
    /// by Stage 2's ViewModel.
    ///
    /// V212: replaces GreedyRowPackingService (largest-first sort, fixed-gap
    /// only, force-placed oversized views). Confirmed changes vs V212:
    ///   - Sort: reading-order (crop-box center -> U/V projection -> row
    ///     band -> tiebreak) instead of largest-area-first.
    ///   - Bad data (WidthMm/HeightMm <= 0): filtered out before sort,
    ///     reported as Failed — never placed.
    ///   - Oversized (wider than usable sheet width): filtered out before
    ///     sort, reported as Skipped-Oversized — never force-placed.
    ///   - Row-break: two-phase (build row width-first, then check whole-row
    ///     height fit, defer entire row to next sheet if it doesn't fit) —
    ///     replaces V212's single-pass speculative wrap check.
    ///   - Gap style is now per-ViewType-group (Fixed or EvenGap), supplied
    ///     via ViewGroupGapSettings instead of one global H/V gap pair.
    /// </summary>
    public static class ReadingOrderPackingService
    {
        /// <summary>
        /// Result of one full Pack() call: the produced sheets plus every
        /// view that did not make it onto a sheet, split by reason
        /// (Skipped-Oversized vs Failed-Bad-Data) so Stage 2/3's metric
        /// cards and per-row Status column can report both distinctly.
        /// </summary>
        public class PackResult
        {
            public List<SheetGroup> Sheets { get; } = new();
            public List<ViewInfo> SkippedOversized { get; } = new();
            public List<ViewInfo> FailedBadData { get; } = new();
        }

        /// <summary>
        /// Packs the given selected views onto suggested SheetGroups.
        /// Views are first split into independent ViewType groups (non-mixed),
        /// then each group is packed onto as many sheets as needed.
        /// </summary>
        /// <param name="selectedViews">Views the user selected in Stage 1.</param>
        /// <param name="titleblock">Titleblock providing usable sheet area (post-margin).</param>
        /// <param name="gapSettingsByType">Per-ViewType-group gap style + H/V gap values.
        /// Every ViewType present in selectedViews must have an entry here — a group with
        /// no matching entry falls back to GapStyle.Fixed / 5mm gaps (safety default,
        /// should not normally occur since the ViewModel builds this collection from the
        /// same distinct-ViewType set as selectedViews).</param>
        /// <param name="readingDirection">Global sort axis order (applies to all groups).</param>
        /// <param name="tiebreak">Global within-row-band secondary sort (applies to all groups).</param>
        /// <param name="yToleranceMm">Row-band tolerance in mm — views whose projected V
        /// coordinate falls within this distance of a band's running average are
        /// considered part of the same reading-order row.</param>
        public static PackResult Pack(
            IEnumerable<ViewInfo> selectedViews,
            TitleblockOption titleblock,
            IReadOnlyDictionary<ViewType, ViewGroupGapSettings> gapSettingsByType,
            ReadingDirection readingDirection,
            RowTiebreak tiebreak,
            double yToleranceMm)
        {
            var result = new PackResult();

            // Non-mixed: split into independent groups by RevitViewType only (per confirmed rule).
            var byType = selectedViews
                .GroupBy(v => v.RevitViewType)
                .OrderBy(g => g.Key.ToString());

            foreach (var group in byType)
            {
                var gapSettings = gapSettingsByType.TryGetValue(group.Key, out var gs)
                    ? gs
                    : new ViewGroupGapSettings(group.Key, group.Key.ToString());

                var sheetsForType = PackSingleGroup(
                    group.ToList(), titleblock, group.Key, gapSettings,
                    readingDirection, tiebreak, yToleranceMm,
                    result.SkippedOversized, result.FailedBadData);

                result.Sheets.AddRange(sheetsForType);
            }

            return result;
        }

        /// <summary>
        /// Packs one ViewType group's views onto as many sheets as needed:
        /// filter bad data -> filter oversized -> sort by reading order ->
        /// two-phase row-build per sheet.
        /// </summary>
        private static List<SheetGroup> PackSingleGroup(
            List<ViewInfo> views,
            TitleblockOption titleblock,
            ViewType viewType,
            ViewGroupGapSettings gapSettings,
            ReadingDirection readingDirection,
            RowTiebreak tiebreak,
            double yToleranceMm,
            List<ViewInfo> skippedOversizedOut,
            List<ViewInfo> failedBadDataOut)
        {
            var sheets = new List<SheetGroup>();

            // ---- Step 0: filter bad data (missing/zero dimensions) ----
            var valid = new List<ViewInfo>();
            foreach (var v in views)
            {
                if (v.WidthMm <= 0 || v.HeightMm <= 0)
                    failedBadDataOut.Add(v);
                else
                    valid.Add(v);
            }

            // ---- Step 1: filter oversized (wider than usable sheet width) ----
            var fittable = new List<ViewInfo>();
            foreach (var v in valid)
            {
                if (v.WidthMm > titleblock.UsableWidthMm)
                    skippedOversizedOut.Add(v);
                else
                    fittable.Add(v);
            }

            if (fittable.Count == 0)
                return sheets;

            // ---- Step 2: sort by reading order ----
            var sortedViews = SortByReadingOrder(fittable, readingDirection, tiebreak, yToleranceMm);

            // ---- Step 3: two-phase row packing across as many sheets as needed ----
            var remaining = new List<ViewInfo>(sortedViews);
            int sheetIndex = 1;

            while (remaining.Count > 0)
            {
                var sheet = new SheetGroup(viewType, sheetIndex);
                var rows = BuildSheetRows(remaining, titleblock, gapSettings);

                if (rows.Count == 0)
                {
                    // Safety valve: should not normally happen since oversized
                    // views were already filtered out in Step 1, but guards
                    // against an infinite loop if it ever does.
                    break;
                }

                LayoutRowsOntoSheet(sheet, rows, titleblock, gapSettings);
                sheets.Add(sheet);

                foreach (var row in rows)
                    foreach (var v in row)
                        remaining.Remove(v);

                sheetIndex++;
            }

            return sheets;
        }

        /// <summary>
        /// Phase A of the two-phase row-builder: consumes as many views as
        /// fit onto ONE sheet, grouped into rows. A row is built width-first
        /// (greedy left-to-right against usable width); once a row is
        /// complete, the whole row's height is checked against remaining
        /// sheet height — if it doesn't fit and the sheet already has at
        /// least one row, the ENTIRE row is deferred to the next sheet
        /// (never partially committed). This is the key V212 correction
        /// over V212's single-pass speculative wrap check.
        /// </summary>
        private static List<List<ViewInfo>> BuildSheetRows(
            List<ViewInfo> pool,
            TitleblockOption titleblock,
            ViewGroupGapSettings gapSettings)
        {
            var rows = new List<List<ViewInfo>>();
            double usableW = titleblock.UsableWidthMm;
            double usableH = titleblock.UsableHeightMm;
            double heightUsed = 0;

            int cursor = 0;
            while (cursor < pool.Count)
            {
                var row = new List<ViewInfo>();
                double widthUsed = 0;
                double rowHeight = 0;
                int i = cursor;

                for (; i < pool.Count; i++)
                {
                    var v = pool[i];
                    double addedWidth = row.Count == 0 ? v.WidthMm : v.WidthMm + gapSettings.HorizontalGapMm;

                    if (widthUsed + addedWidth <= usableW)
                    {
                        row.Add(v);
                        widthUsed += addedWidth;
                        rowHeight = Math.Max(rowHeight, v.HeightMm);
                    }
                    else
                    {
                        break; // row is done, width-wise
                    }
                }

                if (row.Count == 0)
                {
                    // Shouldn't happen — oversized views already filtered in
                    // Step 1 — but break rather than loop forever.
                    break;
                }

                double neededHeight = rows.Count == 0
                    ? rowHeight
                    : heightUsed + gapSettings.VerticalGapMm + rowHeight;

                if (neededHeight > usableH && rows.Count > 0)
                {
                    // Whole row deferred to the next sheet — stop this sheet here.
                    break;
                }

                rows.Add(row);
                heightUsed = neededHeight;
                cursor = i;
            }

            return rows;
        }

        /// <summary>
        /// Phase B: assigns actual OffsetXMm/OffsetYMm to each view in each
        /// row, dispatching to Fixed (tight-left, constant gap) or EvenGap
        /// (gap stretched to fill leftover width, single-item rows centered)
        /// per this group's GapStyle.
        /// </summary>
        private static void LayoutRowsOntoSheet(
            SheetGroup sheet,
            List<List<ViewInfo>> rows,
            TitleblockOption titleblock,
            ViewGroupGapSettings gapSettings)
        {
            var positionsInOrder = new[] { "Top-Left", "Top-Right", "Bottom-Left", "Bottom-Right" };
            int placedCount = 0;
            double rowTop = 0;

            foreach (var row in rows)
            {
                double rowHeight = row.Max(v => v.HeightMm);

                if (gapSettings.GapStyle == GapStyle.Fixed)
                {
                    double cursorX = 0;
                    foreach (var v in row)
                    {
                        AddPlacement(sheet, v, cursorX, rowTop, positionsInOrder, ref placedCount);
                        cursorX += v.WidthMm + gapSettings.HorizontalGapMm;
                    }
                }
                else // EvenGap
                {
                    if (row.Count == 1)
                    {
                        double centerX = (titleblock.UsableWidthMm - row[0].WidthMm) / 2.0;
                        AddPlacement(sheet, row[0], Math.Max(0, centerX), rowTop, positionsInOrder, ref placedCount);
                    }
                    else
                    {
                        double totalContentWidth = row.Sum(v => v.WidthMm);
                        double availableGapSpace = titleblock.UsableWidthMm - totalContentWidth;
                        double actualGap = Math.Max(gapSettings.HorizontalGapMm, availableGapSpace / (row.Count - 1));

                        double cursorX = 0;
                        foreach (var v in row)
                        {
                            AddPlacement(sheet, v, cursorX, rowTop, positionsInOrder, ref placedCount);
                            cursorX += v.WidthMm + actualGap;
                        }
                    }
                }

                rowTop += rowHeight + gapSettings.VerticalGapMm;
            }
        }

        private static void AddPlacement(
            SheetGroup sheet,
            ViewInfo view,
            double offsetXMm,
            double offsetYMm,
            string[] positionsInOrder,
            ref int placedCount)
        {
            var placement = new ViewPlacement(view, sheet)
            {
                OffsetXMm = offsetXMm,
                OffsetYMm = offsetYMm,
                Fits = true,
                Position = positionsInOrder[Math.Min(placedCount, positionsInOrder.Length - 1)]
            };
            sheet.Placements.Add(placement);
            placedCount++;
        }

        /// <summary>
        /// Reading-order sort: projects each view's model-space crop-box
        /// center onto the view's own Right (U) / Up (V) direction vectors,
        /// bands views into rows by V-coordinate proximity (band-average
        /// anchor, not chained point-to-point comparison — avoids the
        /// Y-band-drift bug APUS's V320 predecessor had), then sorts within
        /// each band by U position (or Name/Size per tiebreak). Views with
        /// no resolved crop-box geometry (ViewInfo.IsMarkerResolved == false)
        /// are pushed to the end, per SortFallback.PushToEnd — never
        /// skipped, only sorted last, already logged as Warning at load time.
        /// </summary>
        private static List<ViewInfo> SortByReadingOrder(
            List<ViewInfo> views,
            ReadingDirection direction,
            RowTiebreak tiebreak,
            double yToleranceMm)
        {
            var resolved = new List<(ViewInfo View, double U, double V)>();
            var unresolved = new List<ViewInfo>();

            foreach (var v in views)
            {
                if (!v.IsMarkerResolved || v.CropCenterModel == null || v.RightDirection == null || v.UpDirection == null)
                {
                    unresolved.Add(v);
                    continue;
                }

                var center = v.CropCenterModel;
                double uFeet = center.DotProduct(v.RightDirection);
                double vFeet = center.DotProduct(v.UpDirection);

                // Convert to mm so yToleranceMm (a UI-facing mm value) compares
                // against the same units as the row-band grouping below.
                resolved.Add((v, uFeet * 304.8, vFeet * 304.8));
            }

            bool topToBottom = direction is ReadingDirection.TopToBottom_LeftToRight or ReadingDirection.TopToBottom_RightToLeft;
            bool leftToRight = direction is ReadingDirection.TopToBottom_LeftToRight or ReadingDirection.BottomToTop_LeftToRight;

            // Top-to-bottom means decreasing V (Up-axis) first; bottom-to-top means increasing V first.
            var orderedByV = topToBottom
                ? resolved.OrderByDescending(r => r.V).ToList()
                : resolved.OrderBy(r => r.V).ToList();

            // Band into rows by V-tolerance using a running band-average anchor
            // (average of all V's currently in the band), not the last point
            // added — comparing against a running average avoids row-drift
            // when several nearly-aligned views incrementally pull the band
            // threshold away from the row's true center.
            var bands = new List<List<(ViewInfo View, double U, double V)>>();
            foreach (var r in orderedByV)
            {
                if (bands.Count == 0)
                {
                    bands.Add(new List<(ViewInfo, double, double)> { r });
                    continue;
                }

                var currentBand = bands[^1];
                double bandAverageV = currentBand.Average(b => b.V);

                if (Math.Abs(r.V - bandAverageV) <= yToleranceMm)
                    currentBand.Add(r);
                else
                    bands.Add(new List<(ViewInfo, double, double)> { r });
            }

            var result = new List<ViewInfo>();
            foreach (var band in bands)
            {
                IEnumerable<(ViewInfo View, double U, double V)> sortedBand = tiebreak switch
                {
                    RowTiebreak.Name => band.OrderBy(b => b.View.Name, StringComparer.OrdinalIgnoreCase),
                    RowTiebreak.Size => band.OrderByDescending(b => b.View.WidthMm * b.View.HeightMm),
                    _ => leftToRight ? band.OrderBy(b => b.U) : band.OrderByDescending(b => b.U)
                };

                result.AddRange(sortedBand.Select(b => b.View));
            }

            // Unresolved views always pushed to the tail, per SortFallback.PushToEnd.
            // SortFallback.SortByName would additionally alphabetize within this
            // tail group — not yet exposed as a UI choice (global sort fallback
            // is fixed to PushToEnd for V212; SortByName exists as a ported enum
            // value for future use, flagging this as an assumption).
            result.AddRange(unresolved);

            return result;
        }

        /// <summary>
        /// Re-validates whether a manually re-assigned placement still fits
        /// its new sheet's usable area. Called from Stage 2 when the user
        /// overrides "Suggested Sheet #" via the per-row dropdown. This is a
        /// simplified aggregate check (total packed row-height vs usable
        /// height) rather than a full re-run of row-packing, since manual
        /// overrides are individual spot-edits, not a full repack.
        /// V212: takes the target sheet's ViewGroupGapSettings instead of
        /// two raw doubles, so the aggregate check uses the correct
        /// Fixed/EvenGap-consistent horizontal gap for that group (EvenGap's
        /// stretched gap is not modeled here since this is a worst-case
        /// aggregate estimate, not a real layout — it uses HorizontalGapMm
        /// as the per-item minimum, which is always <= the actual EvenGap
        /// spacing, so this check is conservative and will not report "fits"
        /// when the real layout would not).
        /// </summary>
        public static bool RevalidateFit(
            SheetGroup sheet,
            TitleblockOption titleblock,
            ViewGroupGapSettings gapSettings)
        {
            double usedHeight = 0;
            double rowWidth = 0;
            double rowHeight = 0;
            bool isFirstOnRow = true;
            bool isFirstRow = true;

            foreach (var p in sheet.Placements)
            {
                double gapBeforeThisItem = isFirstOnRow ? 0 : gapSettings.HorizontalGapMm;

                if (rowWidth + gapBeforeThisItem + p.View.WidthMm > titleblock.UsableWidthMm)
                {
                    double gapBeforeThisRow = isFirstRow ? 0 : gapSettings.VerticalGapMm;
                    usedHeight += rowHeight + gapBeforeThisRow;
                    rowWidth = 0;
                    rowHeight = 0;
                    isFirstOnRow = true;
                    isFirstRow = false;
                    gapBeforeThisItem = 0;
                }

                rowWidth += gapBeforeThisItem + p.View.WidthMm;
                rowHeight = Math.Max(rowHeight, p.View.HeightMm);
                isFirstOnRow = false;
            }
            usedHeight += rowHeight;

            bool fits = usedHeight <= titleblock.UsableHeightMm;
            foreach (var p in sheet.Placements)
                p.Fits = fits;

            return fits;
        }
    }
}
