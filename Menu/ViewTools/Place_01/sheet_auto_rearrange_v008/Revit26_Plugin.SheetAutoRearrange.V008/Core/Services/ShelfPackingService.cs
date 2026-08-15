using Autodesk.Revit.DB;
using Revit26_Plugin.SheetAutoRearrange.V008.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.SheetAutoRearrange.V008.Core.Services
{
    /// <summary>
    /// Handles 2D "shelf" packing for tall/wide/both-flagged anchor views:
    ///   - Tall anchor: claims a multi-row-band footprint (full anchor height,
    ///     one column-width). Normal views shelf beside it in horizontal
    ///     sub-rows, gap-distributed so the LAST sub-row's bottom edge lands
    ///     exactly on the anchor's bottom edge (per confirmed design).
    ///   - Wide anchor: mirrored — claims a multi-column-band footprint.
    ///     Normal views stack beside it in vertical sub-columns, gap-
    ///     distributed so the RIGHTMOST sub-column's right edge lands on the
    ///     anchor's right edge.
    ///   - TallAndWide anchor: both fills combine to cover the L-shaped
    ///     remainder around the anchor (ShelfBlock.ShelvedRows +
    ///     ShelvedColumns).
    ///   - Same-category anchors ("same size range") are placed side by side
    ///     first, wrapping to the next row once the region's width is used.
    ///
    /// Anything left over after all anchor blocks are placed is returned
    /// separately so the caller (SheetOrderPackingService) can continue
    /// ordinary uniform-row packing below/around the shelved blocks.
    /// </summary>
    public class ShelfPackingService
    {
        private const double MmToFeet = 1.0 / 304.8;
        private const double FeetToMm = 304.8;

        public class ShelfPackResult
        {
            public List<ShelfBlock> Blocks { get; } = new();

            /// <summary>Y coordinate (feet, sheet space) where the region's remaining unshelved space begins — the caller resumes ordinary row packing at/below this line.</summary>
            public double NextAvailableY { get; set; }

            /// <summary>Normal views NOT consumed by any shelf (either there were no anchors, or leftover items didn't fit any shelf) — caller packs these normally.</summary>
            public List<ViewOnSheetItem> RemainingNormalItems { get; } = new();
        }

        private readonly ViewSizeClassifierService _classifier = new();

        public ShelfPackResult Pack(
            List<ViewOnSheetItem> tickedItems,
            Models.PlaceableRegion region,
            GapSettings gapSettings,
            TallWideDetectionSettings tallSettings,
            TallWideDetectionSettings wideSettings)
        {
            var result = new ShelfPackResult();

            var classification = _classifier.Classify(tickedItems, tallSettings, wideSettings);

            var anchors = tickedItems
                .Where(i => classification.Categories[i] != ViewSizeCategory.Normal)
                .OrderByDescending(i => i.CurrentCenter.Y)
                .ThenBy(i => i.CurrentCenter.X)
                .ToList();

            var normals = tickedItems
                .Where(i => classification.Categories[i] == ViewSizeCategory.Normal)
                .ToList();

            if (anchors.Count == 0)
            {
                // No tall/wide views in this ticked set — nothing to shelf,
                // caller's ordinary row packer handles everything.
                result.NextAvailableY = region.GetOverallBounds().Max.Y;
                result.RemainingNormalItems.AddRange(normals);
                return result;
            }

            var largeRect = region.LargeRect;
            double cursorY = largeRect.Max.Y;
            double cursorX = largeRect.Min.X;

            var anchorQueue = new Queue<ViewOnSheetItem>(anchors);
            var normalPool = new List<ViewOnSheetItem>(normals);

            // Anchors are grouped into placement "waves": consecutive anchors
            // of the SAME category are placed side by side (confirmed: "if
            // multiple tall views found has same range size place the one
            // after one"), wrapping to a new row once width runs out. A
            // category change starts a fresh wave/row.
            while (anchorQueue.Count > 0)
            {
                var category = classification.Categories[anchorQueue.Peek()];
                var wave = new List<ViewOnSheetItem>();

                while (anchorQueue.Count > 0 && classification.Categories[anchorQueue.Peek()] == category)
                    wave.Add(anchorQueue.Dequeue());

                double waveTopY = cursorY;
                double waveCursorX = largeRect.Min.X;
                double waveMaxSpanFeet = 0;

                foreach (var anchorItem in wave)
                {
                    var block = PackSingleAnchor(
                        anchorItem, category, region, gapSettings,
                        waveCursorX, waveTopY, normalPool);

                    result.Blocks.Add(block);

                    double anchorWFeet = anchorItem.WidthMm * MmToFeet;
                    double anchorHFeet = anchorItem.HeightMm * MmToFeet;

                    waveCursorX += anchorWFeet + (gapSettings.Groups[0].HorizontalGapMm * MmToFeet);
                    waveMaxSpanFeet = Math.Max(waveMaxSpanFeet, anchorHFeet);

                    // Wrap this wave to a new row if the next same-category
                    // anchor would exceed the region's right edge.
                    if (waveCursorX > largeRect.Max.X && anchorQueue.Count > 0
                        && classification.Categories[anchorQueue.Peek()] == category)
                    {
                        waveTopY -= waveMaxSpanFeet + (gapSettings.Groups[0].VerticalGapMm * MmToFeet);
                        waveCursorX = largeRect.Min.X;
                        waveMaxSpanFeet = 0;
                    }
                }

                cursorY = waveTopY - waveMaxSpanFeet - (gapSettings.Groups[0].VerticalGapMm * MmToFeet);
            }

            result.NextAvailableY = cursorY;
            result.RemainingNormalItems.AddRange(normalPool); // whatever wasn't consumed by any shelf
            return result;
        }

        /// <summary>
        /// Places one anchor at (originX, originTopY) and fills the space
        /// beside/below it with normal views drawn from normalPool (items
        /// used are removed from the pool). Returns the resolved ShelfBlock.
        /// </summary>
        private ShelfBlock PackSingleAnchor(
            ViewOnSheetItem anchor,
            ViewSizeCategory category,
            Models.PlaceableRegion region,
            GapSettings gapSettings,
            double originX,
            double originTopY,
            List<ViewOnSheetItem> normalPool)
        {
            double anchorWFeet = anchor.WidthMm * MmToFeet;
            double anchorHFeet = anchor.HeightMm * MmToFeet;

            var block = new ShelfBlock { Anchor = anchor, Category = category };

            double anchorCenterX = originX + anchorWFeet / 2.0;
            double anchorCenterY = originTopY - anchorHFeet / 2.0;
            block.AnchorCenter = new XYZ(anchorCenterX, anchorCenterY, 0);

            var (rangeMinX, rangeMaxX, usedRect) = region.GetUsableXRangeAtY(originTopY, originTopY - anchorHFeet);
            block.AnchorFits = (originX >= rangeMinX - 1e-6) && (originX + anchorWFeet <= rangeMaxX + 1e-6)
                                && (originTopY - anchorHFeet >= usedRect.Min.Y - 1e-6);

            var hGap = gapSettings.Groups[0].HorizontalGapMm * MmToFeet;
            var vGap = gapSettings.Groups[0].VerticalGapMm * MmToFeet;

            if (category == ViewSizeCategory.Tall || category == ViewSizeCategory.TallAndWide)
            {
                double shelfLeftX = originX + anchorWFeet + hGap;
                double shelfRightBound = rangeMaxX;
                PackShelfRows(block.ShelvedRows, normalPool, shelfLeftX, shelfRightBound,
                    originTopY, anchorHFeet, hGap, vGap);
            }

            if (category == ViewSizeCategory.Wide || category == ViewSizeCategory.TallAndWide)
            {
                // Wide fill sits BELOW the anchor (mirrors Tall's "beside" as
                // "below", since a Wide anchor's long axis is horizontal) —
                // for TallAndWide, this covers the remaining L-shape area
                // beneath the anchor's row-band, to the left of the Tall
                // fill's column, per the L-shape split confirmed earlier.
                double shelfTopY = originTopY - anchorHFeet - vGap;
                double shelfBottomBound = region.LargeRect.Min.Y;
                PackShelfColumns(block.ShelvedColumns, normalPool, originX, anchorWFeet,
                    shelfTopY, shelfBottomBound, hGap, vGap);
            }

            return block;
        }

        /// <summary>
        /// Fills horizontal sub-rows beside a Tall anchor. Each sub-row packs
        /// left-to-right until shelfRightBound, then wraps to the next
        /// sub-row below. After all sub-rows are built, gap distribution
        /// stretches ONLY the internal vertical gaps BETWEEN sub-rows so the
        /// last sub-row's bottom edge lands exactly on the anchor's bottom
        /// edge (originTopY - anchorSpanFeet) — never compressing below the
        /// configured minimum vGap, never resizing views themselves.
        /// </summary>
        private void PackShelfRows(
            List<ShelfRow> shelvedRows,
            List<ViewOnSheetItem> normalPool,
            double shelfLeftX,
            double shelfRightBound,
            double topY,
            double anchorSpanFeet,
            double hGap,
            double vGap)
        {
            double anchorBottomY = topY - anchorSpanFeet;
            double cursorY = topY;

            while (normalPool.Count > 0 && cursorY > anchorBottomY + 1e-6)
            {
                var row = new ShelfRow();
                double cursorX = shelfLeftX;
                double rowMaxHFeet = 0;

                // Greedily take items from the pool that still fit this row's
                // remaining width — items are consumed in pool order
                // (original reading-order queue), not re-sorted by size.
                int i = 0;
                while (i < normalPool.Count)
                {
                    var candidate = normalPool[i];
                    double wFeet = candidate.WidthMm * MmToFeet;
                    double hFeet = candidate.HeightMm * MmToFeet;

                    if (cursorX + wFeet > shelfRightBound + 1e-6)
                    {
                        i++;
                        continue; // doesn't fit this row's remaining width — try the next pool item
                    }

                    row.Items.Add(candidate);
                    normalPool.RemoveAt(i);
                    cursorX += wFeet + hGap;
                    rowMaxHFeet = Math.Max(rowMaxHFeet, hFeet);
                    // do not increment i — list shrank, next candidate is now at i
                }

                if (row.Items.Count == 0)
                    break; // nothing in the pool fits this row's width at all — stop, leave remainder for normal packing

                row.NaturalSizeFeet = rowMaxHFeet;
                shelvedRows.Add(row);
                cursorY -= rowMaxHFeet + vGap;
            }

            DistributeRowGaps(shelvedRows, topY, anchorBottomY, shelfLeftX, hGap, vGap);
        }

        /// <summary>
        /// Gap distribution for Tall-anchor sub-rows: the natural stack
        /// (sum of row heights + minimum vGap between them) is compared
        /// against the anchor's span. If natural stack is smaller, the
        /// surplus is split evenly across the INTERNAL gaps only (between
        /// row 1↔2, 2↔3, etc. — never before row 1 or after the last row),
        /// so the first row's top stays at the anchor's top and the last
        /// row's bottom lands exactly on the anchor's bottom. If the natural
        /// stack is already ≥ the anchor's span, no distribution happens —
        /// the stack overflows past the anchor's bottom (Place What's
        /// Placeable fallback, flagged as expected behavior).
        /// </summary>
        private void DistributeRowGaps(
            List<ShelfRow> shelvedRows,
            double topY,
            double anchorBottomY,
            double shelfLeftX,
            double hGap,
            double baseVGap)
        {
            if (shelvedRows.Count == 0)
                return;

            double anchorSpanFeet = topY - anchorBottomY;
            double naturalStackFeet = shelvedRows.Sum(r => r.NaturalSizeFeet)
                                       + (shelvedRows.Count - 1) * baseVGap;

            double extraGapTotal = Math.Max(0, anchorSpanFeet - naturalStackFeet);
            int internalGapCount = Math.Max(1, shelvedRows.Count - 1);
            double extraPerGap = shelvedRows.Count > 1 ? extraGapTotal / internalGapCount : 0;

            double cursorY = topY;
            for (int r = 0; r < shelvedRows.Count; r++)
            {
                var row = shelvedRows[r];
                double rowTopY = cursorY;
                double rowBottomY = rowTopY - row.NaturalSizeFeet;

                double cursorX = shelfLeftX;
                foreach (var item in row.Items)
                {
                    double wFeet = item.WidthMm * MmToFeet;
                    double hFeet = item.HeightMm * MmToFeet;
                    double centerX = cursorX + wFeet / 2.0;
                    double centerY = rowTopY - hFeet / 2.0; // each item top-aligned within its own sub-row
                    row.ResolvedCenters[item.ViewportId] = new XYZ(centerX, centerY, 0);
                    cursorX += wFeet + hGap;
                }

                double gapAfterThisRow = baseVGap + (r < shelvedRows.Count - 1 ? extraPerGap : 0);
                cursorY = rowBottomY - gapAfterThisRow;
            }
        }

        /// <summary>Mirror of PackShelfRows for Wide anchors — vertical sub-columns, gap-distributed so the rightmost sub-column's right edge lands on the anchor's right edge.</summary>
        private void PackShelfColumns(
            List<ShelfRow> shelvedColumns,
            List<ViewOnSheetItem> normalPool,
            double leftX,
            double anchorSpanFeet,
            double topY,
            double bottomBound,
            double hGap,
            double vGap)
        {
            double anchorRightX = leftX + anchorSpanFeet;
            double cursorX = leftX;

            while (normalPool.Count > 0 && cursorX < anchorRightX - 1e-6)
            {
                var col = new ShelfRow();
                double cursorY = topY;
                double colMaxWFeet = 0;

                int i = 0;
                while (i < normalPool.Count)
                {
                    var candidate = normalPool[i];
                    double wFeet = candidate.WidthMm * MmToFeet;
                    double hFeet = candidate.HeightMm * MmToFeet;

                    if (cursorY - hFeet < bottomBound - 1e-6)
                    {
                        i++;
                        continue;
                    }

                    col.Items.Add(candidate);
                    normalPool.RemoveAt(i);
                    cursorY -= hFeet + vGap;
                    colMaxWFeet = Math.Max(colMaxWFeet, wFeet);
                }

                if (col.Items.Count == 0)
                    break;

                col.NaturalSizeFeet = colMaxWFeet;
                shelvedColumns.Add(col);
                cursorX += colMaxWFeet + hGap;
            }

            DistributeColumnGaps(shelvedColumns, leftX, anchorRightX, topY, hGap, vGap);
        }

        /// <summary>Gap distribution for Wide-anchor sub-columns — mirrors DistributeRowGaps, stretching horizontal internal gaps so the rightmost column's right edge lands on the anchor's right edge.</summary>
        private void DistributeColumnGaps(
            List<ShelfRow> shelvedColumns,
            double leftX,
            double anchorRightX,
            double topY,
            double baseHGap,
            double vGap)
        {
            if (shelvedColumns.Count == 0)
                return;

            double anchorSpanFeet = anchorRightX - leftX;
            double naturalStackFeet = shelvedColumns.Sum(c => c.NaturalSizeFeet)
                                       + (shelvedColumns.Count - 1) * baseHGap;

            double extraGapTotal = Math.Max(0, anchorSpanFeet - naturalStackFeet);
            int internalGapCount = Math.Max(1, shelvedColumns.Count - 1);
            double extraPerGap = shelvedColumns.Count > 1 ? extraGapTotal / internalGapCount : 0;

            double cursorX = leftX;
            for (int c = 0; c < shelvedColumns.Count; c++)
            {
                var col = shelvedColumns[c];
                double colLeftX = cursorX;
                double colRightX = colLeftX + col.NaturalSizeFeet;

                double cursorY = topY;
                foreach (var item in col.Items)
                {
                    double wFeet = item.WidthMm * MmToFeet;
                    double hFeet = item.HeightMm * MmToFeet;
                    double centerX = colLeftX + wFeet / 2.0; // each item left-aligned within its own sub-column
                    double centerY = cursorY - hFeet / 2.0;
                    col.ResolvedCenters[item.ViewportId] = new XYZ(centerX, centerY, 0);
                    cursorY -= hFeet + vGap;
                }

                double gapAfterThisCol = baseHGap + (c < shelvedColumns.Count - 1 ? extraPerGap : 0);
                cursorX = colRightX + gapAfterThisCol;
            }
        }
    }
}
