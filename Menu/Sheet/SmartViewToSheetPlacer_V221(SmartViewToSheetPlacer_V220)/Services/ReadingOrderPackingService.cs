using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.SmartViewToSheetPlacer.V221.Models;

namespace Revit26_Plugin.SmartViewToSheetPlacer.V221.Services
{
    /// <summary>
    /// Pure calculation service: packs selected views onto suggested sheets.
    /// Placement is always non-mixed: each Revit ViewType group is packed
    /// onto its own independent set of sheets; a sheet never contains more
    /// than one ViewType. No Revit API writes happen here — this is a
    /// stateless, deterministic transform (aside from reading pre-resolved
    /// geometry off ViewInfo) used directly by Stage 2's ViewModel.
    ///
    /// V220 REWRITE — reading order now ONLY sorts (SortByReadingOrder,
    /// unchanged); it no longer decides row/sheet layout. Per explicit
    /// confirmation, sheet-count and per-sheet layout are now:
    ///   1. Walk the sorted list in original order, summing
    ///      WidthMm*HeightMm (raw item area, no gap allowance — confirmed
    ///      "rough estimate") against the titleblock's usable area. The
    ///      moment adding the next item would exceed it, stop — everything
    ///      accumulated so far is this sheet's CANDIDATE slice, still in
    ///      reading-order sequence (see BuildCandidateSlice).
    ///   2. Hand that slice to whichever of the 7 RowFillStrategy
    ///      algorithms this sheet is set to (global default at slice-time,
    ///      overridable per sheet afterward) — MasterRowBandPackingService
    ///      for Shelf/Guillotine/MaxRects, FreeRectPackingService for
    ///      MaxFill/Skyline/SkylineWasteMap, RafisAlgoPackingService for
    ///      RafisAlgo. Ported verbatim from SheetAutoRearrange V022 — see
    ///      those classes.
    ///   3. Anything the algorithm's own Unplaced list reports goes back to
    ///      the FRONT of the remaining pool, keeping their original
    ///      reading-order sequence among themselves, ahead of whatever
    ///      wasn't yet consumed (confirmed: "unplaced [B,D] + untouched
    ///      [E,F...] -> [B,D,E,F...]").
    ///   4. Repeat from step 1 until the remaining pool is empty.
    ///
    /// V220 CHANGE: EvenGap removed entirely (Fixed-gap-only, per explicit
    /// confirmation) — ViewGroupGapSettings.GapStyle no longer exists;
    /// HorizontalGapMm/VerticalGapMm feed the chosen algorithm directly
    /// (H for both axes on the 3 MasterRowBand strategies' single `gap`
    /// parameter, same convention as SheetAutoRearrange's original; H and V
    /// separately for the other 4 strategies, which support two axes).
    ///
    /// Still filters bad data (WidthMm/HeightMm <= 0) and oversized (wider
    /// than usable sheet width) exactly as before — neither ever reaches
    /// the capacity/algorithm loop.
    /// </summary>
    public static class ReadingOrderPackingService
    {
        /// <summary>
        /// Result of one full Pack() call: the produced sheets plus every
        /// view that did not make it onto a sheet, split by reason
        /// (Skipped-Oversized / Failed-Bad-Data / Failed-To-Place) so
        /// Stage 2/3's metric cards and per-row Status column can report
        /// each distinctly.
        /// </summary>
        public class PackResult
        {
            public List<SheetGroup> Sheets { get; } = new();
            public List<ViewInfo> SkippedOversized { get; } = new();
            public List<ViewInfo> FailedBadData { get; } = new();

            /// <summary>
            /// V220 NEW: views whose candidate slice a chosen RowFillStrategy
            /// algorithm placed ZERO of (defensive edge case — see
            /// PackSingleGroup's placedThisSheet.Count == 0 guard). Distinct
            /// from FailedBadData (missing/zero WidthMm/HeightMm) and
            /// SkippedOversized (wider than usable sheet width) — these
            /// views have valid, in-bounds dimensions but the algorithm
            /// still could not place them, which should be rare in practice.
            /// </summary>
            public List<ViewInfo> FailedToPlace { get; } = new();
        }

