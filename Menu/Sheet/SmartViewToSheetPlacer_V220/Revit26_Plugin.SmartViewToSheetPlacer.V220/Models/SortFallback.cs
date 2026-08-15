namespace Revit26_Plugin.SmartViewToSheetPlacer.V220.Models
{
    /// <summary>
    /// How to order views whose crop-box center could not be resolved
    /// (e.g. view has no crop box, or CropBox read failed).
    /// Ported from APUS_V321_01 (Models/SortFallback.cs) — same two values.
    /// Confirmed behavior (per Rafi): unresolved views are always pushed to
    /// the end of the sort, logged as a Warning, and reported as
    /// "Placed (Unresolved order)" in the Stage 2/3 grid Status column —
    /// they are NOT skipped or failed, only sorted last.
    /// </summary>
    public enum SortFallback
    {
        /// <summary>Unresolvable views are placed after all resolvable ones (default).</summary>
        PushToEnd,

        /// <summary>Unresolvable views are sorted alphabetically among themselves, still pushed to end.</summary>
        SortByName
    }
}
