using Autodesk.Revit.DB;
using Revit26_Plugin.SheetAutoRearrange.V010.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.SheetAutoRearrange.V010.Core.Services
{
    /// <summary>
    /// Handles 2D "shelf" packing for tall/wide/both-flagged anchor views.
    ///
    /// V010 CHANGE: sub-row alignment (within a Tall anchor's shelved rows)
    /// and sub-column alignment (within a Wide anchor's shelved columns)
    /// are now user-configurable via TallWideDetectionSettings.SubRowAlignment
    /// and .SubColumnAlignment — previously hardcoded top-aligned /
    /// left-aligned respectively. Defaults: Bottom for rows, Right for
    /// columns, per explicit request.
    /// </summary>
    public class ShelfPackingService
    {
        private const double MmToFeet = 1.0 / 304.8;
        private const double FeetToMm = 304.8;

        public class ShelfPackResult
        {
            public List<ShelfBlock> Blocks { get; } = new();
            public double NextAvailableY { get; set; }
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
                result.NextAvailableY = region.GetOverallBounds().Max.Y;
                result.RemainingNormalItems.AddRange(normals);
                return result;
            }

            var largeRect = region.LargeRect;
            double cursorY = largeRect.Max.Y;

            var anchorQueue = new Queue<ViewOnSheetItem>(anchors);
            var normalPool = new List<ViewOnSheetItem>(normals);

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
                        anchorItem, category, region, gapSettings, tallSettings, wideSettings,
                        waveCursorX, waveTopY, normalPool);

                    result.Blocks.Add(block);

                    double anchorWFeet = anchorItem.WidthMm * MmToFeet;
                    double anchorHFeet = anchorItem.HeightMm * MmToFeet;

                    waveCursorX += anchorWFeet + (gapSettings.GlobalHorizontalGapMm * MmToFeet);
                    waveMaxSpanFeet = Math.Max(waveMaxSpanFeet, anchorHFeet);

                    if (waveCursorX > largeRect.Max.X && anchorQueue.Count > 0
                        && classification.Categories[anchorQueue.Peek()] == category)
                    {
                        waveTopY -= waveMaxSpanFeet + (gapSettings.GlobalVerticalGapMm * MmToFeet);
                        waveCursorX = largeRect.Min.X;
                        waveMaxSpanFeet = 0;
                    }
                }

                cursorY = waveTopY - waveMaxSpanFeet - (gapSettings.GlobalVerticalGapMm * MmToFeet);
            }

            result.NextAvailableY = cursorY;
            result.RemainingNormalItems.AddRange(normalPool);
            return result;
        }

        private ShelfBlock PackSingleAnchor(
            ViewOnSheetItem anchor,
            ViewSizeCategory category,
            Models.PlaceableRegion region,
            GapSettings gapSettings,
            TallWideDetectionSettings tallSettings,
            TallWideDetectionSettings wideSettings,
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

            var hGap = gapSettings.GlobalHorizontalGapMm * MmToFeet;
            var vGap = gapSettings.GlobalVerticalGapMm * MmToFeet;

            if (category == ViewSizeCategory.Tall || category == ViewSizeCategory.TallAndWide)
            {
                double shelfLeftX = originX + anchorWFeet + hGap;
                double shelfRightBound = rangeMaxX;
                PackShelfRows(block.ShelvedRows, normalPool, shelfLeftX, shelfRightBound,
                    originTopY, anchorHFeet, hGap, vGap, tallSettings.SubRowAlignment);
            }

            if (category == ViewSizeCategory.Wide || category == ViewSizeCategory.TallAndWide)
            {
                double shelfTopY = originTopY - anchorHFeet - vGap;
                double shelfBottomBound = region.LargeRect.Min.Y;
                PackShelfColumns(block.ShelvedColumns, normalPool, originX, anchorWFeet,
                    shelfTopY, shelfBottomBound, hGap, vGap, wideSettings.SubColumnAlignment);
            }

            return block;
        }

        /// <summary>
        /// Fills horizontal sub-rows beside a Tall anchor. subRowAlignment
        /// (V010 NEW) controls how each ROW's items align within that row's
        /// own natural height when items in the row differ in height —
        /// separate from DistributeRowGaps' vertical stretching of the
        /// GAPS between rows, which is unaffected by this setting.
        /// </summary>
        private void PackShelfRows(
            List<ShelfRow> shelvedRows,
            List<ViewOnSheetItem> normalPool,
            double shelfLeftX,
            double shelfRightBound,
            double topY,
            double anchorSpanFeet,
            double hGap,
            double vGap,
            RowAlignment subRowAlignment)
        {
            double anchorBottomY = topY - anchorSpanFeet;
            double cursorY = topY;

            while (normalPool.Count > 0 && cursorY > anchorBottomY + 1e-6)
            {
                var row = new ShelfRow();
                double cursorX = shelfLeftX;
                double rowMaxHFeet = 0;

                int i = 0;
                while (i < normalPool.Count)
                {
                    var candidate = normalPool[i];
                    double wFeet = candidate.WidthMm * MmToFeet;
                    double hFeet = candidate.HeightMm * MmToFeet;

                    if (cursorX + wFeet > shelfRightBound + 1e-6)
                    {
                        i++;
                        continue;
                    }

                    row.Items.Add(candidate);
                    normalPool.RemoveAt(i);
                    cursorX += wFeet + hGap;
                    rowMaxHFeet = Math.Max(rowMaxHFeet, hFeet);
                }

                if (row.Items.Count == 0)
                    break;

                row.NaturalSizeFeet = rowMaxHFeet;
                shelvedRows.Add(row);
                cursorY -= rowMaxHFeet + vGap;
            }

            DistributeRowGaps(shelvedRows, topY, anchorBottomY, shelfLeftX, hGap, vGap, subRowAlignment);
        }

        /// <summary>
        /// Gap distribution for Tall-anchor sub-rows — unchanged stretching
        /// logic (surplus space split across INTERNAL gaps only). V010
        /// CHANGE: each item's position WITHIN its own sub-row's natural
        /// height now follows subRowAlignment (Top/Center/Bottom) instead
        /// of always top-aligning — matters when a sub-row has items of
        /// different heights sharing that row.
        /// </summary>
        private void DistributeRowGaps(
            List<ShelfRow> shelvedRows,
            double topY,
            double anchorBottomY,
            double shelfLeftX,
            double hGap,
            double baseVGap,
            RowAlignment subRowAlignment)
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

                    // V010: item's vertical position within its OWN sub-row's
                    // natural height band, per subRowAlignment. When every
                    // item in the row shares the same height as the row's
                    // NaturalSizeFeet (the common case), all three modes
                    // produce an identical result — this only matters for
                    // mixed-height sub-rows.
                    double centerY = subRowAlignment switch
                    {
                        RowAlignment.Top => rowTopY - hFeet / 2.0,
                        RowAlignment.Bottom => rowBottomY + hFeet / 2.0,
                        _ => rowTopY - (row.NaturalSizeFeet - hFeet) / 2.0 - hFeet / 2.0 // Center
                    };

                    row.ResolvedCenters[item.ViewportId] = new XYZ(centerX, centerY, 0);
                    cursorX += wFeet + hGap;
                }

                double gapAfterThisRow = baseVGap + (r < shelvedRows.Count - 1 ? extraPerGap : 0);
                cursorY = rowBottomY - gapAfterThisRow;
            }
        }

        /// <summary>Mirror of PackShelfRows for Wide anchors. subColumnAlignment (V010 NEW) controls horizontal position within each sub-column's natural width.</summary>
        private void PackShelfColumns(
            List<ShelfRow> shelvedColumns,
            List<ViewOnSheetItem> normalPool,
            double leftX,
            double anchorSpanFeet,
            double topY,
            double bottomBound,
            double hGap,
            double vGap,
            BlockAlignmentH subColumnAlignment)
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

            DistributeColumnGaps(shelvedColumns, leftX, anchorRightX, topY, hGap, vGap, subColumnAlignment);
        }

        /// <summary>
        /// Gap distribution for Wide-anchor sub-columns. V010 CHANGE: each
        /// item's horizontal position WITHIN its own sub-column's natural
        /// width now follows subColumnAlignment (Left/Center/Right) instead
        /// of always left-aligning.
        /// </summary>
        private void DistributeColumnGaps(
            List<ShelfRow> shelvedColumns,
            double leftX,
            double anchorRightX,
            double topY,
            double baseHGap,
            double vGap,
            BlockAlignmentH subColumnAlignment)
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

                    // V010: item's horizontal position within its OWN
                    // sub-column's natural width band, per subColumnAlignment.
                    double centerX = subColumnAlignment switch
                    {
                        BlockAlignmentH.Left => colLeftX + wFeet / 2.0,
                        BlockAlignmentH.Right => colRightX - wFeet / 2.0,
                        _ => colLeftX + (col.NaturalSizeFeet - wFeet) / 2.0 + wFeet / 2.0 // Center
                    };

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
