// File: Services/PlacingAlgorithms/EvenGapPlacementService.cs
using Autodesk.Revit.DB;
using Revit26_Plugin.APUS.V322.Models;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS.V322.Services.PlacingAlgorithms
{
    /// <summary>
    /// A single section queued for placement, carrying everything the engine
    /// needs to sort and size it.
    /// </summary>
    public class PlacementCandidate
    {
        public ViewSection SectionView { get; set; }
        public XYZ MarkerPoint { get; set; }
        public bool MarkerResolved { get; set; }
        public SectionFootprint Footprint { get; set; }

        /// <summary>
        /// False when GetBoxOutline() measurement threw and Footprint was
        /// set to a placeholder default rather than a real value. BuildPlan
        /// reports these as failures instead of silently placing them at a
        /// near-zero size (the previous behaviour before this fix).
        /// </summary>
        public bool IsMeasurable { get; set; } = true;
    }

    /// <summary>
    /// One section's final placement result.
    /// </summary>
    public class PlacementResult
    {
        public ViewSection SectionView { get; set; }
        public XYZ FinalCenter { get; set; }
        public int SheetIndex { get; set; }
        public int DetailNumber { get; set; }
    }

    /// <summary>
    /// Sheet-level summary used for the pre-placement confirmation dialog
    /// and the post-run metric bar.
    /// SheetGroups is sheets -> rows -> candidates. Rows are the actual
    /// groups BuildPlan computed (including its height-aware row breaks) —
    /// callers must NOT re-derive rows from a flattened list, since a
    /// second width-only pass can disagree with BuildPlan's height-aware
    /// one and place a row that doesn't actually fit.
    /// </summary>
    public class PlacementPlan
    {
        public List<List<List<PlacementCandidate>>> SheetGroups { get; set; } = new();
        public List<PlacementCandidate> Oversized { get; set; } = new();
        public List<PlacementCandidate> Unmeasurable { get; set; } = new();
        public int TotalSheetsNeeded => SheetGroups.Count;
        public int TotalSectionsToPlace => SheetGroups.Sum(sheet => sheet.Sum(row => row.Count));
    }

    /// <summary>
    /// V321 placement engine.
    ///
    /// Replaces SimpleGridPlacementService (the only algorithm V320 actually
    /// dispatched to) and deletes the dead ReadingOrderBinPlacementService,
    /// MultiStrategyShelfLayoutService, AdaptiveGridPlacementService,
    /// MultiSheetGridPlacementService, and GridLayoutCalculationService,
    /// none of which were ever reached by the handler.
    ///
    /// Behaviour (per Rafi's decisions):
    ///  - Reading order: marker location, sorted by the chosen ReadingDirection,
    ///    grouped into row bands using Row Y-tolerance, then by RowTiebreak.
    ///  - Row layout: even X-gaps (first flush left, last flush right,
    ///    H-gap = minimum enforced gap); single-view rows are centered.
    ///  - Vertical: bottom-aligned within a row; rows packed from the top with
    ///    a fixed vertical gap. No fill-to-height scaling — true scale is kept.
    ///  - Sizing: Viewport.GetBoxOutline() is the only size source, used both
    ///    for packing and for the (report-only) utilization metric.
    ///  - Oversized views (wider than usable width) are skipped as failures.
    ///  - No per-sheet cap: sheets fill by real size and overflow to the next.
    ///  - No detail-curve decorations are drawn.
    /// </summary>
    public class EvenGapPlacementService
    {
        private readonly Action<LogLevel, string> _log;

        public EvenGapPlacementService(Action<LogLevel, string> log)
        {
            _log = log;
        }

        /// <summary>
        /// Pass 1: sort candidates into reading order.
        /// </summary>
        public List<PlacementCandidate> SortByReadingOrder(
            List<PlacementCandidate> candidates,
            View activeView,
            ReadingDirection direction,
            double yToleranceFt,
            RowTiebreak tiebreak,
            SortFallback fallback)
        {
            XYZ right = activeView.RightDirection;
            XYZ up = activeView.UpDirection;

            var resolved = candidates.Where(c => c.MarkerResolved).ToList();
            var unresolved = candidates.Where(c => !c.MarkerResolved).ToList();

            // Project marker points into the view's right/up axes.
            var projected = resolved.Select(c => new
            {
                Candidate = c,
                U = c.MarkerPoint.DotProduct(right),
                V = c.MarkerPoint.DotProduct(up)
            }).ToList();

            bool topToBottom = direction == ReadingDirection.TopToBottom_LeftToRight
                             || direction == ReadingDirection.TopToBottom_RightToLeft;
            bool leftToRight = direction == ReadingDirection.TopToBottom_LeftToRight
                             || direction == ReadingDirection.BottomToTop_LeftToRight;

            // Bucket into row bands using Y-tolerance, comparing against the
            // running band anchor rather than the previous single point —
            // this avoids the V320 drift bug where a slowly-climbing/falling
            // sequence of points could each be "close enough" to the last one
            // but end up far from the band's true center.
            var ordered = topToBottom
                ? projected.OrderByDescending(p => p.V).ToList()
                : projected.OrderBy(p => p.V).ToList();

            var bands = new List<List<dynamic>>();
            foreach (var p in ordered)
            {
                var band = bands.LastOrDefault();
                if (band == null || Math.Abs(p.V - (double)band.Average(b => (double)b.V)) > yToleranceFt)
                {
                    band = new List<dynamic>();
                    bands.Add(band);
                }
                band.Add(p);
            }

            var result = new List<PlacementCandidate>();
            foreach (var band in bands)
            {
                IEnumerable<dynamic> sortedBand = tiebreak switch
                {
                    RowTiebreak.Name => band.OrderBy(b => (string)b.Candidate.SectionView.Name),
                    RowTiebreak.Size => band.OrderByDescending(b =>
                        (double)b.Candidate.Footprint.WidthFt * (double)b.Candidate.Footprint.HeightFt),
                    _ => leftToRight
                        ? band.OrderBy(b => (double)b.U)
                        : band.OrderByDescending(b => (double)b.U)
                };

                foreach (var b in sortedBand)
                    result.Add((PlacementCandidate)b.Candidate);
            }

            // Fallback: sections with no resolvable marker go to the end.
            IEnumerable<PlacementCandidate> tail = fallback == SortFallback.SortByName
                ? unresolved.OrderBy(c => c.SectionView.Name)
                : unresolved;

            result.AddRange(tail);
            return result;
        }

        /// <summary>
        /// Pass 2: group sorted candidates into rows per sheet, respecting
        /// usable width/height. No per-sheet cap on view count — sheets fill
        /// by real size and overflow to the next sheet.
        /// Oversized candidates (wider than usable width) and unmeasurable
        /// candidates (GetBoxOutline failed) are pulled out separately and
        /// reported as failures rather than placed.
        /// Returns the actual rows it built (sheets -> rows -> candidates),
        /// including the height-aware row breaks — callers must use these
        /// rows as-is rather than re-deriving rows from a flattened list.
        /// </summary>
        public PlacementPlan BuildPlan(
            List<PlacementCandidate> sortedCandidates,
            double usableWidthFt,
            double usableHeightFt,
            double hGapMinFt,
            double vGapFt)
        {
            var plan = new PlacementPlan();
            var remaining = new List<PlacementCandidate>();

            foreach (var c in sortedCandidates)
            {
                if (!c.IsMeasurable)
                {
                    plan.Unmeasurable.Add(c);
                    _log(LogLevel.Warning,
                        $"{c.SectionView.Name} skipped — could not be measured (GetBoxOutline failed).");
                    continue;
                }

                if (c.Footprint.WidthFt > usableWidthFt)
                {
                    plan.Oversized.Add(c);
                    _log(LogLevel.Warning,
                        $"{c.SectionView.Name} skipped — footprint width " +
                        $"{c.Footprint.WidthFt:F2} ft exceeds usable sheet width {usableWidthFt:F2} ft.");
                    continue;
                }
                remaining.Add(c);
            }

            while (remaining.Count > 0)
            {
                var sheetRows = new List<List<PlacementCandidate>>();
                double heightUsed = 0;
                var pool = new List<PlacementCandidate>(remaining);

                while (pool.Count > 0)
                {
                    var row = new List<PlacementCandidate>();
                    double widthUsed = 0;
                    double rowHeight = 0;

                    int i = 0;
                    while (i < pool.Count)
                    {
                        var cand = pool[i];
                        double addedWidth = row.Count == 0
                            ? cand.Footprint.WidthFt
                            : cand.Footprint.WidthFt + hGapMinFt;

                        if (widthUsed + addedWidth <= usableWidthFt)
                        {
                            row.Add(cand);
                            widthUsed += addedWidth;
                            rowHeight = Math.Max(rowHeight, cand.Footprint.HeightFt);
                            i++;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (row.Count == 0)
                    {
                        // Single view alone doesn't fit remaining width —
                        // stop this sheet, carry the rest to the next one.
                        break;
                    }

                    double neededHeight = heightUsed == 0 ? rowHeight : heightUsed + vGapFt + rowHeight;
                    if (neededHeight > usableHeightFt && sheetRows.Count > 0)
                    {
                        // This row doesn't fit vertically — leave it for the next sheet.
                        break;
                    }

                    sheetRows.Add(row);
                    heightUsed = neededHeight;
                    foreach (var c in row) pool.Remove(c);
                }

                if (sheetRows.Count == 0)
                {
                    // Nothing fit at all (shouldn't normally happen since
                    // oversized items are filtered before this point) — bail
                    // to avoid an infinite loop.
                    foreach (var c in pool)
                    {
                        plan.Oversized.Add(c);
                        _log(LogLevel.Warning, $"{c.SectionView.Name} skipped — could not fit on any sheet.");
                    }
                    break;
                }

                // The real rows this sheet computed are what gets stored —
                // no downstream re-derivation, which is what caused bug #1.
                plan.SheetGroups.Add(sheetRows);
                remaining = pool;
            }

            return plan;
        }

        /// <summary>
        /// Pass 3: compute final centers for one sheet's rows.
        /// Rows are packed from the top of the usable area downward with a
        /// fixed vertical gap (no fill-to-height stretch). Within each row,
        /// gaps are distributed evenly: first view flush left, last view
        /// flush right, H-gap acts as the minimum. A single-view row is
        /// centered horizontally.
        /// </summary>
        public List<PlacementResult> ComputeRowLayout(
            List<List<PlacementCandidate>> rows,
            SheetPlacementArea area,
            double hGapMinFt,
            double vGapFt,
            int sheetIndex,
            Func<int> nextDetailNumber)
        {
            var results = new List<PlacementResult>();
            double rowTop = area.Origin.Y;

            foreach (var row in rows)
            {
                double rowHeight = row.Max(c => c.Footprint.HeightFt);
                double rowBottom = rowTop - rowHeight;

                double totalContentWidth = row.Sum(c => c.Footprint.WidthFt);

                if (row.Count == 1)
                {
                    var c = row[0];
                    double centerX = area.Origin.X + area.Width / 2.0;
                    double centerY = rowBottom + c.Footprint.HeightFt / 2.0;
                    results.Add(new PlacementResult
                    {
                        SectionView = c.SectionView,
                        FinalCenter = new XYZ(centerX, centerY, 0),
                        SheetIndex = sheetIndex,
                        DetailNumber = nextDetailNumber()
                    });
                }
                else
                {
                    double availableGapSpace = area.Width - totalContentWidth;
                    double gapCount = row.Count - 1;
                    double actualGap = gapCount > 0
                        ? Math.Max(hGapMinFt, availableGapSpace / gapCount)
                        : 0;

                    double cursorX = area.Origin.X;
                    foreach (var c in row)
                    {
                        double centerX = cursorX + c.Footprint.WidthFt / 2.0;
                        double centerY = rowBottom + c.Footprint.HeightFt / 2.0;
                        results.Add(new PlacementResult
                        {
                            SectionView = c.SectionView,
                            FinalCenter = new XYZ(centerX, centerY, 0),
                            SheetIndex = sheetIndex,
                            DetailNumber = nextDetailNumber()
                        });
                        cursorX += c.Footprint.WidthFt + actualGap;
                    }
                }

                rowTop = rowBottom - vGapFt;
            }

            return results;
        }

        /// <summary>
        /// Utilization is computed from the same GetBoxOutline-derived
        /// footprints used for packing (fixing the V320 mismatch where
        /// packing and utilization used two different size sources).
        /// This is reported only — it never feeds back into the layout.
        /// </summary>
        public double CalculateUtilization(List<PlacementCandidate> placed, SheetPlacementArea area)
        {
            if (area.Width <= 0 || area.Height <= 0) return 0;
            double usedArea = placed.Sum(c => c.Footprint.WidthFt * c.Footprint.HeightFt);
            double totalArea = area.Width * area.Height;
            return totalArea > 0 ? (usedArea / totalArea) * 100.0 : 0;
        }
    }
}
