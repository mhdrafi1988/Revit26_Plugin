using Autodesk.Revit.DB;
using Revit26_Plugin.SmartViewToSheetPlacer.V221.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.SmartViewToSheetPlacer.V221.Services
{
    /// <summary>
    /// V220 PORT from SheetAutoRearrange V022 (verbatim algorithm, retargeted
    /// from ViewOnSheetItem to this tool's ViewInfo model — ViewInfo.ViewId
    /// stands in for ViewOnSheetItem.ViewportId as the unique identity key).
    ///
    /// Implements "Rafi's Algo" as a Master Row -> Column -> Sub-Row ->
    /// Sub-Column recursive climb — see the original SheetAutoRearrange
    /// V022 class for the full confirmed mechanic breakdown (Master Row
    /// height source; column width source; sub-row width scope; side-by-
    /// side sub-row packing; recursion stop condition; next-column start
    /// state). Unmodified here beyond the ViewOnSheetItem -> ViewInfo
    /// retarget.
    ///
    /// Called on one sheet's worth of candidate views at a time (already
    /// selected by ReadingOrderPackingService's area-sum capacity estimate);
    /// Unplaced items are looped back into the next sheet's candidate pool
    /// by the caller, same as the other two ported services.
    /// </summary>
    public class RafisAlgoPackingService
    {
        private const double MmToFeet = 1.0 / 304.8;

        public class Placement
        {
            public ViewInfo Item { get; set; } = null!;
            /// <summary>Top-left X, sheet space feet, relative to the region origin.</summary>
            public double X { get; set; }
            /// <summary>Top-left Y (top-based, 0 = region top, growing downward in this intermediate space).</summary>
            public double Y { get; set; }
        }

        public class Result
        {
            public List<Placement> Placed { get; set; } = new();
            public List<ViewInfo> Unplaced { get; set; } = new();
            /// <summary>Total row-space height consumed (top-based), feet.</summary>
            public double TotalHeightFeet { get; set; }
        }

        public Result Pack(List<ViewInfo> items, double containerWidthFeet, double containerHeightMaxFeet, double hGapFeet, double vGapFeet)
        {
            var remaining = new List<ViewInfo>(items);
            var placed = new List<Placement>();
            var placedIds = new HashSet<ElementId>();
            double baseline = 0.0; // top-based: 0 = region top, grows downward as Master Rows are consumed

            while (remaining.Count > 0)
            {
                // ── MASTER ROW setup: height = tallest item still remaining anywhere ──
                double rowHeight = remaining.Max(i => i.HeightMm) * MmToFeet;
                double rowTopY = baseline;
                double rowBottomY = baseline + rowHeight;

                if (rowBottomY > containerHeightMaxFeet + 1e-9)
                    break; // no vertical space left for even this row — remaining -> caller loops into next sheet

                double cursorX = 0.0;
                bool placedAnyThisRow = false;

                // ── COLUMN loop: walk left-to-right across the row ──
                while (cursorX < containerWidthFeet - 1e-9)
                {
                    // Tier 1 — COLUMN: tallest remaining item that fits (width, height) at this X.
                    var columnAnchor = remaining
                        .Where(i => !placedIds.Contains(i.ViewId))
                        .Where(i => (i.WidthMm * MmToFeet) <= (containerWidthFeet - cursorX) + 1e-9
                                 && (i.HeightMm * MmToFeet) <= rowHeight + 1e-9)
                        .OrderByDescending(i => i.HeightMm)
                        .FirstOrDefault();

                    if (columnAnchor == null)
                        break; // nothing left fits this row at this X — close the row

                    double columnWidth = columnAnchor.WidthMm * MmToFeet;
                    double anchorHeight = columnAnchor.HeightMm * MmToFeet;

                    // Place the anchor bottom-aligned to the row's bottom edge.
                    placed.Add(new Placement { Item = columnAnchor, X = cursorX, Y = rowBottomY - anchorHeight });
                    placedIds.Add(columnAnchor.ViewId);
                    placedAnyThisRow = true;

                    // Tiers 2+ — SUB-ROW / SUB-COLUMN CLIMB: from the anchor's top edge upward to
                    // the Master Row's top edge, repeatedly filling a sub-row band within the SAME
                    // columnWidth budget. Each band packs items SIDE BY SIDE.
                    double climbTopY = rowBottomY - anchorHeight;

                    while (climbTopY > rowTopY + 1e-9)
                    {
                        double availableHeight = climbTopY - rowTopY;

                        double subCursorX = cursorX;
                        double bandRemainingWidth = columnWidth;
                        double bandHeight = 0.0;
                        bool placedAnyInBand = false;

                        var bandCandidates = remaining
                            .Where(i => !placedIds.Contains(i.ViewId))
                            .Where(i => (i.HeightMm * MmToFeet) <= availableHeight + 1e-9)
                            .OrderByDescending(i => i.HeightMm)
                            .ToList();

                        foreach (var candidate in bandCandidates)
                        {
                            double cw = candidate.WidthMm * MmToFeet;
                            double ch = candidate.HeightMm * MmToFeet;
                            double advance = placedAnyInBand ? cw + hGapFeet : cw;

                            if (advance > bandRemainingWidth + 1e-9)
                                continue; // doesn't fit remaining width in this band — try the next (shorter-or-equal) candidate

                            placed.Add(new Placement { Item = candidate, X = subCursorX, Y = climbTopY - ch });
                            placedIds.Add(candidate.ViewId);

                            subCursorX += advance;
                            bandRemainingWidth -= advance;
                            bandHeight = Math.Max(bandHeight, ch);
                            placedAnyInBand = true;
                        }

                        if (!placedAnyInBand)
                            break; // nothing fits this band at all — column exhausted

                        climbTopY -= (bandHeight + vGapFeet);
                    }

                    cursorX += columnWidth + hGapFeet;
                }

                remaining = remaining.Where(i => !placedIds.Contains(i.ViewId)).ToList();

                if (!placedAnyThisRow)
                    break; // nothing placed this row at all — avoid an infinite loop, remaining -> caller loops into next sheet

                // ── ROW CLOSE: advance baseline for the next Master Row ──
                baseline = rowBottomY + vGapFeet;
            }

            double totalHeightUsed = placed.Count > 0 ? placed.Max(p => p.Y + p.Item.HeightMm * MmToFeet) : 0;
            return new Result { Placed = placed, Unplaced = remaining, TotalHeightFeet = totalHeightUsed };
        }
    }
}
