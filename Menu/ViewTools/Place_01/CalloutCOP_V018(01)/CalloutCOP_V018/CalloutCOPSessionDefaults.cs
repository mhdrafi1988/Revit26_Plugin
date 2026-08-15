using System.Collections.Generic;
using Revit26_Plugin.CalloutCOP.V018.ViewModels;

namespace Revit26_Plugin.CalloutCOP.V018.Helpers
{
    /// <summary>
    /// In-memory-only "last used" state for the Callout COP tool, scoped to the
    /// current Revit process/session. NOT persisted to settings.json - resets
    /// whenever Revit restarts (per Rafi's explicit "in-memory only" instruction).
    ///
    /// Holds:
    ///  - Last-used drafting view name per bulk-fill slot (Left/Center/Right),
    ///    used to pre-fill the bulk-fill combos AND any per-row combo slot
    ///    that doesn't already have a real assignment on window open.
    ///  - Last-used sheet-filter checklist selection, restored on every open
    ///    after the first. The active-sheet auto-default only applies when
    ///    HasSheetFilterSelection is still false (i.e. very first open this
    ///    session) - once a selection exists, active view is ignored entirely.
    ///
    /// Matching is by drafting-view Name (not ElementId) since the
    /// DraftingViewItemViewModel wrapper is rebuilt fresh each time the window
    /// opens and reference equality would never match across instances.
    /// </summary>
    public static class CalloutCOPSessionDefaults
    {
        public static string LastLeftViewName { get; set; }
        public static string LastCenterViewName { get; set; }
        public static string LastRightViewName { get; set; }

        /// <summary>Last-used sheet filter checklist selection. Null/empty + IsAllSelected=true means "ALL".</summary>
        public static HashSet<string> LastSelectedSheetNumbers { get; set; }

        /// <summary>True once a sheet-filter selection has been made at least once this session.</summary>
        public static bool HasSheetFilterSelection { get; set; }

        public static bool LastIsAllSelected { get; set; } = true;

        /// <summary>
        /// Finds the matching wrapper for a remembered drafting-view name within
        /// the current window's drafting view list. Returns null if not found
        /// (e.g. the remembered view was deleted) or if no name was remembered yet.
        /// </summary>
        public static DraftingViewItemViewModel ResolveLastUsed(
            string rememberedName,
            IEnumerable<DraftingViewItemViewModel> allDraftingViews)
        {
            if (string.IsNullOrEmpty(rememberedName))
                return null;

            foreach (var dv in allDraftingViews)
            {
                if (dv.Name == rememberedName)
                    return dv;
            }

            return null;
        }
    }
}
