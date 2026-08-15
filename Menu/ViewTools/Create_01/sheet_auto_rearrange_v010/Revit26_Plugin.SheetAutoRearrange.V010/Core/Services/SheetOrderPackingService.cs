using Autodesk.Revit.DB;
using Revit26_Plugin.SheetAutoRearrange.V010.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.SheetAutoRearrange.V010.Core.Services
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
            double hGapFeet = gapSettings.GlobalHorizontalGapMm * MmToFeet;
            double vGapFeet = gapSettings.GlobalVerticalGapMm * MmToFeet;

            var shelfResult = _shelfPacker.Pack(items, region, gapSettings, tallSettings, wideSettings);

            var shelfPlacements = new List<PackedViewPlacement>();
            foreach (var block in shelfResult.Blocks)
            {
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

            var remainingItems = shelfResult.Blocks.Count > 0
                ? shelfResult.RemainingNormalItems
                : items;

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
                    double wFeet = item.WidthMm * MmToFeet;
                    double hFeet = item.HeightMm * MmToFeet;

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

            var flat = shelfPlacements.Concat(laidOutRows.SelectMany(r => r)).ToList();
            if (flat.Count == 0)
                return results;

            // Step 4: whole-block alignment. NOTE: in the UI, blockH/blockV
            // are now surfaced as "Column Align" per explicit request — this
            // still reuses the existing BlockAlignmentH/V mechanism
            // internally (per confirmed design), only the UI label changed.
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

            foreach (var block in shelfResult.Blocks)
            {
                var grouping = block.Category == ViewSizeCategory.Wide
                    ? wideSettings.OverflowGrouping
                    : tallSettings.OverflowGrouping;

                if (grouping != ShelfOverflowGrouping.KeepShelfTogether)
                    continue;

                var anchorResult = results.FirstOrDefault(r => r.Item.ViewportId == block.Anchor.ViewportId);
                if (anchorResult == null || anchorResult.Fits)
                    continue;

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
    }
}
