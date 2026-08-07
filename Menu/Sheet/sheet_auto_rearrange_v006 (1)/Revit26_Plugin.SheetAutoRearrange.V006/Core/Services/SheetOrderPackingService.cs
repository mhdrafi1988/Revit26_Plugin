using Autodesk.Revit.DB;
using Revit26_Plugin.SheetAutoRearrange.V006.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.SheetAutoRearrange.V006.Core.Services
{
    /// <summary>
    /// Default algorithm for Sheet Auto Rearrange. Groups ticked views into
    /// rows by their CURRENT vertical position on the sheet (within a
    /// user-defined tolerance), places the topmost row first, orders each
    /// row right-to-left, then repacks — applying per-row alignment
    /// (Top/Center/Bottom) and whole-block alignment (H: Left/Center/Right,
    /// V: Top/Center/Bottom).
    ///
    /// V006 CHANGE: usable area is now a PlaceableRegion (single rect, or
    /// Large+Small L-shape). Each row's right-to-left cursor starts from
    /// whichever sub-rect that row's Y-band falls in (confirmed: rows
    /// constrained to the narrower width, never spilling across sub-rects).
    /// Whole-block alignment (Step 4) is computed and applied against
    /// LargeRect ONLY, per confirmed design — for single-rect regions,
    /// LargeRect IS the whole usable area, so behavior there is unchanged
    /// from V002.
    /// </summary>
    public class SheetOrderPackingService
    {
        private const double MmToFeet = 1.0 / 304.8;

        public List<PackedViewPlacement> Pack(
            List<ViewOnSheetItem> items,
            PlaceableRegion region,
            GapSettings gapSettings,
            double rowToleranceMm,
            RowAlignment rowAlignment,
            BlockAlignmentH blockH,
            BlockAlignmentV blockV)
        {
            var results = new List<PackedViewPlacement>();
            if (items.Count == 0)
                return results;

            double toleranceFeet = rowToleranceMm * MmToFeet;

            // Step 1: group into rows by current Y position, tallest-Y (topmost) first.
            var rows = GroupIntoRows(items, toleranceFeet);

            // Step 2: within each row, order right-to-left (X descending).
            foreach (var row in rows)
                row.Sort((a, b) => b.CurrentCenter.X.CompareTo(a.CurrentCenter.X));

            // Step 3: lay out rows top-down starting at the region's overall top,
            // each row placed right-to-left within whichever sub-rect its Y-band
            // falls in, applying per-row alignment for any height difference
            // within the row.
            var laidOutRows = new List<List<PackedViewPlacement>>();
            double cursorY = region.GetOverallBounds().Max.Y;

            foreach (var row in rows)
            {
                double rowHeightFeet = row.Max(i => i.HeightMm) * MmToFeet;
                double bandTop = cursorY;
                double bandBottom = cursorY - rowHeightFeet;

                var (rangeMinX, rangeMaxX, usedRect) = region.GetUsableXRangeAtY(bandTop, bandBottom);
                double cursorX = rangeMaxX; // start from the right edge of this row's usable rect

                var rowResults = new List<PackedViewPlacement>();

                foreach (var item in row)
                {
                    var group = ResolveGroup(item, gapSettings);
                    double wFeet = item.WidthMm * MmToFeet;
                    double hFeet = item.HeightMm * MmToFeet;
                    double hGapFeet = group.HorizontalGapMm * MmToFeet;

                    double right = cursorX;
                    double left = right - wFeet;

                    double top = rowAlignment switch
                    {
                        RowAlignment.Top => cursorY,
                        RowAlignment.Bottom => cursorY - (rowHeightFeet - hFeet),
                        _ => cursorY - (rowHeightFeet - hFeet) / 2.0 // Center
                    };
                    double bottom = top - hFeet;

                    var center = new XYZ((left + right) / 2.0, (top + bottom) / 2.0, 0);

                    rowResults.Add(new PackedViewPlacement
                    {
                        Item = item,
                        NewCenter = center,
                        Fits = true // fit re-evaluated per-row-rect below, after block alignment shift
                    });

                    cursorX = left - hGapFeet;
                }

                laidOutRows.Add(rowResults);

                var vGroup = ResolveGroup(row[0], gapSettings);
                cursorY -= rowHeightFeet + (vGroup.VerticalGapMm * MmToFeet);
            }

            var flat = laidOutRows.SelectMany(r => r).ToList();
            if (flat.Count == 0)
                return results;

            // Step 4: compute the packed block's bounding box, then shift the
            // whole block per BlockAlignmentH / BlockAlignmentV against
            // region.LargeRect ONLY — confirmed design. For single-rect
            // regions (RightEdge/BottomEdge/Manual), LargeRect is the entire
            // usable area, so this exactly matches V002 behavior. For Corner
            // regions, items that were packed into SmallRect still get
            // shifted by the same block delta as everything else; their
            // fit-check afterward is evaluated against whichever rect their
            // row actually used (see Step 5), not against LargeRect.
            var largeRect = region.LargeRect;

            double blockMinX = flat.Min(p => p.NewCenter.X - (p.Item.WidthMm * MmToFeet) / 2.0);
            double blockMaxX = flat.Max(p => p.NewCenter.X + (p.Item.WidthMm * MmToFeet) / 2.0);
            double blockMinY = flat.Min(p => p.NewCenter.Y - (p.Item.HeightMm * MmToFeet) / 2.0);
            double blockMaxY = flat.Max(p => p.NewCenter.Y + (p.Item.HeightMm * MmToFeet) / 2.0);

            double blockWidth = blockMaxX - blockMinX;
            double blockHeight = blockMaxY - blockMinY;
            double usableWidth = largeRect.Width;
            double usableHeight = largeRect.Height;

            double shiftX = blockH switch
            {
                BlockAlignmentH.Left => largeRect.Min.X - blockMinX,
                BlockAlignmentH.Right => largeRect.Max.X - blockMaxX,
                _ => (largeRect.Min.X + usableWidth / 2.0) - (blockMinX + blockWidth / 2.0) // Center
            };

            double shiftY = blockV switch
            {
                BlockAlignmentV.Top => largeRect.Max.Y - blockMaxY,
                BlockAlignmentV.Bottom => largeRect.Min.Y - blockMinY,
                _ => (largeRect.Min.Y + usableHeight / 2.0) - (blockMinY + blockHeight / 2.0) // Center
            };

            // CONFIRMED (carried over from V002): overflow must always spill
            // below the sheet's bottom edge, never above its top. Clamp shiftY
            // so the block's top never exceeds LargeRect's top; any excess
            // height is forced downward past the bottom edge instead.
            double shiftedTopY = blockMaxY + shiftY;
            if (shiftedTopY > largeRect.Max.Y)
                shiftY -= (shiftedTopY - largeRect.Max.Y);

            // Step 5: apply the shift, then re-evaluate Fits per item against
            // whichever sub-rect that item's ORIGINAL row band used (per
            // confirmed design: "fit-check whichever sub-rect the item's row
            // band falls in"). We captured usedRect per row above but flattened
            // it away — recompute the row->rect mapping here via each item's
            // pre-shift Y band, which is stable since shiftY/shiftX are uniform
            // across the whole block.
            foreach (var placement in flat)
            {
                var shifted = new XYZ(
                    placement.NewCenter.X + shiftX,
                    placement.NewCenter.Y + shiftY,
                    0);

                double halfW = (placement.Item.WidthMm * MmToFeet) / 2.0;
                double halfH = (placement.Item.HeightMm * MmToFeet) / 2.0;

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

            return results;
        }

        /// <summary>
        /// Groups items into rows by current Y position (topmost first).
        /// CONFIRMED SEMANTICS: tolerance is anchored to the FIRST item added
        /// to each row — every subsequent item is compared against that
        /// anchor, not against its immediate neighbor. A chain of small
        /// pairwise gaps can therefore still split into separate rows if the
        /// cumulative drift from the row's first item exceeds the tolerance,
        /// even though each neighbor pair individually falls within it.
        /// </summary>
        private List<List<ViewOnSheetItem>> GroupIntoRows(List<ViewOnSheetItem> items, double toleranceFeet)
        {
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

        private ViewTypeGapGroup ResolveGroup(ViewOnSheetItem item, GapSettings gapSettings)
        {
            string groupName = ViewTypeGroupResolver.ToGapGroupName(item.ViewType);
            return gapSettings.Groups.FirstOrDefault(g => g.GroupName == groupName)
                   ?? gapSettings.Groups[0];
        }
    }
}
