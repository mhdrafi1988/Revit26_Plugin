using Autodesk.Revit.DB;
using Revit26_Plugin.SheetAutoRearrange.V008.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.SheetAutoRearrange.V008.Core.Services
{
    /// <summary>
    /// Packed layout result for a single view — final center point (feet, sheet
    /// space) and whether it fit within the resolved placeable region.
    /// </summary>
    public class PackedViewPlacement
    {
        public ViewOnSheetItem Item { get; set; } = null!;
        public XYZ NewCenter { get; set; } = XYZ.Zero;
        public bool Fits { get; set; }
    }

    /// <summary>
    /// Ported from Revit26_Plugin.SmartViewToSheetPlacer.V213's
    /// ReadingOrderPackingService. Sorts ticked views by reading order
    /// (top row first, left-to-right within a row) and repacks them in a
    /// two-phase row layout, honoring per-ViewType-group gap settings.
    ///
    /// V006 CHANGE: usable area is no longer a flat min/max XYZ pair — it's a
    /// PlaceableRegion (single rect, or Large+Small L-shape for corner title
    /// blocks). Each row now queries its own usable X-range from the region
    /// based on that row's Y-band, per confirmed design ("simply be
    /// constrained to the narrower available width").
    /// </summary>
    public class ReadingOrderPackingService
    {
        private const double MmToFeet = 1.0 / 304.8;

        /// <summary>
        /// Packs <paramref name="items"/> (already filtered to ticked/selected)
        /// into <paramref name="region"/>, sorted in reading order.
        /// </summary>
        public List<PackedViewPlacement> Pack(
            List<ViewOnSheetItem> items,
            PlaceableRegion region,
            GapSettings gapSettings)
        {
            var results = new List<PackedViewPlacement>();
            if (items.Count == 0)
                return results;

            // Phase 1: sort by reading order — top row first (largest Y first in
            // Revit's up-positive sheet space), then left-to-right (X ascending)
            // within a row. Row membership determined by a fixed tolerance band
            // based on the tallest item in view, consistent with V213's approach.
            double rowToleranceFeet = items.Max(i => i.HeightMm) * MmToFeet * 0.5;

            var sorted = items
                .OrderByDescending(i => i.CurrentCenter.Y)
                .ThenBy(i => i.CurrentCenter.X)
                .ToList();

            var rows = GroupIntoRows(sorted, rowToleranceFeet);

            // Phase 2: lay out each row left-to-right, top row starting at the
            // region's overall top edge, advancing downward row by row. Each
            // row's usable X-range is looked up fresh from the region based on
            // that row's Y-band (large rect, or small rect if the band falls
            // in the notch) — never a single flat range for the whole packing pass.
            double regionTopY = region.GetOverallBounds().Max.Y;
            double cursorY = regionTopY;

            foreach (var row in rows)
            {
                double rowHeightFeet = row.Max(i => i.HeightMm) * MmToFeet;
                double bandTop = cursorY;
                double bandBottom = cursorY - rowHeightFeet;

                var (rangeMinX, rangeMaxX, usedRect) = region.GetUsableXRangeAtY(bandTop, bandBottom);
                double cursorX = rangeMinX;

                foreach (var item in row)
                {
                    var group = ResolveGroup(item, gapSettings);
                    double wFeet = item.WidthMm * MmToFeet;
                    double hFeet = item.HeightMm * MmToFeet;
                    double hGapFeet = group.HorizontalGapMm * MmToFeet;

                    double left = cursorX;
                    double right = left + wFeet;
                    double top = cursorY;
                    double bottom = top - hFeet;

                    bool fits = right <= rangeMaxX + 1e-6
                                && bottom >= usedRect.Min.Y - 1e-6;

                    var center = new XYZ((left + right) / 2.0, (top + bottom) / 2.0, 0);

                    results.Add(new PackedViewPlacement
                    {
                        Item = item,
                        NewCenter = center,
                        Fits = fits
                    });

                    cursorX = right + hGapFeet;
                }

                var vGroup = ResolveGroup(row[0], gapSettings);
                cursorY -= rowHeightFeet + (vGroup.VerticalGapMm * MmToFeet);
            }

            return results;
        }

        private List<List<ViewOnSheetItem>> GroupIntoRows(List<ViewOnSheetItem> sortedItems, double toleranceFeet)
        {
            var rows = new List<List<ViewOnSheetItem>>();
            var currentRow = new List<ViewOnSheetItem>();
            double? currentRowY = null;

            foreach (var item in sortedItems)
            {
                if (currentRowY == null || Math.Abs(item.CurrentCenter.Y - currentRowY.Value) <= toleranceFeet)
                {
                    currentRow.Add(item);
                    currentRowY ??= item.CurrentCenter.Y;
                }
                else
                {
                    rows.Add(currentRow.OrderBy(i => i.CurrentCenter.X).ToList());
                    currentRow = new List<ViewOnSheetItem> { item };
                    currentRowY = item.CurrentCenter.Y;
                }
            }

            if (currentRow.Count > 0)
                rows.Add(currentRow.OrderBy(i => i.CurrentCenter.X).ToList());

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
