using Autodesk.Revit.DB;
using Revit26_Plugin.SheetAutoRearrange.V002.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.SheetAutoRearrange.V002.Core.Services
{
    /// <summary>
    /// Default algorithm for Sheet Auto Rearrange. Groups ticked views into
    /// rows by their CURRENT vertical position on the sheet (within a
    /// user-defined tolerance), places the topmost row first, orders each
    /// row right-to-left, then repacks — applying per-row alignment
    /// (Top/Center/Bottom) and whole-block alignment (H: Left/Center/Right,
    /// V: Top/Center/Bottom) against the titleblock usable area.
    /// </summary>
    public class SheetOrderPackingService
    {
        private const double MmToFeet = 1.0 / 304.8;

        public List<PackedViewPlacement> Pack(
            List<ViewOnSheetItem> items,
            XYZ usableAreaMinFeet,
            XYZ usableAreaMaxFeet,
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

            // Step 3: lay out rows top-down starting at the usable area's top,
            // each row placed right-to-left, applying per-row alignment for
            // any height difference within the row.
            var laidOutRows = new List<List<PackedViewPlacement>>();
            double cursorY = usableAreaMaxFeet.Y;

            foreach (var row in rows)
            {
                double rowHeightFeet = row.Max(i => i.HeightMm) * MmToFeet;
                double cursorX = usableAreaMaxFeet.X; // start from the right edge

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
                        Fits = true // fit re-evaluated after block alignment shift, below
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
            // whole block per BlockAlignmentH / BlockAlignmentV against the
            // titleblock usable area.
            double blockMinX = flat.Min(p => p.NewCenter.X - (p.Item.WidthMm * MmToFeet) / 2.0);
            double blockMaxX = flat.Max(p => p.NewCenter.X + (p.Item.WidthMm * MmToFeet) / 2.0);
            double blockMinY = flat.Min(p => p.NewCenter.Y - (p.Item.HeightMm * MmToFeet) / 2.0);
            double blockMaxY = flat.Max(p => p.NewCenter.Y + (p.Item.HeightMm * MmToFeet) / 2.0);

            double blockWidth = blockMaxX - blockMinX;
            double blockHeight = blockMaxY - blockMinY;
            double usableWidth = usableAreaMaxFeet.X - usableAreaMinFeet.X;
            double usableHeight = usableAreaMaxFeet.Y - usableAreaMinFeet.Y;

            double shiftX = blockH switch
            {
                BlockAlignmentH.Left => usableAreaMinFeet.X - blockMinX,
                BlockAlignmentH.Right => usableAreaMaxFeet.X - blockMaxX,
                _ => (usableAreaMinFeet.X + usableWidth / 2.0) - (blockMinX + blockWidth / 2.0) // Center
            };

            double shiftY = blockV switch
            {
                BlockAlignmentV.Top => usableAreaMaxFeet.Y - blockMaxY,
                BlockAlignmentV.Bottom => usableAreaMinFeet.Y - blockMinY,
                _ => (usableAreaMinFeet.Y + usableHeight / 2.0) - (blockMinY + blockHeight / 2.0) // Center
            };

            // CONFIRMED: overflow must always spill below the sheet's bottom
            // edge, never above its top, regardless of BlockV alignment. If
            // the packed block is taller than the usable area, the alignment
            // shift above could otherwise push the block's TOP past the
            // usable area's top (e.g. BlockV=Bottom with a tall block). Clamp
            // shiftY so the block's top never exceeds the usable area's top —
            // any excess height is then forced downward past the bottom edge
            // instead, which is where PackedViewPlacement.Fits already
            // expects overflow to land.
            double shiftedTopY = blockMaxY + shiftY;
            if (shiftedTopY > usableAreaMaxFeet.Y)
                shiftY -= (shiftedTopY - usableAreaMaxFeet.Y);

            foreach (var placement in flat)
            {
                var shifted = new XYZ(
                    placement.NewCenter.X + shiftX,
                    placement.NewCenter.Y + shiftY,
                    0);

                double halfW = (placement.Item.WidthMm * MmToFeet) / 2.0;
                double halfH = (placement.Item.HeightMm * MmToFeet) / 2.0;

                bool fits = (shifted.X - halfW) >= usableAreaMinFeet.X - 1e-6
                            && (shifted.X + halfW) <= usableAreaMaxFeet.X + 1e-6
                            && (shifted.Y - halfH) >= usableAreaMinFeet.Y - 1e-6
                            && (shifted.Y + halfH) <= usableAreaMaxFeet.Y + 1e-6;

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
