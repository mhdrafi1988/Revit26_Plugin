using Autodesk.Revit.DB;
using Revit26_Plugin.SheetAutoRearrange.V006.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.SheetAutoRearrange.V006.Core.Services
{
    /// <summary>Result of a detection pass — either a resolved PlaceableRegion, or a signal that the sheet has 2+ title blocks and must be skipped.</summary>
    public class TitleBlockDetectionResult
    {
        public PlaceableRegion? Region { get; set; }

        /// <summary>True if 2+ title block instances were found on the sheet — per confirmed design, the caller must skip this sheet and log a warning, no region is produced.</summary>
        public bool MultipleTitleBlocksFound { get; set; }

        /// <summary>True if no title block instance was found at all on the sheet.</summary>
        public bool NoTitleBlockFound { get; set; }
    }

    /// <summary>
    /// Locates the title block instance on a sheet and classifies its position
    /// against the sheet's own bounding box to resolve a PlaceableRegion:
    ///   - RightEdge / BottomEdge → single rectangle (sheet minus the strip).
    ///   - Corner → L-shape (Large rect + Small rect).
    ///   - Undetected → caller falls back to manual user input (single rect).
    ///
    /// Multiple title blocks on one sheet → caller skips the sheet entirely
    /// (confirmed: "if two title block present just do nothing").
    /// </summary>
    public class TitleBlockDetectionService
    {
        private const double FeetToMm = 304.8;
        private const double MmToFeet = 1.0 / 304.8;

        /// <summary>
        /// ASSUMPTION (flagged, not confirmed with an exact value by Rafi):
        /// 5mm tolerance for "hugs this edge" / "spans fully" comparisons.
        /// Revit title block bboxes rarely land on exact sheet-edge coordinates
        /// (frame line width, family origin offset, etc.) so some slack is
        /// required. Exposed as a constant for easy tuning if 5mm proves wrong
        /// in practice.
        /// </summary>
        private const double EdgeToleranceMm = 5.0;

        public TitleBlockDetectionResult Detect(Document doc, ViewSheet sheet)
        {
            var result = new TitleBlockDetectionResult();

            var titleBlocks = new FilteredElementCollector(doc, sheet.Id)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .ToList();

            if (titleBlocks.Count == 0)
            {
                result.NoTitleBlockFound = true;
                return result;
            }

            if (titleBlocks.Count > 1)
            {
                result.MultipleTitleBlocksFound = true;
                return result;
            }

            var tb = titleBlocks[0];
            var tbBox = tb.get_BoundingBox(sheet);
            var sheetBox = sheet.get_BoundingBox(null);

            if (tbBox == null || sheetBox == null)
            {
                // Can't classify without geometry — treat as undetected so the
                // caller offers manual input rather than guessing.
                result.Region = null;
                return result;
            }

            result.Region = Classify(sheetBox.Min, sheetBox.Max, tbBox.Min, tbBox.Max);
            return result;
        }

        private PlaceableRegion Classify(XYZ sheetMin, XYZ sheetMax, XYZ tbMin, XYZ tbMax)
        {
            double tolFeet = EdgeToleranceMm * MmToFeet;

            bool tbHugsRight = System.Math.Abs(tbMax.X - sheetMax.X) <= tolFeet;
            bool tbHugsLeft = System.Math.Abs(tbMin.X - sheetMin.X) <= tolFeet;
            bool tbHugsTop = System.Math.Abs(tbMax.Y - sheetMax.Y) <= tolFeet;
            bool tbHugsBottom = System.Math.Abs(tbMin.Y - sheetMin.Y) <= tolFeet;

            bool tbSpansFullHeight = tbHugsTop && tbHugsBottom;
            bool tbSpansFullWidth = tbHugsLeft && tbHugsRight;

            // ── RightEdge: hugs right, spans full height ──
            if (tbHugsRight && tbSpansFullHeight)
            {
                var large = new RectFeet(sheetMin, new XYZ(tbMin.X, sheetMax.Y, 0));
                return new PlaceableRegion(TitleBlockDetectionMode.RightEdge, large, null, "Right Edge");
            }

            // ── BottomEdge: hugs bottom, spans full width ──
            if (tbHugsBottom && tbSpansFullWidth)
            {
                var large = new RectFeet(new XYZ(sheetMin.X, tbMax.Y, 0), sheetMax);
                return new PlaceableRegion(TitleBlockDetectionMode.BottomEdge, large, null, "Bottom Edge");
            }

            // ── Corner: touches exactly one vertical edge + one horizontal edge, spans neither fully ──
            bool touchesOneVerticalEdge = tbHugsLeft ^ tbHugsRight;   // exactly one, not both, not neither
            bool touchesOneHorizontalEdge = tbHugsTop ^ tbHugsBottom;

            if (touchesOneVerticalEdge && touchesOneHorizontalEdge && !tbSpansFullHeight && !tbSpansFullWidth)
            {
                return ClassifyCorner(sheetMin, sheetMax, tbMin, tbMax, tbHugsRight, tbHugsBottom);
            }

            // ── Anything else: undetected — caller offers manual fallback ──
            return CreateUndetected();
        }

        /// <summary>
        /// Builds the L-shape for a corner title block. Splits the sheet into a
        /// Large rect (the full-width OR full-height strip on the side opposite
        /// the block's horizontal position) and a Small rect (the remainder
        /// beside the block, same row/column as the block but excluding it).
        ///
        /// ASSUMPTION (flagged): Large rect is chosen as the full-width strip
        /// spanning the sheet's other vertical half (top, if block is at
        /// bottom; bottom, if block is at top) — i.e. horizontal banding takes
        /// priority over vertical banding when picking which split is "large".
        /// This matches the common case (title block in a bottom corner,
        /// views read top-to-bottom) but is a judgment call for the reverse
        /// case (block in a top corner) — flagging for review since it wasn't
        /// explicitly specified.
        /// </summary>
        private PlaceableRegion ClassifyCorner(XYZ sheetMin, XYZ sheetMax, XYZ tbMin, XYZ tbMax, bool tbAtRight, bool tbAtBottom)
        {
            RectFeet large, small;

            if (tbAtBottom)
            {
                // Large: full-width strip above the title block's row.
                large = new RectFeet(new XYZ(sheetMin.X, tbMax.Y, 0), sheetMax);

                // Small: remainder beside the title block, same row, excluding it.
                small = tbAtRight
                    ? new RectFeet(sheetMin, new XYZ(tbMin.X, tbMax.Y, 0))          // block bottom-right → small is bottom-left
                    : new RectFeet(new XYZ(tbMax.X, sheetMin.Y, 0), new XYZ(sheetMax.X, tbMax.Y, 0)); // block bottom-left → small is bottom-right
            }
            else
            {
                // Title block at top → Large: full-width strip below the title block's row.
                large = new RectFeet(sheetMin, new XYZ(sheetMax.X, tbMin.Y, 0));

                small = tbAtRight
                    ? new RectFeet(new XYZ(sheetMin.X, tbMin.Y, 0), new XYZ(tbMin.X, sheetMax.Y, 0))   // block top-right → small is top-left
                    : new RectFeet(new XYZ(tbMax.X, tbMin.Y, 0), sheetMax);                             // block top-left → small is top-right
            }

            string corner = (tbAtBottom ? "Bottom" : "Top") + "-" + (tbAtRight ? "Right" : "Left");
            return new PlaceableRegion(TitleBlockDetectionMode.Corner, large, small, $"Corner ({corner})");
        }

        private PlaceableRegion CreateUndetected()
        {
            // Empty placeholder rect — caller (ViewModel) must not use this for
            // packing; Undetected mode signals the UI to request manual input.
            var empty = new RectFeet(XYZ.Zero, XYZ.Zero);
            return new PlaceableRegion(TitleBlockDetectionMode.Undetected, empty, null, "Not detected");
        }

        /// <summary>Builds a Manual-mode single-rect region from user-entered mm coordinates (sheet space).</summary>
        public PlaceableRegion BuildManualRegion(double minXMm, double minYMm, double maxXMm, double maxYMm)
        {
            var min = new XYZ(minXMm * MmToFeet, minYMm * MmToFeet, 0);
            var max = new XYZ(maxXMm * MmToFeet, maxYMm * MmToFeet, 0);
            var rect = new RectFeet(min, max);
            return new PlaceableRegion(TitleBlockDetectionMode.Manual, rect, null, "Manual");
        }
    }
}
