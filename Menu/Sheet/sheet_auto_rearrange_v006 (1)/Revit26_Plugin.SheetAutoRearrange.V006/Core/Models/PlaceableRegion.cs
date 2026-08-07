using Autodesk.Revit.DB;

namespace Revit26_Plugin.SheetAutoRearrange.V006.Core.Models
{
    /// <summary>
    /// How the usable placement area was determined for the active sheet.
    /// </summary>
    public enum TitleBlockDetectionMode
    {
        /// <summary>Title block spans full sheet height, hugs the right edge — usable area is a single rectangle.</summary>
        RightEdge,

        /// <summary>Title block spans full sheet width, hugs the bottom edge — usable area is a single rectangle.</summary>
        BottomEdge,

        /// <summary>Title block touches two adjacent sheet edges (a corner) without spanning either fully — usable area is an L-shape (Large + Small rect).</summary>
        Corner,

        /// <summary>Title block position could not be classified (floating / non-standard). User must supply the usable rectangle manually.</summary>
        Undetected,

        /// <summary>User-entered rectangle — used after Undetected, or if the user overrides auto-detection.</summary>
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

        /// <summary>True if this row's Y-band (given by its vertical extent) falls within this rect's Y-range.</summary>
        public bool ContainsYBand(double bandTop, double bandBottom)
            => bandTop <= Max.Y + 1e-6 && bandBottom >= Min.Y - 1e-6;
    }

    /// <summary>
    /// Resolved usable placement area for a sheet: either a single rectangle
    /// (RightEdge / BottomEdge / Undetected-manual cases) or an L-shape made of
    /// a Large rect + a Small rect (Corner case). Packing services query
    /// <see cref="GetUsableXRangeAtY"/> per row-band rather than assuming one
    /// flat rectangle. Block alignment (H/V) always targets <see cref="LargeRect"/>.
    /// </summary>
    public class PlaceableRegion
    {
        public TitleBlockDetectionMode Mode { get; }

        /// <summary>The larger of the two sub-rectangles. For single-rect modes, this IS the whole usable area. Alignment targets this rect only.</summary>
        public RectFeet LargeRect { get; }

        /// <summary>Present only for Mode == Corner. The remainder strip beside/above the title block, narrower than LargeRect.</summary>
        public RectFeet? SmallRect { get; }

        /// <summary>Human-readable classification text for the UI (e.g. "Corner (Bottom-Right)").</summary>
        public string DisplayText { get; }

        public PlaceableRegion(TitleBlockDetectionMode mode, RectFeet largeRect, RectFeet? smallRect, string displayText)
        {
            Mode = mode;
            LargeRect = largeRect;
            SmallRect = smallRect;
            DisplayText = displayText;
        }

        public bool IsLShape => SmallRect.HasValue;

        /// <summary>
        /// Returns the usable X-range (min, max) for a row whose vertical band spans
        /// [bandBottom, bandTop]. If the band overlaps the notch region (falls only
        /// within SmallRect's Y-range and not LargeRect's), the row is constrained to
        /// SmallRect's width. Otherwise it uses LargeRect. Rows never spill across
        /// both sub-rects on the same row — per confirmed design.
        /// </summary>
        public (double minX, double maxX, RectFeet usedRect) GetUsableXRangeAtY(double bandTop, double bandBottom)
        {
            if (SmallRect.HasValue)
            {
                bool inLarge = LargeRect.ContainsYBand(bandTop, bandBottom);
                bool inSmall = SmallRect.Value.ContainsYBand(bandTop, bandBottom);

                // Band falls only within the small rect's Y-range (below/beside the
                // large rect, in the notch) — constrain to the narrower rect.
                if (inSmall && !inLarge)
                    return (SmallRect.Value.Min.X, SmallRect.Value.Max.X, SmallRect.Value);
            }

            return (LargeRect.Min.X, LargeRect.Max.X, LargeRect);
        }

        /// <summary>Convenience: overall bounding box across both sub-rects, used only for preview/canvas framing — never for packing math.</summary>
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
