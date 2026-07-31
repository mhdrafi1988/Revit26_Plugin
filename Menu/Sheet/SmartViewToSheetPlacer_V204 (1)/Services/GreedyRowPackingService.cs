using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.SmartViewToSheetPlacer.V204.Models;

namespace Revit26_Plugin.SmartViewToSheetPlacer.V204.Services
{
    /// <summary>
    /// Pure calculation service: packs selected views onto suggested sheets
    /// using greedy row-packing (fill left-to-right, wrap to a new row when a
    /// view doesn't fit remaining row width, stack rows top-to-bottom until
    /// sheet height is exceeded — same logic as the APUS viewport placement
    /// tool). Placement is always non-mixed: each Revit ViewType group is
    /// packed onto its own independent set of sheets; a sheet never contains
    /// more than one ViewType. No Revit API writes happen here — this is a
    /// stateless, deterministic transform used directly by Stage 2's
    /// ViewModel (no IExternalEventHandler required for this step).
    /// V204: moved from Handlers/ to Services/ — this is a pure calculation
    /// service, not a Revit-API handler.
    /// </summary>
    public static class GreedyRowPackingService
    {
        /// <summary>
        /// Packs the given selected views onto suggested SheetGroups.
        /// Views are first split into independent ViewType groups (non-mixed),
        /// then each group is packed onto as many sheets as needed using
        /// greedy row-packing against the titleblock's usable area.
        /// </summary>
        /// <param name="selectedViews">Views the user selected in Stage 1.</param>
        /// <param name="titleblock">Titleblock providing usable sheet area (post-margin).</param>
        /// <param name="gapHorizontalMm">Gap reserved between views placed side-by-side on the
        /// same row. Applied strictly between items — a single view on a row reserves no gap.</param>
        /// <param name="gapVerticalMm">Gap reserved between rows of views. Applied strictly
        /// between rows — a single row on a sheet reserves no gap.</param>
        /// <returns>All SheetGroups produced, in ViewType-group order then sheet index order.</returns>
        public static List<SheetGroup> Pack(
            IEnumerable<ViewInfo> selectedViews,
            TitleblockOption titleblock,
            double gapHorizontalMm = 0,
            double gapVerticalMm = 0)
        {
            var result = new List<SheetGroup>();

            // Non-mixed: split into independent groups by RevitViewType only (per confirmed rule).
            var byType = selectedViews
                .GroupBy(v => v.RevitViewType)
                .OrderBy(g => g.Key.ToString());

            foreach (var group in byType)
            {
                var sheetsForType = PackSingleGroup(group.ToList(), titleblock, group.Key, gapHorizontalMm, gapVerticalMm);
                result.AddRange(sheetsForType);
            }

            return result;
        }

        /// <summary>
        /// Packs one ViewType group's views onto as many sheets as needed.
        /// Views are placed largest-first within the group (helps row-fill
        /// efficiency), each sheet filled via greedy left-to-right / top-to-
        /// bottom row wrapping.
        /// </summary>
        private static List<SheetGroup> PackSingleGroup(
            List<ViewInfo> views,
            TitleblockOption titleblock,
            ViewType viewType,
            double gapHorizontalMm,
            double gapVerticalMm)
        {
            var sheets = new List<SheetGroup>();
            var remaining = new Queue<ViewInfo>(views.OrderByDescending(v => v.HeightMm * v.WidthMm));

            int sheetIndex = 1;
            while (remaining.Count > 0)
            {
                var sheet = new SheetGroup(viewType, sheetIndex);
                PackOntoSheet(sheet, remaining, titleblock, gapHorizontalMm, gapVerticalMm);
                sheets.Add(sheet);
                sheetIndex++;

                // Safety valve: a single view larger than the usable area still
                // consumes exactly one sheet (flagged as overflow) rather than
                // looping forever.
                if (sheet.Placements.Count == 0 && remaining.Count > 0)
                {
                    var oversized = remaining.Dequeue();
                    var placement = new ViewPlacement(oversized, sheet) { Fits = false, Position = "Top-Left" };
                    sheet.Placements.Add(placement);
                }
            }

            return sheets;
        }

