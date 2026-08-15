namespace Revit26_Plugin.SmartViewToSheetPlacer.V220.Models
{
    /// <summary>
    /// Stage 1's view-selection grid filter: show all views, only views
    /// already placed on a real Revit sheet (ViewInfo.IsAlreadyPlaced), or
    /// only views not yet placed. Confirmed with Rafi: simple two-button
    /// toggle above the grid (not a popover).
    /// </summary>
    public enum PlacementFilterMode
    {
        All,
        Placed,
        NotPlaced
    }
}
