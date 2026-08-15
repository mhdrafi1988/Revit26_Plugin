using Autodesk.Revit.DB;
using Revit26_Plugin.SmartViewToSheetPlacer.V220.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.SmartViewToSheetPlacer.V220.Services
{
    /// <summary>
    /// V220 PORT from SheetAutoRearrange V022 (verbatim algorithm, retargeted
    /// from ViewOnSheetItem to this tool's ViewInfo model — ViewInfo.ViewId
    /// stands in for ViewOnSheetItem.ViewportId as the unique identity key,
    /// since no Viewport exists yet at packing time here). See that class's
    /// original header for the full "Master Row + Band Rectangle Packing —
    /// All Three Fill Strategies" spec and its known row-height plateau
    /// characteristic (ported as-specified, unfixed).
    ///
    /// Implements the Shelf, Guillotine, and MaxRects strategies. Unlike the
    /// SheetAutoRearrange original, this service's Pack() is called on a
    /// single sheet's worth of CANDIDATE views (already selected by
    /// ReadingOrderPackingService's area-sum capacity estimate) — it still
    /// packs into ONE container per call and returns Unplaced for anything
    /// that didn't fit, same shape as before; the caller (V220
    /// ReadingOrderPackingService) is responsible for looping Unplaced back
    /// into the next sheet's candidate pool.
    ///
    /// SHARED SCAFFOLDING (identical for all three strategies): Items are
    /// packed row by row into "Master Rows." Each Master Row's height is the
    /// tallest height among ALL items still unplaced anywhere in the input
    /// list. Every remaining item is assigned to a height Band based on its
    /// ratio to that row height:
    ///   Band 1: ratio >= 0.75   Band 2: ratio >= 0.50
    ///   Band 3: ratio >= 0.25   Band 4: everything below 0.25
    /// (Shelf strategy only, additionally: each band gets a temporary
    /// placement ceiling = tallest item's height WITHIN that band, locked
    /// for the row.)
    ///
    /// All three strategies place items bottom-aligned within their row.
    /// Gap is a single uniform value (feet) — V220 uses the ViewType group's
    /// HorizontalGapMm for both axes, same convention as the
    /// SheetAutoRearrange original (Fixed-gap-only per V220's confirmed
    /// removal of EvenGap; H is the source of truth for this strategy
    /// family's one-gap parameter).
    /// </summary>
    public class MasterRowBandPackingService
    {
        private const double MmToFeet = 1.0 / 304.8;

        public class Placement
        {
            public ViewInfo Item { get; set; } = null!;
            /// <summary>Top-left X, sheet space feet, relative to the row-packing origin.</summary>
            public double X { get; set; }
            /// <summary>Top-left Y (top-based, row 0 starts at 0 and grows downward in this intermediate space — converted to sheet Y by the caller).</summary>
            public double Y { get; set; }
            public int Band { get; set; }
            public string BandLabel { get; set; } = "";
        }

        public class Result
        {
            public List<Placement> Placed { get; set; } = new();
            public List<ViewInfo> Unplaced { get; set; } = new();
            /// <summary>Total row-space height consumed (top-based), feet.</summary>
            public double TotalHeightFeet { get; set; }
        }

        private class FreeRect
        {
            public double X, Y, W, H;
        }

        public Result Pack(
            List<ViewInfo> items,
            double containerWidthFeet,
            double containerHeightMaxFeet,
            double gapFeet,
            RowFillStrategy strategy)
        {
            return strategy switch
            {
                RowFillStrategy.Shelf => PackShelf(items, containerWidthFeet, containerHeightMaxFeet, gapFeet),
                RowFillStrategy.Guillotine => PackFreeRect(items, containerWidthFeet, containerHeightMaxFeet, gapFeet, useShortSideFit: false),
                RowFillStrategy.MaxRects => PackFreeRect(items, containerWidthFeet, containerHeightMaxFeet, gapFeet, useShortSideFit: true),
                _ => throw new ArgumentOutOfRangeException(nameof(strategy))
            };
        }

        // ── Shared Step 1: banding ──────────────────────────────────────
        private static int BandOf(double heightFeet, double rowHeightFeet)
        {
            double ratio = heightFeet / rowHeightFeet;
            if (ratio >= 0.75) return 1;
            if (ratio >= 0.50) return 2;
            if (ratio >= 0.25) return 3;
            return 4;
        }

        private static (double rowHeight, Dictionary<int, List<ViewInfo>> bands) StartMasterRow(List<ViewInfo> remaining)
        {
            double rowHeight = remaining.Max(i => i.HeightMm * MmToFeet);

            var bands = new Dictionary<int, List<ViewInfo>>();
            foreach (var it in remaining)
            {
                int b = BandOf(it.HeightMm * MmToFeet, rowHeight);
                if (!bands.TryGetValue(b, out var list)) { list = new List<ViewInfo>(); bands[b] = list; }
                list.Add(it);
            }

            return (rowHeight, bands);
        }

        /// <summary>
        /// Shelf strategy only — Steps 3 and 4 both repeatedly group a pool
        /// of leftover items by matching width to build "columns" stacked
        /// bottom-aligned to rowBottom, differing only in (a) the height
        /// budget a column may fill up to (Step 3: that band's temporary
        /// ceiling; Step 4: the full row height) and (b) the Band tag
        /// stamped on each placement.
        /// </summary>
        private static double PackColumnsIntoRow(
            List<ViewInfo> pool,
            List<Placement> placed,
            HashSet<ElementId> placedThisRow,
            double startX,
            double containerW,
            double rowBottom,
            double gap,
            double heightBudget,
            int bandTag,
            string bandLabel)
        {
            double x = startX;
            var byWidth = pool.GroupBy(i => Math.Round(i.WidthMm, 1)).OrderByDescending(g => g.Key);

            foreach (var widthGroup in byWidth)
            {
                var columnItems = widthGroup.OrderByDescending(i => i.HeightMm).ToList();
                double colWidth = columnItems[0].WidthMm * MmToFeet;

                if (x + colWidth > containerW + 1e-9)
                    continue; // doesn't fit remaining row width — try a narrower column group

                double cursorYFromBottom = 0.0; // distance up from rowBottom
                foreach (var it in columnItems)
                {
                    double h = it.HeightMm * MmToFeet;
                    if (cursorYFromBottom + h > heightBudget + 1e-9)
                        continue; // doesn't fit remaining height budget in this column — skip, stays in pool

                    double itemY = rowBottom - cursorYFromBottom - h;
                    placed.Add(new Placement { Item = it, X = x, Y = itemY, Band = bandTag, BandLabel = bandLabel });
                    placedThisRow.Add(it.ViewId);
                    cursorYFromBottom += h + gap;
                }

                x += colWidth + gap;
            }

            return x;
        }

        // ============================================================
        // STRATEGY A: Shelf
        // ============================================================
        private Result PackShelf(List<ViewInfo> items, double containerW, double containerHMax, double gap)
        {
            var remaining = new List<ViewInfo>(items);
            var placed = new List<Placement>();
            double rowTop = 0.0;

            while (remaining.Count > 0)
            {
                var (rowHeight, bands) = StartMasterRow(remaining);
                double rowBottom = rowTop + rowHeight;
                var placedThisRow = new HashSet<ElementId>();
                double x = 0.0;

                // Step 2: Band-local temporary placement ceiling = tallest item WITHIN that band.
                var bandCeiling = new Dictionary<int, double>();
                foreach (var kv in bands)
                    bandCeiling[kv.Key] = kv.Value.Max(i => i.HeightMm) * MmToFeet;

                // Step 3: Band 1 left-to-right at full row height, then Bands 2-4 each
                // packed as columns up to their own band ceiling.
                if (bands.TryGetValue(1, out var band1))
                {
                    var pool = band1.Where(i => !placedThisRow.Contains(i.ViewId)).ToList();
                    x = PackColumnsIntoRow(pool, placed, placedThisRow, x, containerW, rowBottom, gap, rowHeight, 1, "1");
                    bands[1] = pool.Where(i => !placedThisRow.Contains(i.ViewId)).ToList();
                }

                foreach (int b in new[] { 2, 3, 4 })
                {
                    if (!bands.TryGetValue(b, out var pool)) continue;
                    pool = pool.Where(i => !placedThisRow.Contains(i.ViewId)).ToList();
                    double ceiling = bandCeiling.TryGetValue(b, out var c) ? c : 0;

                    x = PackColumnsIntoRow(pool, placed, placedThisRow, x, containerW, rowBottom, gap, ceiling, b, b.ToString());
                    bands[b] = pool.Where(i => !placedThisRow.Contains(i.ViewId)).ToList();
                }

                // Step 4: gap-fill pass — pool remaining Band 2/3/4 items, full row-height budget
                var gapfillPool = new List<ViewInfo>();
                foreach (int b in new[] { 2, 3, 4 })
                    if (bands.TryGetValue(b, out var p)) gapfillPool.AddRange(p);
                gapfillPool = gapfillPool.Where(i => !placedThisRow.Contains(i.ViewId)).ToList();

                x = PackColumnsIntoRow(gapfillPool, placed, placedThisRow, x, containerW, rowBottom, gap, rowHeight, 0, "gapfill");

                // Shared Step 6: close the row
                if (placedThisRow.Count == 0)
                    break;

                remaining = remaining.Where(i => !placedThisRow.Contains(i.ViewId)).ToList();
                rowTop = rowBottom + gap;
                if (rowTop > containerHMax)
                    break;
            }

            return new Result { Placed = placed, Unplaced = remaining, TotalHeightFeet = rowTop };
        }

        // ============================================================
        // STRATEGY B (Guillotine, useShortSideFit=false) and
        // STRATEGY C (MaxRects,   useShortSideFit=true)
        // ============================================================
        private Result PackFreeRect(List<ViewInfo> items, double containerW, double containerHMax, double gap, bool useShortSideFit)
        {
            var remaining = new List<ViewInfo>(items);
            var placed = new List<Placement>();
            double rowTop = 0.0;

            while (remaining.Count > 0)
            {
                var (rowHeight, bands) = StartMasterRow(remaining);

                // Step 3: placement order — Band 1 (tallest-first), then 2, 3, 4 (each tallest-first)
                var order = new List<ViewInfo>();
                foreach (int b in new[] { 1, 2, 3, 4 })
                    if (bands.TryGetValue(b, out var list))
                        order.AddRange(list.OrderByDescending(i => i.HeightMm));

                var freeRects = new List<FreeRect> { new FreeRect { X = 0, Y = rowTop, W = containerW, H = rowHeight } };
                var placedThisRow = new HashSet<ElementId>();

                foreach (var it in order)
                {
                    double w = it.WidthMm * MmToFeet;
                    double h = it.HeightMm * MmToFeet;

                    int bestIdx = -1;
                    double bestScore = double.MaxValue;
                    for (int idx = 0; idx < freeRects.Count; idx++)
                    {
                        var fr = freeRects[idx];
                        if (w <= fr.W + 1e-9 && h <= fr.H + 1e-9)
                        {
                            double score = useShortSideFit
                                ? Math.Min(fr.W - w, fr.H - h)   // MaxRects: Best-Short-Side-Fit
                                : (fr.W - w) + (fr.H - h);       // Guillotine: Best-Area-Fit (combined leftover)
                            if (score < bestScore)
                            {
                                bestScore = score;
                                bestIdx = idx;
                            }
                        }
                    }
                    if (bestIdx < 0) continue; // doesn't fit any free rect — skip, stays in remaining for next row

                    var chosen = freeRects[bestIdx];
                    freeRects.RemoveAt(bestIdx);

                    double itemX = chosen.X;
                    double itemY = chosen.Y + chosen.H - h; // bottom-aligned within the free rect

                    int band = BandOf(h, rowHeight);
                    placed.Add(new Placement { Item = it, X = itemX, Y = itemY, Band = band, BandLabel = band.ToString() });
                    placedThisRow.Add(it.ViewId);

                    double px1 = itemX, py1 = itemY - gap;
                    double px2 = itemX + w + gap, py2 = itemY + h;

                    if (useShortSideFit)
                    {
                        var newFree = new List<FreeRect>();
                        newFree.AddRange(SplitAgainst(chosen, px1, py1, px2, py2));
                        foreach (var existing in freeRects)
                        {
                            if (Overlaps(px1, py1, px2, py2, existing))
                                newFree.AddRange(SplitAgainst(existing, px1, py1, px2, py2));
                            else
                                newFree.Add(existing);
                        }
                        freeRects = newFree;
                    }
                    else
                    {
                        var right = new FreeRect { X = chosen.X + w + gap, Y = chosen.Y, W = chosen.W - w - gap, H = chosen.H };
                        var below = new FreeRect { X = chosen.X, Y = chosen.Y, W = w, H = chosen.H - h - gap };
                        if (right.W > 1e-9 && right.H > 1e-9) freeRects.Add(right);
                        if (below.W > 1e-9 && below.H > 1e-9) freeRects.Add(below);
                    }
                }

                if (placedThisRow.Count == 0)
                    break;

                remaining = remaining.Where(i => !placedThisRow.Contains(i.ViewId)).ToList();
                rowTop = rowTop + rowHeight + gap;
                if (rowTop > containerHMax)
                    break;
            }

            return new Result { Placed = placed, Unplaced = remaining, TotalHeightFeet = rowTop };
        }

        private static bool Overlaps(double ax1, double ay1, double ax2, double ay2, FreeRect b)
        {
            double bx1 = b.X, by1 = b.Y, bx2 = b.X + b.W, by2 = b.Y + b.H;
            return !(ax2 <= bx1 + 1e-9 || ax1 >= bx2 - 1e-9 || ay2 <= by1 + 1e-9 || ay1 >= by2 - 1e-9);
        }

        /// <summary>4-way split of a free rect against an overlapping placed-item footprint. Discards degenerate (zero/negative) pieces.</summary>
        private static List<FreeRect> SplitAgainst(FreeRect fr, double px1, double py1, double px2, double py2)
        {
            var pieces = new List<FreeRect>();

            if (fr.X < px1) // left piece
                pieces.Add(new FreeRect { X = fr.X, Y = fr.Y, W = px1 - fr.X, H = fr.H });

            if (fr.X + fr.W > px2) // right piece
                pieces.Add(new FreeRect { X = px2, Y = fr.Y, W = (fr.X + fr.W) - px2, H = fr.H });

            if (fr.Y < py1) // bottom piece
                pieces.Add(new FreeRect { X = fr.X, Y = fr.Y, W = fr.W, H = py1 - fr.Y });

            if (fr.Y + fr.H > py2) // top piece
                pieces.Add(new FreeRect { X = fr.X, Y = py2, W = fr.W, H = (fr.Y + fr.H) - py2 });

            return pieces.Where(p => p.W > 1e-9 && p.H > 1e-9).ToList();
        }
    }
}