        /// <summary>
        /// Packs the given selected views onto suggested SheetGroups.
        /// Views are first split into independent ViewType groups (non-mixed),
        /// then each group is packed onto as many sheets as needed.
        /// </summary>
        /// <param name="selectedViews">Views the user selected in Stage 1.</param>
        /// <param name="titleblock">Titleblock providing usable sheet area (post-margin).</param>
        /// <param name="gapSettingsByType">Per-ViewType-group H/V gap values (Fixed-gap-only,
        /// V220). Every ViewType present in selectedViews must have an entry here — a group
        /// with no matching entry falls back to a fresh 5mm/5mm default (safety default,
        /// should not normally occur since the ViewModel builds this collection from the
        /// same distinct-ViewType set as selectedViews).</param>
        /// <param name="readingDirection">Global sort axis order (applies to all groups).</param>
        /// <param name="tiebreak">Global within-row-band secondary sort (applies to all groups).</param>
        /// <param name="yToleranceMm">Row-band tolerance in mm — views whose projected V
        /// coordinate falls within this distance of a band's running average are
        /// considered part of the same reading-order row.</param>
        /// <param name="defaultFillStrategy">V220 NEW: Stage 2's global default packing
        /// strategy — stamped onto every newly-created SheetGroup.FillStrategy as its
        /// initial "template" value, editable per sheet afterward.</param>
        public static PackResult Pack(
            IEnumerable<ViewInfo> selectedViews,
            TitleblockOption titleblock,
            IReadOnlyDictionary<ViewType, ViewGroupGapSettings> gapSettingsByType,
            ReadingDirection readingDirection,
            RowTiebreak tiebreak,
            double yToleranceMm,
            RowFillStrategy defaultFillStrategy)
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
                    readingDirection, tiebreak, yToleranceMm, defaultFillStrategy,
                    result.SkippedOversized, result.FailedBadData, result.FailedToPlace);

