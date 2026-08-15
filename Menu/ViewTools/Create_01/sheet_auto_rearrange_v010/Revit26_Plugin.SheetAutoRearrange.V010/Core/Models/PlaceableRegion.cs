using Autodesk.Revit.DB;

namespace Revit26_Plugin.SheetAutoRearrange.V010.Core.Models
{
    /// <summary>
    /// How the usable placement area was determined for the active sheet.
    ///
    /// V008 CHANGE: the V006/V007 RightEdge/BottomEdge/Corner classification
    /// model is REMOVED. Root cause: it compared the title block's bbox
    /// against a separately-derived "sheet bbox" (from
    /// ViewSheet.get_BoundingBox(null)), which Revit's API does not return
    /// reliably for sheets — it frequently yields a fixed ±100ft generic
    /// placeholder instead of the sheet's real extent, so every "hugs edge"
    /// comparison failed and every sheet fell through to Undetected.
    ///
    /// The corrected model (confirmed working in this suite's own
    /// SmartViewToSheetPlacer tool): the title block FamilyInstance's own
    /// bounding box IS the reliable, authoritative reference — there is no
    /// separate "sheet bbox" to compare it against. The placeable area is
    /// simply the title block's bbox, inset by the user's configured
    /// margins (GapSettings.MarginTopMm/BottomMm/LeftMm/RightMm).
    /// </summary>
    public enum TitleBlockDetectionMode
    {
        /// <summary>Title block instance found; usable area = its bbox inset by margins.</summary>
        Detected,

        /// <summary>No title block instance found (or 2+ found — see TitleBlockDetectionResult.MultipleTitleBlocksFound), or its bbox could not be read. Caller must fall back to manual input.</summary>
        Undetected,

        /// <summary>User-entered rectangle — used after Undetected, or if the user overrides a successful detection.</summary>
        Manual
    }

    /// <summary>
    /// A single axis-aligned rectangle in sheet space (feet, Revit internal units).
    /// </summary>
    public readonly struct RectFeet
    {
        public XYZ Min { get; }
        public XYZ Max { get; }

        public RectFeet(XYZ min, XYZ max)
        {
            Min = min;
            Max = max;
        }

        public double Width => Max.X - Min.X;
        public double Height => Max.Y - Min.Y;

        public bool ContainsYBand(double bandTop, double bandBottom)
            => bandTop <= Max.Y + 1e-6 && bandBottom >= Min.Y - 1e-6;
    }

    /// <summary>
    /// Resolved usable placement area for a sheet.
    ///
    /// V008 CHANGE: always a SINGLE rectangle now — LargeRect is the title
    /// block's bbox inset by margins; SmallRect is retained on the type
    /// (always null in V008) purely so ShelfPackingService's tall/wide-view
    /// L-shape logic — which is UNRELATED to title block classification, it
    /// operates on space carved out by an oversized VIEW, not by the title
    /// block — continues to compile and degrade gracefully (GetUsableXRangeAtY
    /// simply always returns LargeRect's range when SmallRect is null).
    /// </summary>
    public class PlaceableRegion
    {
        public TitleBlockDetectionMode Mode { get; }

        /// <summary>The usable rectangle. Always the full placeable area in V008 (title block bbox inset by margins) — never a sub-split.</summary>
        public RectFeet LargeRect { get; }

        /// <summary>Always null in V008 — retained only for ShelfPackingService's unrelated tall/wide-view L-shape mechanism, which reuses this same type.</summary>
        public RectFeet? SmallRect { get; }

        /// <summary>Human-readable classification text for the UI (e.g. "Detected", "Manual").</summary>
        public string DisplayText { get; }

        public PlaceableRegion(TitleBlockDetectionMode mode, RectFeet largeRect, RectFeet? smallRect, string displayText)
        {
            Mode = mode;
            LargeRect = largeRect;
            SmallRect = smallRect;
            DisplayText = displayText;
        }

        /// <summary>Always false in V008 — no title-block-driven L-shape exists anymore.</summary>
        public bool IsLShape => SmallRect.HasValue;

        public (double minX, double maxX, RectFeet usedRect) GetUsableXRangeAtY(double bandTop, double bandBottom)
        {
            if (SmallRect.HasValue)
            {
                bool inLarge = LargeRect.ContainsYBand(bandTop, bandBottom);
                bool inSmall = SmallRect.Value.ContainsYBand(bandTop, bandBottom);

                if (inSmall && !inLarge)
                    return (SmallRect.Value.Min.X, SmallRect.Value.Max.X, SmallRect.Value);
            }

            return (LargeRect.Min.X, LargeRect.Max.X, LargeRect);
        }

        public RectFeet GetOverallBounds()
        {
            if (!SmallRect.HasValue)
                return LargeRect;

            double minX = System.Math.Min(LargeRect.Min.X, SmallRect.Value.Min.X);
            double minY = System.Math.Min(LargeRect.Min.Y, SmallRect.Value.Min.Y);
            double maxX = System.Math.Max(LargeRect.Max.X, SmallRect.Value.Max.X);
            double maxY = System.Math.Max(LargeRect.Max.Y, SmallRect.Value.Max.Y);
            return new RectFeet(new XYZ(minX, minY, 0), new XYZ(maxX, maxY, 0));
        }
    }
}
