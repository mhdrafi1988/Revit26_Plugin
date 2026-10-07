using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.SheetViewArrange.V001.Core.Layout
{
    /// <summary>How the last row is laid out when the layout has two or more rows.</summary>
    public enum LastRowMode
    {
        /// <summary>Same gap as the row above, starting at the left edge (like justified text).</summary>
        PackLeft,

        /// <summary>Stretched to the full width like every other row.</summary>
        Justify,

        /// <summary>Same gap as the row above, centred in the width.</summary>
        Center
    }

    /// <summary>Axis-aligned rectangle in sheet space (feet).</summary>
    public readonly struct LayoutRect
    {
        /// <summary>Left edge.</summary>
        public double MinX { get; }

        /// <summary>Bottom edge.</summary>
        public double MinY { get; }

        /// <summary>Right edge.</summary>
        public double MaxX { get; }

        /// <summary>Top edge.</summary>
        public double MaxY { get; }

        /// <summary>Creates a rectangle from its four edges.</summary>
        public LayoutRect(double minX, double minY, double maxX, double maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        /// <summary>MaxX − MinX.</summary>
        public double Width => MaxX - MinX;

        /// <summary>MaxY − MinY.</summary>
        public double Height => MaxY - MinY;

        /// <summary>True when the rectangle has a positive width and height.</summary>
        public bool IsValid => Width > 0 && Height > 0;
    }

    /// <summary>Where one item lands: its footprint's bottom-left corner plus its row/column.</summary>
    public sealed class LayoutSlot
    {
        /// <summary>Index of the item in the input list.</summary>
        public int Index { get; init; }

        /// <summary>0-based row, counted from the top.</summary>
        public int Row { get; init; }

        /// <summary>0-based position within its row, counted from the left.</summary>
        public int Column { get; init; }

        /// <summary>New left edge of the item's footprint (feet).</summary>
        public double MinX { get; init; }

        /// <summary>New bottom edge of the item's footprint (feet).</summary>
        public double MinY { get; init; }

        /// <summary>False when the item lands (partly) outside the usable area.</summary>
        public bool Fits { get; init; }
    }

    /// <summary>Output of <see cref="ReadingTableLayout.Compute"/>.</summary>
    public sealed class LayoutResult
    {
        /// <summary>One slot per input item, in input order.</summary>
        public List<LayoutSlot> Slots { get; } = new();

        /// <summary>Number of rows.</summary>
        public int RowCount { get; set; }

        /// <summary>True when every item lies inside the usable area.</summary>
        public bool AllFit { get; set; } = true;

        /// <summary>Why the layout does not fit, or null when it does.</summary>
        public string Problem { get; set; }
    }

    /// <summary>
    /// Lays items out like text on a page: left → right, wrapping into rows top → bottom.
    /// <list type="bullet">
    /// <item>Rows are filled until the next item would not fit at the minimum horizontal gap.</item>
    /// <item>Each row is justified to the full usable width (equal gaps). The last row follows
    /// <see cref="LastRowMode"/> when there are two or more rows; a single-row layout is justified.</item>
    /// <item>Within a row every item's bottom edge sits on the row's bottom line, so view
    /// titles under the views line up.</item>
    /// <item>Rows are spread with equal gaps to fill the usable height (one row sits at the top).</item>
    /// </list>
    /// Pure math, no Revit types — so it can be tested outside Revit.
    /// </summary>
    public static class ReadingTableLayout
    {
        private const double Eps = 1e-9;

        /// <summary>
        /// Computes the layout. <paramref name="sizes"/> are footprint (width, height) pairs in
        /// reading order; all lengths in feet.
        /// </summary>
        public static LayoutResult Compute(
            IReadOnlyList<(double Width, double Height)> sizes,
            LayoutRect area,
            double minHGap,
            double minVGap,
            LastRowMode lastRowMode)
        {
            var result = new LayoutResult();
            if (sizes == null || sizes.Count == 0)
                return result;

            if (!area.IsValid)
            {
                result.AllFit = false;
                result.Problem = "The margins leave no usable area on the sheet.";
                return result;
            }

            minHGap = Math.Max(0, minHGap);
            minVGap = Math.Max(0, minVGap);

            var rows = BuildRows(sizes, area.Width, minHGap);
            result.RowCount = rows.Count;

            // ── Horizontal: x of each item, per row ─────────────────────────
            var xs = new double[sizes.Count];
            double previousGap = minHGap;
            for (int r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                bool isShortLastRow = r == rows.Count - 1 && rows.Count > 1 && lastRowMode != LastRowMode.Justify;
                double sumW = row.Sum(i => sizes[i].Width);
                double fullGap = row.Count > 1 ? (area.Width - sumW) / (row.Count - 1) : minHGap;

                // A short last row reuses the row above's gap so it reads as part of the same
                // table — never wider than the width allows, never below the minimum.
                double gap = isShortLastRow && row.Count > 1
                    ? Math.Max(minHGap, Math.Min(previousGap, fullGap))
                    : fullGap;

                double x = area.MinX;
                if (isShortLastRow && lastRowMode == LastRowMode.Center)
                {
                    double rowWidth = sumW + gap * (row.Count - 1);
                    x += Math.Max(0, (area.Width - rowWidth) / 2.0);
                }

                foreach (int i in row)
                {
                    xs[i] = x;
                    x += sizes[i].Width + gap;
                }

                if (row.Count > 1)
                    previousGap = gap;
            }

            // ── Vertical: rows spread to fill the height ────────────────────
            var rowHeights = rows.Select(row => row.Max(i => sizes[i].Height)).ToList();
            double totalRowHeight = rowHeights.Sum();
            double vGap = rows.Count > 1 ? (area.Height - totalRowHeight) / (rows.Count - 1) : 0;
            bool heightFits = totalRowHeight + minVGap * (rows.Count - 1) <= area.Height + Eps;
            if (!heightFits)
                vGap = minVGap; // keep the minimum gap; overflowing rows run below the area

            double rowTop = area.MaxY;
            for (int r = 0; r < rows.Count; r++)
            {
                double rowBottom = rowTop - rowHeights[r];
                for (int c = 0; c < rows[r].Count; c++)
                {
                    int i = rows[r][c];
                    var (w, h) = sizes[i];
                    bool fits = xs[i] >= area.MinX - Eps
                                && xs[i] + w <= area.MaxX + Eps
                                && rowBottom >= area.MinY - Eps
                                && rowBottom + h <= area.MaxY + Eps;

                    result.Slots.Add(new LayoutSlot
                    {
                        Index = i,
                        Row = r,
                        Column = c,
                        MinX = xs[i],
                        MinY = rowBottom,
                        Fits = fits
                    });
                }
                rowTop = rowBottom - vGap;
            }

            result.Slots.Sort((a, b) => a.Index.CompareTo(b.Index));
            result.AllFit = result.Slots.All(s => s.Fits);

            if (!result.AllFit)
                result.Problem = DescribeProblem(sizes, area, minVGap, rows.Count, totalRowHeight);

            return result;
        }

        /// <summary>Flow-fills rows: an item starts a new row when it would exceed the width.</summary>
        private static List<List<int>> BuildRows(IReadOnlyList<(double Width, double Height)> sizes, double width, double minHGap)
        {
            var rows = new List<List<int>>();
            var current = new List<int>();
            double used = 0;

            for (int i = 0; i < sizes.Count; i++)
            {
                double w = sizes[i].Width;
                double needed = current.Count == 0 ? w : used + minHGap + w;
                if (current.Count > 0 && needed > width + Eps)
                {
                    rows.Add(current);
                    current = new List<int>();
                    needed = w;
                }
                current.Add(i);
                used = needed;
            }

            if (current.Count > 0)
                rows.Add(current);
            return rows;
        }

        private static string DescribeProblem(
            IReadOnlyList<(double Width, double Height)> sizes,
            LayoutRect area,
            double minVGap,
            int rowCount,
            double totalRowHeight)
        {
            const double FeetToMm = 304.8;
            var problems = new List<string>();

            int tooWide = sizes.Count(s => s.Width > area.Width + Eps);
            if (tooWide > 0)
                problems.Add($"{tooWide} view(s) are wider than the usable area");

            double shortBy = totalRowHeight + minVGap * (rowCount - 1) - area.Height;
            if (shortBy > Eps)
                problems.Add($"{rowCount} row(s) need {shortBy * FeetToMm:F0} mm more height than the usable area");

            return problems.Count == 0
                ? "Some views do not fit in the usable area."
                : string.Join("; ", problems) + ".";
        }
    }
}
