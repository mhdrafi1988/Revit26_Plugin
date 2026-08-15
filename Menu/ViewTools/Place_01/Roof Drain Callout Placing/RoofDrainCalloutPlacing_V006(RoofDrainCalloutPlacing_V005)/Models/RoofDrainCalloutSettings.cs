namespace Revit26_Plugin.RoofDrainCalloutPlacing.V006.Models
{
    /// <summary>
    /// Persisted to %AppData%\Revit26_Plugin\RoofDrainCalloutPlacing\settings.json
    /// via System.Text.Json. Loaded on window open, saved on close and after Run.
    /// Drafting view reference is stored as UniqueId (ElementId is
    /// document-session-scoped and not stable across sessions) and re-resolved
    /// on load; if the saved view no longer exists, the ViewModel falls back
    /// to its normal default-selection logic.
    ///
    /// V004: no Mode (mode selector removed), no SelectedPlanViewUniqueId (no
    /// Plan View dropdown — roof is picked directly and intentionally not
    /// persisted across sessions), no ReviewListExpanded (review grid removed).
    /// </summary>
    public class RoofDrainCalloutSettings
    {
        public string GroupingToleranceMmText { get; set; } = "500";
        public string CalloutMarginMmText { get; set; } = "150";
        public string CalloutFloorMmText { get; set; } = "500";
        public string DuplicateToleranceMmText { get; set; } = "300";
        public string SnapToleranceMmText { get; set; } = "150";

        public string SelectedDraftingViewUniqueId { get; set; }
    }
}
