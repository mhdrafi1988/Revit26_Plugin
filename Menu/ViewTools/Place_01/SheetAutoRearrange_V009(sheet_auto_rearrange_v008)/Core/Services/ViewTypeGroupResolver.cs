using Autodesk.Revit.DB;

namespace Revit26_Plugin.SheetAutoRearrange.V009.Core.Services
{
    /// <summary>
    /// Single source of truth for mapping a Revit ViewType to this tool's
    /// four UI gap-groups ("Floor Plan", "Section / Elevation", "3D View",
    /// "Schedule") and to the View Type filter popover's seven checkboxes.
    /// Previously this mapping was duplicated three times (both packing
    /// services + the ViewModel's filter) with different fallback behavior
    /// for unmapped ViewTypes — consolidated here so all three call sites
    /// agree, and so any new Revit ViewType only needs to be added once.
    /// </summary>
    public static class ViewTypeGroupResolver
    {
        /// <summary>Maps a ViewType to one of the four gap-settings group names.</summary>
        public static string ToGapGroupName(ViewType viewType) => viewType switch
        {
            ViewType.FloorPlan or ViewType.CeilingPlan or ViewType.AreaPlan or ViewType.EngineeringPlan => "Floor Plan",
            ViewType.Section or ViewType.Elevation or ViewType.Detail => "Section / Elevation",
            ViewType.ThreeD => "3D View",
            ViewType.Schedule => "Schedule",
            // ASSUMPTION: any other ViewType (Legend, DraftingView, Walkthrough,
            // Rendering, etc.) falls back to "Floor Plan" gap settings since it
            // has no dedicated group in the current UI. Flag if this needs its
            // own group later.
            _ => "Floor Plan"
        };

        /// <summary>
        /// Maps a ViewType to one of the seven View Type filter popover
        /// categories (Floor Plan / Section / Elevation / 3D View / Legend /
        /// Schedule / Drafting View). Returns null for any ViewType with no
        /// matching filter checkbox — callers should decide the fallback
        /// (this tool's ViewModel defaults unmapped types to always-visible).
        /// </summary>
        public static string? ToFilterCategory(ViewType viewType) => viewType switch
        {
            ViewType.FloorPlan or ViewType.CeilingPlan or ViewType.AreaPlan or ViewType.EngineeringPlan => "FloorPlan",
            ViewType.Section => "Section",
            ViewType.Elevation => "Elevation",
            ViewType.ThreeD => "ThreeD",
            ViewType.Legend => "Legend",
            ViewType.Schedule => "Schedule",
            ViewType.DraftingView => "DraftingView",
            _ => null
        };
    }
}
