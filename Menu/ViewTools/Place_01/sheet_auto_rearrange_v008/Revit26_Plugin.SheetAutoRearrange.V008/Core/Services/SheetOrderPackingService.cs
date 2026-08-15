using Autodesk.Revit.DB;
using Revit26_Plugin.SheetAutoRearrange.V008.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.SheetAutoRearrange.V008.Core.Services
{
    /// <summary>
    /// Default algorithm for Sheet Auto Rearrange.
    ///
    /// V007 CHANGE: if any ticked item is classified Tall/Wide/TallAndWide
    /// (per TallWideDetectionSettings), packing is dispatched to
    /// ShelfPackingService FIRST — anchors + their shelved normal views are
    /// laid out as 2D blocks, gap-distributed to the anchor's edge. Whatever
    /// normal views remain after shelving continue in the EXISTING uniform-
    /// row logic (unchanged from V006), starting below the shelved blocks'
    /// lowest Y. If no anchors are present at all, behavior is byte-for-byte
    /// identical to V006 (dispatcher short-circuits straight to the old path).
    ///
    /// Block alignment (Step 4) and the fit re-check (Step 5) still apply to
    /// the COMBINED result (shelved + normally-packed items) against
    /// LargeRect, per the original confirmed design — a shelf block's items
    /// shift by the same block delta as everything else.
    /// </summary>
    public class SheetOrderPackingService
    {
        private const double MmToFeet = 1.0 / 304.8;

        private readonly ShelfPackingService _shelfPacker = new();

        public List<PackedViewPlacement> Pack(
            List<ViewOnSheetItem> items,
            PlaceableRegion region,
            GapSettings gapSettings,
            double rowToleranceMm,
            RowAlignment rowAlignment,
            BlockAlignmentH blockH,
            BlockAlignmentV blockV,
            TallWideDetectionSettings tallSettings,
            TallWideDetectionSettings wideSettings)
        {
            var results = new List<PackedViewPlacement>();
            if (items.Count == 0)
                return results;

            double toleranceFeet = rowToleranceMm * MmToFeet;

            // ── V007: attempt shelf packing first ──────────────────────────
            var shelfResult = _shelfPacker.Pack(items, region, gapSettings, tallSettings, wideSettings);

            var shelfPlacements = new List<PackedViewPlacement>();
            foreach (var block in shelfResult.Blocks)
            {
                double halfW = block.Anchor.WidthMm * MmToFeet / 2.0;
                double halfH = block.Anchor.HeightMm * MmToFeet / 2.0;
                shelfPlacements.Add(new PackedViewPlacement
                {
                    Item = block.Anchor,
                    NewCenter = block.AnchorCenter,
                    Fits = block.AnchorFits
                });

                foreach (var row in block.ShelvedRows)
                    foreach (var kvp in row.ResolvedCenters)
                    {
                        var item = row.Items.First(i => i.ViewportId == kvp.Key);
                        shelfPlacements.Add(new PackedViewPlacement { Item = item, NewCenter = kvp.Value, Fits = true });
                    }

                foreach (var col in block.ShelvedColumns)
                    foreach (var kvp in col.ResolvedCenters)
                    {
                        var item = col.Items.First(i => i.ViewportId == kvp.Key);
                        shelfPlacements.Add(new PackedViewPlacement { Item = item, NewCenter = kvp.Value, Fits = true });
                    }
            }

            // ── Remaining normal items: EXISTING V006 uniform-row logic,
            // unchanged, starting below the shelved blocks (or at the
            // region's top if no anchors were found at all). ──
            var remainingItems = shelfResult.Blocks.Count > 0
                ? shelfResult.RemainingNormalItems
                : items; // no anchors — dispatcher short-circuits to identical V006 behavior

            var rows = GroupIntoRows(remainingItems, toleranceFeet);
            foreach (var row in rows)
                row.Sort((a, b) => b.CurrentCenter.X.CompareTo(a.CurrentCenter.X));

            var laidOutRows = new List<List<PackedViewPlacement>>();
            double cursorY = shelfResult.Blocks.Count > 0
                ? shelfResult.NextAvailableY
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
                        _ => cursorY - (rowHeightFeet - hFeet) / 2.0
                    };
                    double bottom = top - hFeet;

                    var center = new XYZ((left + right) / 2.0, (top + bottom) / 2.0, 0);

                    rowResults.Add(new PackedViewPlacement { Item = item, NewCenter = center, Fits = true });

                    cursorX = left - hGapFeet;
                }

                laidOutRows.Add(rowResults);

                var vGroup = ResolveGroup(row[0], gapSettings);
                cursorY -= rowHeightFeet + (vGroup.VerticalGapMm * MmToFeet);
            }

            var flat = shelfPlacements.Concat(laidOutRows.SelectMany(r => r)).ToList();
            if (flat.Count == 0)
                return results;

            // Step 4: whole-block alignment — unchanged from V006, now
            // applied across shelf items + normally-packed items combined.
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

            // ── V007: apply ShelfOverflowGrouping per block ────────────────
            // KeepShelfTogether: if the anchor's OWN post-shift fit-check
            // failed, force every item that was shelved beside it to Fits =
            // false too, so the whole block reads as "overflow" together —
            // even if an individual shelved view's own box happened to still
            // land inside the region after the shift.
            // RepackIndividually: no override — each item already got its
            // own independent fit-check above, exactly as if it were a
            // normal item; nothing further to do for that mode.
            foreach (var block in shelfResult.Blocks)
            {
                var grouping = block.Category == ViewSizeCategory.Wide
                    ? wideSettings.OverflowGrouping
                    : tallSettings.OverflowGrouping; // Tall and TallAndWide both key off tallSettings' grouping choice

                if (grouping != ShelfOverflowGrouping.KeepShelfTogether)
                    continue;

                var anchorResult = results.FirstOrDefault(r => r.Item.ViewportId == block.Anchor.ViewportId);
                if (anchorResult == null || anchorResult.Fits)
                    continue; // anchor fit fine — nothing to force

                var shelvedIds = block.AllItems().Select(i => i.ViewportId).ToHashSet();
                foreach (var r in results.Where(r => shelvedIds.Contains(r.Item.ViewportId)))
                    r.Fits = false;
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

        private ViewTypeGapGroup ResolveGroup(ViewOnSheetItem item, GapSettings gapSettings)
        {
            string groupName = ViewTypeGroupResolver.ToGapGroupName(item.ViewType);
            return gapSettings.Groups.FirstOrDefault(g => g.GroupName == groupName)
                   ?? gapSettings.Groups[0];
        }
    }
}
