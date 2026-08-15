namespace Revit26_Plugin.SheetAutoRearrange.V008.Core.Models
{
    /// <summary>
    /// Result of classifying a view against the mode height/width of the
    /// ticked set, per TallWideDetectionSettings. A view can be flagged on
    /// either axis independently, or both (per confirmed design: "both" gets
    /// a full 2D block treatment).
    /// </summary>
    public enum ViewSizeCategory
    {
        Normal,
        Tall,
        Wide,
        TallAndWide
    }

    /// <summary>How an overflowing tall/wide/both anchor block's shelved views are handled.</summary>
    public enum ShelfOverflowGrouping
    {
        /// <summary>Anchor view + everything shelved beside it move to the overflow zone together, preserving the shelf layout.</summary>
        KeepShelfTogether,

        /// <summary>Only the anchor view overflows; its shelved views are released back into normal packing.</summary>
        RepackIndividually
    }
}
