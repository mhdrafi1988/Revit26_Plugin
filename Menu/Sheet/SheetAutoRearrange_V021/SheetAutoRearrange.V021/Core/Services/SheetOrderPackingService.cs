using Autodesk.Revit.DB;
using Revit26_Plugin.SheetAutoRearrange.V021.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.SheetAutoRearrange.V021.Core.Services
{
    /// <summary>
    /// Sheet Auto Rearrange's only packing algorithm as of V009 (Reading
    /// Order still exists as a file for potential future use, but is no
    /// longer reachable from the UI — algorithm selection was removed per
    /// explicit request: "only on method is needed (by default)").
    ///
    /// V009 CHANGE: gap between views is now a single global H/V value
    /// (GapSettings.GlobalHorizontalGapMm / GlobalVerticalGapMm) instead of
    /// per-ViewType groups — ResolveGroup/ViewTypeGroupResolver removed
    /// entirely, every gap lookup below reads straight off GapSettings.
    ///
    /// V014 CHANGE: ShelfPackingService (Tall/Wide anchor + L-shape shelf
    /// system) REMOVED entirely, replaced by MasterRowBandPackingService —
    /// see that class's header for the full algorithm description and its
    /// known row-height plateau characteristic (ported as-specified,
    /// unfixed, per explicit request). There is no anchor concept anymore:
    /// every item goes through the same Master Row/Band pipeline, then any
    /// item that service could not place falls through to the simple
    /// row-packing tail below (GroupIntoRows), same as before.
    ///
    /// V014 FIX: overflow items (Fits=false) are now actually relocated
    /// below the titleblock's usable area (largeRect.Min.Y and downward)
    /// instead of keeping whatever in-bounds/overlapping coordinates the
    /// packer had already computed for them. Previously the Warning log
    /// message claimed "placed below the sheet" but nothing enforced that —
    /// an overflow item could sit anywhere the packer left it, including
    /// still inside the usable region overlapping another view. See the
    /// dedicated block near the end of Pack() for the relocation logic.
    /// </summary>
    public class SheetOrderPackingService
    {
        private const double MmToFeet = 1.0 / 304.8;

        private readonly MasterRowBandPackingService _masterRowPacker = new();

        // ── Small shared conversions, used throughout Pack() — extracted
        // since "item.WidthMm/HeightMm * MmToFeet" was repeated inline at
        // every call site (6+ times) with no single source of truth.
        private static double WFeet(ViewOnSheetItem item) => item.WidthMm * MmToFeet;
        private static double HFeet(ViewOnSheetItem item) => item.HeightMm * MmToFeet;

        /// <summary>
        /// Axis-aligned left/right/top/bottom bounds of a placement's
        /// footprint, in sheet-space feet. Extracted from the overlap-check
        /// block below, where this exact 4-line "center + half-size ->
        /// bounds" computation was duplicated once for the candidate and
        /// once (inline, in a lambda) for each already-accepted item.
        /// </summary>
        private static (double left, double right, double top, double bottom) GetBounds(PackedViewPlacement p)
        {
            double halfW = WFeet(p.Item) / 2.0;
            double halfH = HFeet(p.Item) / 2.0;
            return (p.NewCenter.X - halfW, p.NewCenter.X + halfW, p.NewCenter.Y + halfH, p.NewCenter.Y - halfH);
        }

        public List<PackedViewPlacement> Pack(
            List<ViewOnSheetItem> items,
            PlaceableRegion region,
            GapSettings gapSettings,
            double rowToleranceMm,
            RowAlignment rowAlignment,
            BlockAlignmentH blockH,
            BlockAlignmentV blockV,
            RowFillStrategy fillStrategy)
        {
            var results = new List<PackedViewPlacement>();
            if (items.Count == 0)
                return results;

            double toleranceFeet = rowToleranceMm * MmToFeet;
            double hGapFeet = gapSettings.GlobalHorizontalGapMm * MmToFeet;
            double vGapFeet = gapSettings.GlobalVerticalGapMm * MmToFeet;

            var largeRect = region.LargeRect;
            double containerWidthFeet = largeRect.Width;
            double containerHeightMaxFeet = largeRect.Height;

            // V014: MasterRowBandPackingService's single `gap` parameter uses
            // the Horizontal global gap for both axes, per explicit request
            // ("keep separate H/V gap fields, just feed the same value to
            // both internally") — the spec itself only supports one uniform
            // gap value, so H is the chosen source of truth here.
            var masterResult = _masterRowPacker.Pack(items, containerWidthFeet, containerHeightMaxFeet, hGapFeet, fillStrategy);

            // Convert the service's top-based (row 0 at Y=0, growing down)
            // intermediate space into sheet-space centers. Row-space Y=0
            // maps to the region's top edge (largeRect.Max.Y); Y grows
            // downward in row-space, so sheet Y = largeRect.Max.Y - rowY.
            var masterPlacements = new List<PackedViewPlacement>();
            foreach (var p in masterResult.Placed)
            {
                double wFeet = WFeet(p.Item);
                double hFeet = HFeet(p.Item);

                double sheetTopY = largeRect.Max.Y - p.Y;
                double sheetBottomY = sheetTopY - hFeet;
                double centerY = (sheetTopY + sheetBottomY) / 2.0;

                double sheetLeftX = largeRect.Min.X + p.X;
                double centerX = sheetLeftX + wFeet / 2.0;

                masterPlacements.Add(new PackedViewPlacement
                {
                    Item = p.Item,
                    NewCenter = new XYZ(centerX, centerY, 0),
                    Fits = true
                });
            }

            var remainingItems = masterResult.Unplaced;

            var rows = GroupIntoRows(remainingItems, toleranceFeet);
            foreach (var row in rows)
                row.Sort((a, b) => b.CurrentCenter.X.CompareTo(a.CurrentCenter.X));

            var laidOutRows = new List<List<PackedViewPlacement>>();
            double cursorY = masterResult.Placed.Count > 0
                ? (largeRect.Max.Y - masterResult.TotalHeightFeet)
                : region.GetOverallBounds().Max.Y;

            foreach (var row in rows)
            {
                double rowHeightFeet = row.Max(i => i.HeightMm) * MmToFeet;
                double bandTop = cursorY;
                double bandBottom = cursorY - rowHeightFeet;

                var (rangeMinX, rangeMaxX, usedRect) = region.GetUsableXRangeAtY(bandTop, bandBottom);
                double cursorX = rangeMaxX;

                var rowResults = new List<PackedViewPlacement>();

                foreach (var item in row)
                {
                    double wFeet = WFeet(item);
                    double hFeet = HFeet(item);

                    double right = cursorX;
                    double left = right - wFeet;

                    double top = rowAlignment switch
                    {
                        RowAlignment.Top => cursorY,
                        RowAlignment.Bottom => cursorY - (rowHeightFeet - hFeet),
                        _ => cursorY - (rowHeightFeet - hFeet) / 2.0
                    };
                    double bottom = top - hFeet;

                    var center = new XYZ((left + right) / 2.0, (top + bottom) / 2.0, 0);

                    rowResults.Add(new PackedViewPlacement { Item = item, NewCenter = center, Fits = true });

                    cursorX = left - hGapFeet;
                }

                laidOutRows.Add(rowResults);

                cursorY -= rowHeightFeet + vGapFeet;
            }

            var flat = masterPlacements.Concat(laidOutRows.SelectMany(r => r)).ToList();
            if (flat.Count == 0)
                return results;

            // Step 4: whole-block alignment. NOTE: in the UI, blockH/blockV
            // are now surfaced as "Column Align" per explicit request — this
            // still reuses the existing BlockAlignmentH/V mechanism
            // internally (per confirmed design), only the UI label changed.
            // (largeRect already declared above — reused here, not
            // redeclared, to avoid a duplicate-local compile error.)

            double blockMinX = flat.Min(p => p.NewCenter.X - WFeet(p.Item) / 2.0);
            double blockMaxX = flat.Max(p => p.NewCenter.X + WFeet(p.Item) / 2.0);
            double blockMinY = flat.Min(p => p.NewCenter.Y - HFeet(p.Item) / 2.0);
            double blockMaxY = flat.Max(p => p.NewCenter.Y + HFeet(p.Item) / 2.0);

            double blockWidth = blockMaxX - blockMinX;
            double blockHeight = blockMaxY - blockMinY;
            double usableWidth = largeRect.Width;
            double usableHeight = largeRect.Height;

            double shiftX = blockH switch
            {
                BlockAlignmentH.Left => largeRect.Min.X - blockMinX,
                BlockAlignmentH.Right => largeRect.Max.X - blockMaxX,
                _ => (largeRect.Min.X + usableWidth / 2.0) - (blockMinX + blockWidth / 2.0)
            };

            double shiftY = blockV switch
            {
                BlockAlignmentV.Top => largeRect.Max.Y - blockMaxY,
                BlockAlignmentV.Bottom => largeRect.Min.Y - blockMinY,
                _ => (largeRect.Min.Y + usableHeight / 2.0) - (blockMinY + blockHeight / 2.0)
            };

            double shiftedTopY = blockMaxY + shiftY;
            if (shiftedTopY > largeRect.Max.Y)
                shiftY -= (shiftedTopY - largeRect.Max.Y);

            foreach (var placement in flat)
            {
                var shifted = new XYZ(
                    placement.NewCenter.X + shiftX,
                    placement.NewCenter.Y + shiftY,
                    0);

                double halfW = WFeet(placement.Item) / 2.0;
                double halfH = HFeet(placement.Item) / 2.0;

                double itemBandTop = shifted.Y + halfH;
                double itemBandBottom = shifted.Y - halfH;
                var (fitMinX, fitMaxX, fitRect) = region.GetUsableXRangeAtY(itemBandTop, itemBandBottom);

                bool fits = (shifted.X - halfW) >= fitMinX - 1e-6
                            && (shifted.X + halfW) <= fitMaxX + 1e-6
                            && (shifted.Y - halfH) >= fitRect.Min.Y - 1e-6
                            && (shifted.Y + halfH) <= fitRect.Max.Y + 1e-6;

                results.Add(new PackedViewPlacement
                {
                    Item = placement.Item,
                    NewCenter = shifted,
                    Fits = fits
                });
            }

            // ── V013 NEW: pairwise overlap detection ────────────────────
            // The region fit-check above only verifies each view's box sits
            // within the sheet's usable area — it never checked whether two
            // views' boxes overlap EACH OTHER. This is the actual bug
            // behind views showing up on top of one another with "0
            // overflow" reported: every view could individually be inside
            // the usable region while still colliding with a neighbor.
            //
            // Resolution order (flagged, confirmed): the FIRST view in
            // `results` order (i.e. the row-packing/shelf placement
            // sequence already computed above) that occupies a given spot
            // wins and keeps Fits=true; any LATER view whose box overlaps
            // an already-accepted box gets Fits=false. This is O(n^2) —
            // fine for realistic per-sheet view counts (tens, not
            // thousands) — checked only among views that already passed
            // the region fit-check, since a view already flagged Overflow
            // for being outside the region doesn't need an overlap check.
            var accepted = new List<PackedViewPlacement>();
            foreach (var candidate in results)
            {
                if (!candidate.Fits)
                    continue; // already failed the region check — leave as Overflow, don't bother checking overlap

                var (left, right, top, bottom) = GetBounds(candidate);

                bool overlapsAccepted = accepted.Any(a =>
                {
                    var (aLeft, aRight, aTop, aBottom) = GetBounds(a);

                    // Standard axis-aligned rectangle overlap test: two
                    // rects DON'T overlap if one is entirely to the left,
                    // right, above, or below the other. A tiny tolerance
                    // (1e-6 ft ≈ 0.0003mm) avoids flagging boxes that are
                    // merely touching edge-to-edge (e.g. zero-gap packing)
                    // as a false-positive overlap.
                    bool separated = right <= aLeft + 1e-6
                                      || left >= aRight - 1e-6
                                      || bottom >= aTop - 1e-6
                                      || top <= aBottom + 1e-6;
                    return !separated;
                });

                if (overlapsAccepted)
                {
                    candidate.Fits = false;
                }
                else
                {
                    accepted.Add(candidate);
                }
            }

            // NOTE: V014 — the shelf-overflow-grouping block that used to
            // run here (dragging a Tall/Wide anchor's shelved items into
            // Overflow together when the anchor itself didn't fit) has been
            // REMOVED along with the anchor system. Every item's Fits value
            // now comes purely from the region-fit + overlap checks above —
            // there's no "block" concept left to group failures by.

            // ── V014 NEW: actually relocate overflow items below the
            // titleblock's usable area. Previously, an item marked
            // Fits=false simply kept whatever NewCenter the packer had
            // already computed for it (usually still WITHIN the usable
            // region's bounds, or overlapping another view) — the log
            // message claimed "placed below the sheet" but nothing moved
            // it there. This repacks every overflow item into simple rows
            // starting at the region's bottom edge (largeRect.Min.Y) and
            // growing further downward, left-to-right, using the same
            // gap values as everywhere else. This area is intentionally
            // unbounded (nothing below the titleblock constrains it), so
            // these items stay Fits=false (still Overflow — they didn't
            // fit ON the sheet) but now sit at real, non-overlapping
            // coordinates a user can actually find and deal with.
            var overflowItems = results.Where(r => !r.Fits).Select(r => r.Item).ToList();
            if (overflowItems.Count > 0)
            {
                double overflowCursorY = largeRect.Min.Y - vGapFeet;

                // Pack tallest-first per row, matching this file's general
                // convention elsewhere (row height = tallest item in the
                // row); simple left-to-right fill, wrapping to a new row
                // once the row would exceed the titleblock's own width so
                // the overflow group stays visually aligned under it.
                var sortedOverflow = overflowItems.OrderByDescending(i => i.HeightMm).ToList();
                var overflowRows = new List<List<ViewOnSheetItem>>();
                var currentOverflowRow = new List<ViewOnSheetItem>();
                double currentRowWidthFeet = 0.0;

                foreach (var item in sortedOverflow)
                {
                    double wFeet = WFeet(item);
                    double advance = currentOverflowRow.Count > 0 ? wFeet + hGapFeet : wFeet;

                    if (currentOverflowRow.Count > 0 && currentRowWidthFeet + advance > containerWidthFeet + 1e-6)
                    {
                        overflowRows.Add(currentOverflowRow);
                        currentOverflowRow = new List<ViewOnSheetItem>();
                        currentRowWidthFeet = 0.0;
                        advance = wFeet;
                    }

                    currentOverflowRow.Add(item);
                    currentRowWidthFeet += advance;
                }
                if (currentOverflowRow.Count > 0)
                    overflowRows.Add(currentOverflowRow);

                foreach (var row in overflowRows)
                {
                    double rowHeightFeet = row.Max(i => i.HeightMm) * MmToFeet;
                    double rowTopY = overflowCursorY;
                    double rowBottomY = rowTopY - rowHeightFeet;
                    double cursorX = largeRect.Min.X;

                    foreach (var item in row)
                    {
                        double wFeet = WFeet(item);
                        double hFeet = HFeet(item);

                        // Bottom-aligned within its overflow row, matching
                        // this service's general bottom-alignment
                        // convention (RowAlignment default, MasterRowBand
                        // strategies) — keeps shorter overflow items flush
                        // with taller neighbors instead of floating.
                        double itemBottom = rowBottomY;
                        double itemTop = itemBottom + hFeet;
                        var center = new XYZ(cursorX + wFeet / 2.0, (itemTop + itemBottom) / 2.0, 0);

                        var existing = results.First(r => r.Item.ViewportId == item.ViewportId);
                        existing.NewCenter = center;
                        // existing.Fits stays false — this is still Overflow,
                        // just relocated to a real, findable spot below the sheet.

                        cursorX += wFeet + hGapFeet;
                    }

                    overflowCursorY = rowBottomY - vGapFeet;
                }
            }

            return results;
        }

        private List<List<ViewOnSheetItem>> GroupIntoRows(List<ViewOnSheetItem> items, double toleranceFeet)
        {
            if (items.Count == 0)
                return new List<List<ViewOnSheetItem>>();

            var sorted = items.OrderByDescending(i => i.CurrentCenter.Y).ToList();

            var rows = new List<List<ViewOnSheetItem>>();
            var currentRow = new List<ViewOnSheetItem>();
            double? currentRowY = null;

            foreach (var item in sorted)
            {
                if (currentRowY == null || Math.Abs(item.CurrentCenter.Y - currentRowY.Value) <= toleranceFeet)
                {
                    currentRow.Add(item);
                    currentRowY ??= item.CurrentCenter.Y;
                }
                else
                {
                    rows.Add(currentRow);
                    currentRow = new List<ViewOnSheetItem> { item };
                    currentRowY = item.CurrentCenter.Y;
                }
            }

            if (currentRow.Count > 0)
                rows.Add(currentRow);

            return rows;
        }
    }
}
