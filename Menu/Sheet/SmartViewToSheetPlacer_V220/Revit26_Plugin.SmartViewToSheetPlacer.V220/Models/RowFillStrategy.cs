namespace Revit26_Plugin.SmartViewToSheetPlacer.V220.Models
{
    /// <summary>
    /// V220 NEW: which fill algorithm lays out a sheet's assigned views.
    /// Ported verbatim from SheetAutoRearrange V022's RowFillStrategy (same
    /// 7 strategies, same packing math) — see MasterRowBandPackingService /
    /// FreeRectPackingService / RafisAlgoPackingService for the full
    /// algorithm descriptions, unchanged from that port.
    ///
    /// IMPORTANT DIFFERENCE FROM SheetAutoRearrange: there, a strategy packs
    /// into ONE fixed sheet and reports leftovers as Overflow. Here
    /// (SmartViewToSheetPlacer), reading-order sort + an area-sum capacity
    /// estimate (see ReadingOrderPackingService.PackSingleGroup) decide
    /// which views are CANDIDATES for a given sheet; the chosen strategy
    /// then lays out that candidate slice. Any items the strategy still
    /// can't fit go back to the front of the remaining pool and become
    /// candidates for the next sheet — see ReadingOrderPackingService for
    /// that loop. The strategies themselves (this enum, and the 3 ported
    /// services) are otherwise unmodified from the SheetAutoRearrange port.
    ///
    /// Global default is set once in Stage 2 (Placement Algorithm card);
    /// each generated SheetGroup gets its own FillStrategy property
    /// pre-filled from that default, editable afterward per sheet
    /// (confirmed: "one global option, each sheet has it on in data grid
    /// as template").
    /// </summary>
    public enum RowFillStrategy
    {
        /// <summary>
        /// Band 1 placed left-to-right, Bands 2-4 stacked as same-width
        /// sub-row columns bottom-aligned to the Master Row's bottom edge,
        /// then a gap-fill pass pools any remaining Band 2-4 items into
        /// further columns using the row's full height budget. Simplest,
        /// least space-efficient of the group. Band-based (see
        /// MasterRowBandPackingService).
        /// </summary>
        Shelf,

        /// <summary>
        /// Best-Area-Fit selection against a free-rectangle list, 2-way
        /// (right/below) split per placement. Good balance of simplicity
        /// and packing density. Band-based (see MasterRowBandPackingService).
        /// </summary>
        Guillotine,

        /// <summary>
        /// Best-Short-Side-Fit selection against a free-rectangle list,
        /// up to 4-way split per placement (checked against the consumed
        /// rectangle AND every other free rectangle still in the list).
        /// Densest of the band-based group. Band-based (see
        /// MasterRowBandPackingService).
        /// </summary>
        MaxRects,

        /// <summary>
        /// No Master Row/Band scaffolding. Items sorted ascending by area
        /// (smallest first), packed directly into a single free-rectangle
        /// list spanning the whole region via Best-Short-Side-Fit (ties
        /// broken by the candidate leaving the MAX remaining free area
        /// after split), up to 4-way split per placement. See
        /// FreeRectPackingService.PackMaxFill.
        /// </summary>
        MaxFill,

        /// <summary>
        /// No Master Row/Band scaffolding, but retains its own row/baseline
        /// progression: Master Row height = tallest item still remaining
        /// (recomputed per row). Within each row, a recursive Column ->
        /// Sub-Row -> Sub-Column climb — see RafisAlgoPackingService.
        /// </summary>
        RafisAlgo,

        /// <summary>
        /// No Master Row/Band scaffolding. Bottom-left skyline (1D height
        /// profile across the region's width) — for each item, scans all
        /// x-positions for the lowest resulting skyline height under the
        /// item's footprint, places there, then raises the profile. See
        /// FreeRectPackingService.PackSkyline.
        /// </summary>
        Skyline,

        /// <summary>
        /// Same as Skyline, plus a waste map of small gap rectangles left
        /// below the profile when neighboring columns were already taller.
        /// Before each item's main skyline scan, the waste map is checked
        /// first for a fit. See FreeRectPackingService.PackSkylineWasteMap.
        /// </summary>
        SkylineWasteMap
    }
}
