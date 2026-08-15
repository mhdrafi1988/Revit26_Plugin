using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.SheetAutoRearrange.V010.Core.Models
{
    /// <summary>
    /// One sub-row or sub-column of normal views shelved beside a tall/wide
    /// anchor. For a Tall anchor, a "shelf" is a horizontal sub-row (views
    /// side by side, sub-row height = tallest member). For a Wide anchor,
    /// a "shelf" is a vertical sub-column (views stacked, sub-column width =
    /// widest member). For TallAndWide, both kinds are used to fill the
    /// L-shaped remainder around the anchor.
    /// </summary>
    public class ShelfRow
    {
        public List<ViewOnSheetItem> Items { get; } = new();

        /// <summary>Natural size of this shelf before gap distribution (height for a sub-row, width for a sub-column), feet.</summary>
        public double NaturalSizeFeet { get; set; }

        /// <summary>Final placed positions, keyed by ViewportId, after gap distribution has been applied. Populated by ShelfPackingService.</summary>
        public Dictionary<ElementId, XYZ> ResolvedCenters { get; } = new();
    }

    /// <summary>
    /// A tall/wide/both anchor view plus everything packed beside it.
    /// One ShelfBlock is produced per detected anchor by ShelfPackingService;
    /// RearrangeEngine applies ShelfOverflowGrouping to the WHOLE block if
    /// the anchor itself doesn't fit the region.
    /// </summary>
    public class ShelfBlock
    {
        public ViewOnSheetItem Anchor { get; set; } = null!;
        public ViewSizeCategory Category { get; set; }

        /// <summary>Anchor's final placed center, sheet space (feet).</summary>
        public XYZ AnchorCenter { get; set; } = XYZ.Zero;

        /// <summary>True if the anchor itself fits within the resolved PlaceableRegion. If false, ShelfOverflowGrouping decides whether ShelvedRows follow it to overflow or get released back to normal packing.</summary>
        public bool AnchorFits { get; set; }

        /// <summary>
        /// Sub-rows (Tall anchor) or sub-columns (Wide anchor) of normal
        /// views packed beside the anchor. For TallAndWide, this holds the
        /// horizontal sub-rows filling the space beside the tall dimension;
        /// ShelvedColumns holds the vertical fill for the wide dimension —
        /// together they cover the L-shaped remainder around the anchor.
        /// </summary>
        public List<ShelfRow> ShelvedRows { get; } = new();

        /// <summary>Only populated for TallAndWide anchors — the second (column-wise) fill direction.</summary>
        public List<ShelfRow> ShelvedColumns { get; } = new();

        /// <summary>Flat list of every ViewOnSheetItem in this block (anchor + all shelved items), for convenience when applying overflow grouping.</summary>
        public IEnumerable<ViewOnSheetItem> AllItems()
        {
            yield return Anchor;
            foreach (var row in ShelvedRows)
                foreach (var item in row.Items)
                    yield return item;
            foreach (var col in ShelvedColumns)
                foreach (var item in col.Items)
                    yield return item;
        }
    }
}