        /// <summary>
        /// Fills one sheet with as many queued views as fit, using row-packing:
        /// advances left-to-right on the current row, wraps to a new row when
        /// remaining row width is exceeded, and stops (leaving the rest in the
        /// queue for the next sheet) once row height would exceed the sheet's
        /// usable height. gapHorizontalMm is inserted before each view after the
        /// first one on a row; gapVerticalMm is inserted before each row after
        /// the first one on the sheet — so a lone view/row never reserves gap
        /// space it doesn't need (per confirmed "between items only" rule).
        /// </summary>
        private static void PackOntoSheet(
            SheetGroup sheet,
            Queue<ViewInfo> remaining,
            TitleblockOption titleblock,
            double gapHorizontalMm,
            double gapVerticalMm)
        {
            double usableW = titleblock.UsableWidthMm;
            double usableH = titleblock.UsableHeightMm;

            double cursorX = 0;
            double cursorY = 0;
            double rowHeight = 0;

            var positionsInOrder = new[] { "Top-Left", "Top-Right", "Bottom-Left", "Bottom-Right" };
            int placedCount = 0;
            bool isFirstOnRow = true;
            bool isFirstRow = true;

            while (remaining.Count > 0)
            {
                var next = remaining.Peek();

                // Gap only applies before an item that isn't first on its row.
                double gapBeforeThisItem = isFirstOnRow ? 0 : gapHorizontalMm;
                bool fitsOnCurrentRow = cursorX + gapBeforeThisItem + next.WidthMm <= usableW;
                bool needsNewRow = !fitsOnCurrentRow;

                // Gap before a new row only applies if it isn't the sheet's first row.
                double gapBeforeThisRow = isFirstRow ? 0 : gapVerticalMm;
                double rowHeightIfWrapped = needsNewRow ? next.HeightMm : Math.Max(rowHeight, next.HeightMm);
                double yIfWrapped = needsNewRow ? cursorY + rowHeight + gapBeforeThisRow : cursorY;

                bool fitsSheetHeight = yIfWrapped + rowHeightIfWrapped <= usableH;

                if (!fitsSheetHeight)
                {
                    // No more room on this sheet — leave remaining views for the next sheet.
                    break;
                }

                if (needsNewRow)
                {
                    cursorY += rowHeight + gapBeforeThisRow;
                    cursorX = 0;
                    rowHeight = 0;
                    isFirstOnRow = true;
                    isFirstRow = false;
                }

                double placeX = cursorX + (isFirstOnRow ? 0 : gapHorizontalMm);

                remaining.Dequeue();

                var placement = new ViewPlacement(next, sheet)
                {
                    OffsetXMm = placeX,
                    OffsetYMm = cursorY,
                    Fits = true,
                    Position = positionsInOrder[Math.Min(placedCount, positionsInOrder.Length - 1)]
                };
                sheet.Placements.Add(placement);

                cursorX = placeX + next.WidthMm;
                rowHeight = Math.Max(rowHeight, next.HeightMm);
                placedCount++;
                isFirstOnRow = false;
            }
        }

        /// <summary>
        /// Re-validates whether a manually re-assigned placement still fits its
        /// new sheet's usable area. Called from Stage 2 when the user overrides
        /// "Suggested Sheet #" via the per-row dropdown. This is a simplified
        /// aggregate check (total packed area vs usable area) rather than a
        /// full re-run of row-packing, since manual overrides are individual
        /// spot-edits, not a full repack. Gaps are folded into the same
        /// "between items only" rule as the packing pass: gapHorizontalMm is
        /// added before each item after the first on a row, gapVerticalMm
        /// before each row after the first on the sheet.
        /// </summary>
        public static bool RevalidateFit(
            SheetGroup sheet,
            TitleblockOption titleblock,
            double gapHorizontalMm = 0,
            double gapVerticalMm = 0)
        {
            double usedHeight = 0;
            double rowWidth = 0;
            double rowHeight = 0;
            bool isFirstOnRow = true;
            bool isFirstRow = true;

            foreach (var p in sheet.Placements)
            {
                double gapBeforeThisItem = isFirstOnRow ? 0 : gapHorizontalMm;

                if (rowWidth + gapBeforeThisItem + p.View.WidthMm > titleblock.UsableWidthMm)
                {
                    double gapBeforeThisRow = isFirstRow ? 0 : gapVerticalMm;
                    usedHeight += rowHeight + gapBeforeThisRow;
                    rowWidth = 0;
                    rowHeight = 0;
                    isFirstOnRow = true;
                    isFirstRow = false;
                    gapBeforeThisItem = 0;
                }

                rowWidth += gapBeforeThisItem + p.View.WidthMm;
                rowHeight = Math.Max(rowHeight, p.View.HeightMm);
                isFirstOnRow = false;
            }
            usedHeight += rowHeight;

            bool fits = usedHeight <= titleblock.UsableHeightMm;
            foreach (var p in sheet.Placements)
                p.Fits = fits;

            return fits;
        }
    }
}
