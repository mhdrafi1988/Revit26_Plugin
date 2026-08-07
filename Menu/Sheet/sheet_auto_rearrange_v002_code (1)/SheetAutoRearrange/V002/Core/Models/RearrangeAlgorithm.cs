namespace Revit26_Plugin.SheetAutoRearrange.V002.Core.Models
{
    /// <summary>
    /// Selects which repacking algorithm Run() uses for ticked views.
    /// </summary>
    public enum RearrangeAlgorithm
    {
        /// <summary>
        /// Ported from SmartViewToSheetPlacer V213 ReadingOrderPackingService.
        /// Sorts by crop-box position (reading order) and repacks in two-phase rows.
        /// </summary>
        ReadingOrder,

        /// <summary>
        /// Groups current viewports into rows by vertical position (row tolerance),
        /// top row first, each row ordered right-to-left, then repacks with
        /// row alignment (Top/Center/Bottom) and block alignment (H/V) to the
        /// titleblock usable area. Default algorithm for this tool.
        /// </summary>
        SheetOrder
    }
}