                result.Sheets.AddRange(sheetsForType);
            }

            return result;
        }

        /// <summary>
        /// Packs one ViewType group's views onto as many sheets as needed:
        /// filter bad data -> filter oversized -> sort by reading order ->
        /// area-sum capacity slice + algorithm dispatch per sheet (V220).
        /// </summary>
        private static List<SheetGroup> PackSingleGroup(
            List<ViewInfo> views,
            TitleblockOption titleblock,
            ViewType viewType,
            ViewGroupGapSettings gapSettings,
            ReadingDirection readingDirection,
            RowTiebreak tiebreak,
            double yToleranceMm,
            RowFillStrategy defaultFillStrategy,
            List<ViewInfo> skippedOversizedOut,
            List<ViewInfo> failedBadDataOut,
            List<ViewInfo> failedToPlaceOut)
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

            // ---- Step 3 (V220): area-sum capacity slice + algorithm dispatch,
            // one sheet at a time, until the remaining pool is empty. See class
            // header remarks for the full confirmed loop description.
            var remaining = new List<ViewInfo>(sortedViews);
            int sheetIndex = 1;
            const int maxSheetsSafetyValve = 500; // guards against a pathological infinite loop; should never be hit in practice

            while (remaining.Count > 0 && sheetIndex <= maxSheetsSafetyValve)
            {
                var sheet = new SheetGroup(viewType, sheetIndex, defaultFillStrategy);

                var candidates = BuildCandidateSlice(remaining, titleblock);
                if (candidates.Count == 0)
                {
                    // Safety valve: should not normally happen since oversized
                    // views were already filtered out in Step 1, but guards
                    // against an infinite loop if a single item's area alone
                    // exceeds the usable area (degenerate titleblock) or similar.
                    break;
                }

                var (placedThisSheet, unplacedThisSheet) = RunFillStrategy(candidates, titleblock, gapSettings, sheet.FillStrategy);

                if (placedThisSheet.Count == 0)
                {
                    // Defensive: the chosen algorithm placed NOTHING from a non-empty
                    // candidate slice (should not happen for a single-item slice against
                    // an already-oversized-filtered pool, but guards against burning
                    // through maxSheetsSafetyValve on a degenerate titleblock/algorithm
                    // edge case). Stop rather than loop — routed to its own
                    // failedToPlaceOut bucket (distinct from bad-data/oversized), since
                    // these views have valid dimensions but still couldn't be placed.
                    foreach (var v in candidates)
                        failedToPlaceOut.Add(v);
                    break;
                }

                foreach (var p in placedThisSheet)
                    AddPlacement(sheet, p.View, p.OffsetXMm, p.OffsetYMm, titleblock);

                sheets.Add(sheet);

                // Consume the candidates that were actually placed; unplaced
                // candidates go back to the FRONT of the remaining pool, in
                // their original reading-order sequence, ahead of whatever
                // wasn't yet touched this pass (confirmed loop behavior).
                // HashSet<ElementId> lookup instead of List<ViewInfo>.Contains
                // (O(1) vs O(n) per check) — candidate slices can be sizeable,
                // and this runs once per sheet in the outer while loop.
                var candidateIds = candidates.Select(v => v.ViewId).ToHashSet();
                var stillUntouched = remaining.Where(v => !candidateIds.Contains(v.ViewId)).ToList();
                remaining = unplacedThisSheet.Concat(stillUntouched).ToList();

                sheetIndex++;
            }

            return sheets;
        }

        private const double MmToFeet = 1.0 / 304.8;
        private const double FeetToMm = 304.8;

        private static readonly MasterRowBandPackingService MasterRowPacker = new();
        private static readonly FreeRectPackingService FreeRectPacker = new();
        private static readonly RafisAlgoPackingService RafisAlgoPacker = new();

        /// <summary>
        /// V220 NEW — replaces the old two-phase row-builder's row/sheet-split
        /// role. Walks the pool in its current (reading-order-preserving)
        /// sequence, summing raw item area (WidthMm*HeightMm, no gap
        /// allowance — confirmed "rough estimate") against the titleblock's
        /// usable area. Stops the moment adding the next item would exceed
        /// it; everything accumulated so far becomes this sheet's candidate
        /// slice. Always includes at least one item (even if that single
        /// item's area alone exceeds the usable area) so the caller's loop
        /// can make progress — the chosen algorithm will report it Unplaced
        /// if it genuinely can't fit, at which point it loops to the next
        /// sheet same as any other unplaced item.
        /// </summary>
        private static List<ViewInfo> BuildCandidateSlice(List<ViewInfo> pool, TitleblockOption titleblock)
        {
            double usableAreaMm2 = titleblock.UsableWidthMm * titleblock.UsableHeightMm;
            var slice = new List<ViewInfo>();
            double runningArea = 0;

            foreach (var v in pool)
            {
                double itemArea = v.WidthMm * v.HeightMm;

                if (slice.Count > 0 && runningArea + itemArea > usableAreaMm2 + 1e-6)
                    break;

                slice.Add(v);
                runningArea += itemArea;
            }

            return slice;
        }

        /// <summary>
        /// V220 NEW: public entry point for re-laying-out a SINGLE already-
        /// existing sheet's own views under a newly-selected RowFillStrategy
        /// (Generated Sheets list's per-sheet override dropdown). Thin
        /// wrapper over the same RunFillStrategy dispatch the main Pack()
        /// loop uses — does NOT touch sheet membership/sheet-count, purely
        /// re-lays-out the given candidates (which the caller — the
        /// ViewModel's OnSheetFillStrategyChanged — has already restricted
        /// to just that one sheet's current Placements).
        /// </summary>
        public static (List<(ViewInfo View, double OffsetXMm, double OffsetYMm)> Placed, List<ViewInfo> Unplaced) RepackSingleSheet(
            List<ViewInfo> candidates,
            TitleblockOption titleblock,
            ViewGroupGapSettings gapSettings,
            RowFillStrategy strategy)
            => RunFillStrategy(candidates, titleblock, gapSettings, strategy);

        /// <summary>
        /// V220 NEW — dispatches one sheet's candidate slice to whichever of
        /// the 7 RowFillStrategy algorithms this sheet is set to (ported
        /// verbatim from SheetAutoRearrange V022; see MasterRowBandPackingService
        /// / FreeRectPackingService / RafisAlgoPackingService). Converts the
        /// algorithm's top-based intermediate-space feet output into sheet-space
        /// mm offsets (OffsetXMm/OffsetYMm, top-left origin at the titleblock's
        /// usable-area corner — same convention ViewPlacement/Stage 2's preview
        /// canvas already expects). Fixed-gap-only: HorizontalGapMm feeds both
        /// axes for the 3 MasterRowBand strategies' single `gap` parameter (same
        /// convention as the SheetAutoRearrange original); H and V feed
        /// separately for the other 4 strategies.
        /// </summary>
        private static (List<(ViewInfo View, double OffsetXMm, double OffsetYMm)> Placed, List<ViewInfo> Unplaced) RunFillStrategy(
            List<ViewInfo> candidates,
            TitleblockOption titleblock,
            ViewGroupGapSettings gapSettings,
            RowFillStrategy strategy)
        {
            double containerWFeet = titleblock.UsableWidthMm * MmToFeet;
            double containerHFeet = titleblock.UsableHeightMm * MmToFeet;
            double hGapFeet = gapSettings.HorizontalGapMm * MmToFeet;
            double vGapFeet = gapSettings.VerticalGapMm * MmToFeet;

            switch (strategy)
            {
                case RowFillStrategy.Shelf:
                case RowFillStrategy.Guillotine:
                case RowFillStrategy.MaxRects:
                    {
                        var r = MasterRowPacker.Pack(candidates, containerWFeet, containerHFeet, hGapFeet, strategy);
                        return (ProjectToMm(r.Placed, p => p.Item, p => p.X, p => p.Y), r.Unplaced);
                    }
                case RowFillStrategy.MaxFill:
                case RowFillStrategy.Skyline:
                case RowFillStrategy.SkylineWasteMap:
                    {
                        var r = FreeRectPacker.Pack(candidates, containerWFeet, containerHFeet, hGapFeet, vGapFeet, strategy);
                        return (ProjectToMm(r.Placed, p => p.Item, p => p.X, p => p.Y), r.Unplaced);
                    }
                case RowFillStrategy.RafisAlgo:
                    {
                        var r = RafisAlgoPacker.Pack(candidates, containerWFeet, containerHFeet, hGapFeet, vGapFeet);
                        return (ProjectToMm(r.Placed, p => p.Item, p => p.X, p => p.Y), r.Unplaced);
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(strategy));
            }
        }

        /// <summary>
        /// V220 NEW — shared by all 3 branches of RunFillStrategy: each ported
        /// packer's own Placement type exposes the same (Item, X, Y) shape but
        /// as a distinct class (MasterRowBandPackingService.Placement,
        /// FreeRectPackingService.Placement, RafisAlgoPackingService.Placement
        /// aren't a common base type — kept as the SheetAutoRearrange original
        /// port had them, to stay verbatim-portable from that suite). This
        /// takes selector funcs instead of requiring a shared interface, so the
        /// three call sites collapse to one line each rather than three
        /// identical foreach-and-convert blocks.
        /// </summary>
        private static List<(ViewInfo View, double OffsetXMm, double OffsetYMm)> ProjectToMm<TPlacement>(
            List<TPlacement> placements,
            Func<TPlacement, ViewInfo> itemSelector,
            Func<TPlacement, double> xSelector,
            Func<TPlacement, double> ySelector)
        {
            var result = new List<(ViewInfo, double, double)>(placements.Count);
            foreach (var p in placements)
                result.Add((itemSelector(p), xSelector(p) * FeetToMm, ySelector(p) * FeetToMm));
            return result;
        }

        private static void AddPlacement(SheetGroup sheet, ViewInfo view, double offsetXMm, double offsetYMm, TitleblockOption titleblock)
        {
            var placement = new ViewPlacement(view, sheet)
            {
                OffsetXMm = offsetXMm,
                OffsetYMm = offsetYMm,
                Fits = true,
                Position = QuadrantLabel(view, offsetXMm, offsetYMm, titleblock)
            };
            sheet.Placements.Add(placement);
        }

        /// <summary>
        /// V220 NEW — ASSUMPTION FLAGGED: the old row-grid packer stamped a
        /// fixed "Top-Left/Top-Right/Bottom-Left/Bottom-Right" label by
        /// placement ORDER within a 4-item cycle (positionsInOrder array),
        /// which had no real geometric meaning once rows could hold any
        /// number of items. Since the 7 ported algorithms don't have that
        /// concept at all, this derives a genuine geometric quadrant label
        /// instead: which half of the sheet's usable width/height the
        /// view's CENTER falls in, relative to the usable area's own
        /// midpoint. Still used only for Stage 3's "Position on Sheet"
        /// display column and the PlaceViews log line — not fed back into
        /// any packing decision. Flagging in case a different convention
        /// (e.g. row/column index) is preferred instead.
        /// </summary>
        public static string QuadrantLabel(ViewInfo view, double offsetXMm, double offsetYMm, TitleblockOption titleblock)
        {
            double centerX = offsetXMm + view.WidthMm / 2.0;
            double centerY = offsetYMm + view.HeightMm / 2.0;
            double midX = titleblock.UsableWidthMm / 2.0;
            double midY = titleblock.UsableHeightMm / 2.0;

            string vertical = centerY <= midY ? "Top" : "Bottom";
            string horizontal = centerX <= midX ? "Left" : "Right";
            return $"{vertical}-{horizontal}";
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
            // is fixed to PushToEnd for V213; SortByName exists as a ported enum
            // value for future use, flagging this as an assumption).
            result.AddRange(unresolved);

            return result;
        }

        /// <summary>
        /// Re-validates whether a manually re-assigned placement still fits
        /// its new sheet's usable area. Called from Stage 2 when the user
        /// overrides "Suggested Sheet #" via the per-row dropdown. This is a
        /// simplified aggregate check (total packed row-height vs usable
        /// height, simple left-to-right row-wrap simulation) rather than a
        /// full re-run of the sheet's actual RowFillStrategy algorithm,
        /// since manual overrides are individual spot-edits, not a full
        /// repack. Unchanged in V220 beyond the Fixed-gap-only simplification
        /// implicit in ViewGroupGapSettings no longer having a GapStyle —
        /// this method always used HorizontalGapMm/VerticalGapMm directly
        /// and never branched on GapStyle itself.
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
