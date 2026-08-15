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
    /// Implements the 3 "no banding" strategies that pack directly into the
    /// whole region in one pass, with no Master Row/Band scaffolding:
    /// Max Fill, Skyline, and Skyline + Waste Map. (RafisAlgo is a sibling
    /// service, RafisAlgoPackingService, since it still retains its own
    /// row/baseline progression — see that class for why it's separate.)
    ///
    /// Called on one sheet's worth of candidate views at a time (already
    /// selected by ReadingOrderPackingService's area-sum capacity estimate);
    /// Unplaced items are looped back into the next sheet's candidate pool
    /// by the caller, same as MasterRowBandPackingService / RafisAlgo.
    /// </summary>
    public class FreeRectPackingService
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

        private class FreeRect
        {
            public double X, Y, W, H;
        }

        public Result Pack(
            List<ViewInfo> items,
            double containerWidthFeet,
            double containerHeightMaxFeet,
            double hGapFeet,
            double vGapFeet,
            RowFillStrategy strategy)
        {
            return strategy switch
            {
                RowFillStrategy.MaxFill => PackMaxFill(items, containerWidthFeet, containerHeightMaxFeet, hGapFeet, vGapFeet),
                RowFillStrategy.Skyline => PackSkyline(items, containerWidthFeet, containerHeightMaxFeet, hGapFeet, vGapFeet, useWasteMap: false),
                RowFillStrategy.SkylineWasteMap => PackSkyline(items, containerWidthFeet, containerHeightMaxFeet, hGapFeet, vGapFeet, useWasteMap: true),
                _ => throw new ArgumentOutOfRangeException(nameof(strategy))
            };
        }

        // ============================================================
        // MAX FILL
        // Items sorted ascending by area (smallest first), single free-rect
        // list spanning the whole region, Best-Short-Side-Fit, ties -> max
        // remaining free area after split, up to 4-way split.
        // ============================================================
        private Result PackMaxFill(List<ViewInfo> items, double containerW, double containerHMax, double hGap, double vGap)
        {
            var sorted = items.OrderBy(i => i.WidthMm * i.HeightMm).ToList();
            var freeRects = new List<FreeRect> { new FreeRect { X = 0, Y = 0, W = containerW, H = containerHMax } };
            var placed = new List<Placement>();
            var unplaced = new List<ViewInfo>();
            double maxYUsed = 0;

            foreach (var it in sorted)
            {
                double w = it.WidthMm * MmToFeet;
                double h = it.HeightMm * MmToFeet;

                int bestIdx = -1;
                double bestScore = double.MaxValue;
                double bestRemainingArea = double.MinValue;

                for (int idx = 0; idx < freeRects.Count; idx++)
                {
                    var fr = freeRects[idx];
                    if (w <= fr.W + 1e-9 && h <= fr.H + 1e-9)
                    {
                        double score = Math.Min(fr.W - w, fr.H - h); // Best-Short-Side-Fit
                        double remainingArea = (fr.W * fr.H) - (w * h);

                        if (score < bestScore - 1e-9 ||
                            (Math.Abs(score - bestScore) <= 1e-9 && remainingArea > bestRemainingArea))
                        {
                            bestScore = score;
                            bestRemainingArea = remainingArea;
                            bestIdx = idx;
                        }
                    }
                }

                if (bestIdx < 0)
                {
                    unplaced.Add(it); // doesn't fit any free rect — caller loops it into the next sheet
                    continue;
                }

                var chosen = freeRects[bestIdx];
                freeRects.RemoveAt(bestIdx);

                double itemX = chosen.X;
                double itemY = chosen.Y; // top-aligned within the free rect (top-based space)

                placed.Add(new Placement { Item = it, X = itemX, Y = itemY });
                maxYUsed = Math.Max(maxYUsed, itemY + h);

                double px1 = itemX, py1 = itemY;
                double px2 = itemX + w + hGap, py2 = itemY + h + vGap;

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

            return new Result { Placed = placed, Unplaced = unplaced, TotalHeightFeet = maxYUsed };
        }

        // ============================================================
        // SKYLINE (BOTTOM-LEFT) and SKYLINE + WASTE MAP
        // ============================================================
        private class SkylineSegment
        {
            public double X, W, Height; // Height = top-based Y (0 = region top)
        }

        private Result PackSkyline(List<ViewInfo> items, double containerW, double containerHMax, double hGap, double vGap, bool useWasteMap)
        {
            var profile = new List<SkylineSegment> { new SkylineSegment { X = 0, W = containerW, Height = 0 } };
            var wasteMap = new List<FreeRect>();
            var placed = new List<Placement>();
            var unplaced = new List<ViewInfo>();
            double maxYUsed = 0;

            foreach (var it in items)
            {
                double w = it.WidthMm * MmToFeet;
                double h = it.HeightMm * MmToFeet;

                bool placedThisItem = false;

                if (useWasteMap)
                {
                    var wasteCandidate = wasteMap.FirstOrDefault(r => w <= r.W + 1e-9 && h <= r.H + 1e-9);
                    if (wasteCandidate != null)
                    {
                        placed.Add(new Placement { Item = it, X = wasteCandidate.X, Y = wasteCandidate.Y });
                        maxYUsed = Math.Max(maxYUsed, wasteCandidate.Y + h);
                        wasteMap.Remove(wasteCandidate);
                        placedThisItem = true;
                    }
                }

                if (!placedThisItem)
                {
                    double bestX = -1, bestY = double.MaxValue;
                    foreach (double candidateX in BuildCandidateXs(profile, containerW, w))
                    {
                        double y = MaxHeightUnder(profile, candidateX, w);
                        if (y < bestY - 1e-9)
                        {
                            bestY = y;
                            bestX = candidateX;
                        }
                    }

                    if (bestX < 0 || bestY + h > containerHMax + 1e-9)
                    {
                        unplaced.Add(it); // doesn't fit — caller loops it into the next sheet
                        continue;
                    }

                    if (useWasteMap)
                    {
                        foreach (var gap in FindGapsUnder(profile, bestX, w, bestY))
                            wasteMap.Add(gap);
                    }

                    placed.Add(new Placement { Item = it, X = bestX, Y = bestY });
                    maxYUsed = Math.Max(maxYUsed, bestY + h);

                    UpdateSkyline(profile, bestX, w, bestY + h + vGap, containerW);
                }
            }

            return new Result { Placed = placed, Unplaced = unplaced, TotalHeightFeet = maxYUsed };
        }

        private static IEnumerable<double> BuildCandidateXs(List<SkylineSegment> profile, double containerW, double itemW)
        {
            var xs = new SortedSet<double>();
            foreach (var seg in profile)
            {
                if (seg.X + itemW <= containerW + 1e-9) xs.Add(seg.X);
                double segEnd = seg.X + seg.W;
                double alt = segEnd;
                if (alt + itemW <= containerW + 1e-9) xs.Add(alt);
            }
            if (xs.Count == 0 && itemW <= containerW + 1e-9) xs.Add(0);
            return xs;
        }

        private static double MaxHeightUnder(List<SkylineSegment> profile, double x, double itemW)
        {
            double maxH = 0;
            double x2 = x + itemW;
            foreach (var seg in profile)
            {
                double segEnd = seg.X + seg.W;
                bool overlaps = seg.X < x2 - 1e-9 && segEnd > x + 1e-9;
                if (overlaps) maxH = Math.Max(maxH, seg.Height);
            }
            return maxH;
        }

        private static List<FreeRect> FindGapsUnder(List<SkylineSegment> profile, double x, double itemW, double placedY)
        {
            var gaps = new List<FreeRect>();
            double x2 = x + itemW;
            foreach (var seg in profile)
            {
                double segEnd = seg.X + seg.W;
                double left = Math.Max(seg.X, x);
                double right = Math.Min(segEnd, x2);
                if (right <= left + 1e-9) continue;

                if (seg.Height > placedY + 1e-9)
                    gaps.Add(new FreeRect { X = left, Y = placedY, W = right - left, H = seg.Height - placedY });
            }
            return gaps;
        }

        private static void UpdateSkyline(List<SkylineSegment> profile, double x, double itemW, double newHeight, double containerW)
        {
            double x2 = Math.Min(x + itemW, containerW);
            var result = new List<SkylineSegment>();

            foreach (var seg in profile)
            {
                double segEnd = seg.X + seg.W;

                if (segEnd <= x + 1e-9 || seg.X >= x2 - 1e-9)
                {
                    result.Add(seg);
                    continue;
                }

                if (seg.X < x - 1e-9)
                    result.Add(new SkylineSegment { X = seg.X, W = x - seg.X, Height = seg.Height });

                if (segEnd > x2 + 1e-9)
                    result.Add(new SkylineSegment { X = x2, W = segEnd - x2, Height = seg.Height });
            }

            result.Add(new SkylineSegment { X = x, W = x2 - x, Height = newHeight });

            var merged = result.Where(s => s.W > 1e-9).OrderBy(s => s.X).ToList();
            profile.Clear();
            foreach (var seg in merged)
            {
                if (profile.Count > 0 && Math.Abs(profile[^1].Height - seg.Height) < 1e-9 && Math.Abs((profile[^1].X + profile[^1].W) - seg.X) < 1e-6)
                    profile[^1].W += seg.W;
                else
                    profile.Add(seg);
            }
        }

        private static bool Overlaps(double ax1, double ay1, double ax2, double ay2, FreeRect b)
        {
            double bx1 = b.X, by1 = b.Y, bx2 = b.X + b.W, by2 = b.Y + b.H;
            return !(ax2 <= bx1 + 1e-9 || ax1 >= bx2 - 1e-9 || ay2 <= by1 + 1e-9 || ay1 >= by2 - 1e-9);
        }

        /// <summary>4-way split of a free rect against an overlapping placed-item footprint (top-based Y). Discards degenerate pieces.</summary>
        private static List<FreeRect> SplitAgainst(FreeRect fr, double px1, double py1, double px2, double py2)
        {
            var pieces = new List<FreeRect>();

            if (fr.X < px1)
                pieces.Add(new FreeRect { X = fr.X, Y = fr.Y, W = px1 - fr.X, H = fr.H });

            if (fr.X + fr.W > px2)
                pieces.Add(new FreeRect { X = px2, Y = fr.Y, W = (fr.X + fr.W) - px2, H = fr.H });

            if (fr.Y < py1)
                pieces.Add(new FreeRect { X = fr.X, Y = fr.Y, W = fr.W, H = py1 - fr.Y });

            if (fr.Y + fr.H > py2)
                pieces.Add(new FreeRect { X = fr.X, Y = py2, W = fr.W, H = (fr.Y + fr.H) - py2 });

            return pieces.Where(p => p.W > 1e-9 && p.H > 1e-9).ToList();
        }
    }
}
