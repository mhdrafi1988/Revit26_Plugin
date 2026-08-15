namespace Revit26_Plugin.SheetAutoRearrange.V016.Core.Models
{
    /// <summary>
    /// V014 NEW: which fill algorithm MasterRowBandPackingService uses to
    /// place items within each Master Row. Replaces the V008-V013 Tall/Wide
    /// anchor + L-shape shelf system entirely — there is no anchor concept
    /// in any of the three strategies below. All three share identical
    /// Master Row setup and Band assignment (see MasterRowBandPackingService
    /// header comment); only the in-row placement logic differs.
    /// </summary>
    public enum RowFillStrategy
    {
        /// <summary>
        /// Band 1 placed left-to-right, Bands 2-4 stacked as same-width
        /// sub-row columns bottom-aligned to the Master Row's bottom edge,
        /// then a gap-fill pass pools any remaining Band 2-4 items into
        /// further columns using the row's full height budget. Simplest,
        /// least space-efficient of the three.
        /// </summary>
        Shelf,

        /// <summary>
        /// Best-Area-Fit selection against a free-rectangle list, 2-way
        /// (right/below) split per placement. Good balance of simplicity
        /// and packing density.
        /// </summary>
        Guillotine,

        /// <summary>
        /// Best-Short-Side-Fit selection against a free-rectangle list,
        /// up to 4-way split per placement (checked against the consumed
        /// rectangle AND every other free rectangle still in the list).
        /// Densest of the three, especially once item widths and heights
        /// both vary — more bookkeeping than Guillotine.
        /// </summary>
        MaxRects
    }
}
