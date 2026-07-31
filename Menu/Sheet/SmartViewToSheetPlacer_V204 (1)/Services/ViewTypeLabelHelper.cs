using Autodesk.Revit.DB;

namespace Revit26_Plugin.SmartViewToSheetPlacer.V204.Services
{
    /// <summary>
    /// Single source of truth for human-friendly ViewType labels. Previously
    /// duplicated as three separate switch statements: ViewModel's
    /// FriendlyLabel, Handler's FriendlyViewType, and SheetGroup's
    /// PluralizedTypeLabel (V203). Consolidated here in V204 — any new
    /// ViewType label mapping only needs to change in one place.
    /// </summary>
    public static class ViewTypeLabelHelper
    {
        /// <summary>Singular display label, e.g. "Floor Plan", "Structural Plan".</summary>
        public static string Label(ViewType t) => t switch
        {
            ViewType.FloorPlan => "Floor Plan",
            ViewType.CeilingPlan => "Ceiling Plan",
            ViewType.Elevation => "Elevation",
            ViewType.Section => "Section",
            ViewType.ThreeD => "3D View",
            ViewType.DraftingView => "Drafting",
            ViewType.AreaPlan => "Area Plan",
            ViewType.EngineeringPlan => "Structural Plan",
            ViewType.Detail => "Detail",
            ViewType.Legend => "Legend",
            _ => t.ToString()
        };

        /// <summary>
        /// Pluralized label used in SheetGroup.GeneratedName, e.g. "Floor Plans",
        /// "Sections". Irregular plurals (Detail -> Detail, not Details) are
        /// intentional per the confirmed naming convention carried over from V203.
        /// </summary>
        public static string Pluralize(ViewType t) => t switch
        {
            ViewType.FloorPlan => "Floor Plans",
            ViewType.CeilingPlan => "Ceiling Plans",
            ViewType.Elevation => "Elevations",
            ViewType.Section => "Sections",
            ViewType.ThreeD => "3D Views",
            ViewType.DraftingView => "Drafting",
            ViewType.AreaPlan => "Area Plans",
            ViewType.EngineeringPlan => "Structural Plans",
            ViewType.Detail => "Detail",
            ViewType.Legend => "Legends",
            _ => t.ToString()
        };
    }
}
