// File: Models/PlacementAlgorithm.cs
namespace Revit26_Plugin.APUS.V322.Models
{
    /// <summary>
    /// V321: only one live placement algorithm remains.
    /// BinPacking, ReadingOrder, AdaptiveGrid, ReadingOrderBinPacking, and
    /// MultiSheetOptimizer were dead code in V320 (never dispatched to) and
    /// have been removed rather than carried forward.
    /// </summary>
    public enum PlacementAlgorithm
    {
        Grid
    }
}
