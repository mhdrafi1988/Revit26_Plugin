using System.Collections.ObjectModel;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.SmartViewToSheetPlacer.V212.Models
{
    /// <summary>
    /// Groups SuggestedSheets by RevitViewType for Stage 5's "Newly Created
    /// Sheets" grid. Confirmed with Rafi:
    ///   - Collapsible accordion-style group header (chevron), same pattern
    ///     as the 5 main stages / Activity Log.
    ///   - All groups start COLLAPSED by default when Stage 5 first loads.
    ///   - Global Select All / Clear Selection buttons only affect rows in
    ///     currently-EXPANDED groups — collapsed groups are left untouched.
    ///   - Each group header also gets its own "select all in this group"
    ///     checkbox, independent of the global buttons.
    ///   - Header shows a sheet-count badge, e.g. "Floor Plan (3 sheets)".
    /// Rebuilt every time RunPacking() rebuilds SuggestedSheets (Stage2.cs) —
    /// same rebuild point as InputCountsByType, so it always reflects the
    /// current suggested-sheet set.
    /// </summary>
    public partial class SheetTypeGroup : ObservableObject
    {
        public ViewType RevitViewType { get; }
        public string ViewTypeLabel { get; }

        /// <summary>Sheets belonging to this ViewType group, in suggested order.</summary>
        public ObservableCollection<SheetGroup> Sheets { get; } = new();

        /// <summary>Display text for the group header, e.g. "Floor Plan (3 sheets)".
        /// Recomputed whenever Sheets changes (see constructor wiring).</summary>
        public string HeaderText => $"{ViewTypeLabel} ({Sheets.Count} sheet{(Sheets.Count == 1 ? "" : "s")})";

        /// <summary>Whether this group's card is expanded. Confirmed: starts
        /// collapsed (false) every time Stage 5 loads — not persisted, unlike
        /// the Activity Log's expand state, since this is a work-in-progress
        /// review list rather than a standing preference.</summary>
        [ObservableProperty]
        private bool _isExpanded;

        /// <summary>
        /// Per-group "select all in this group" checkbox state. This is a
        /// three-way-feeling but strictly two-way bool: checking it sets
        /// OpenAfterPlacement=true on every sheet in THIS group only;
        /// unchecking sets them all false. Does not attempt tri-state
        /// (partial-selection) display — reflects the state of the last
        /// explicit check/uncheck action on this control, not a computed
        /// aggregate of the sheets' individual states (kept simple per
        /// Rafi's "keep this compact" pattern elsewhere in the tool).
        /// </summary>
        [ObservableProperty]
        private bool _isGroupSelectAllChecked;

        public SheetTypeGroup(ViewType revitViewType, string viewTypeLabel)
        {
            RevitViewType = revitViewType;
            ViewTypeLabel = viewTypeLabel;

            Sheets.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HeaderText));
        }

        partial void OnIsGroupSelectAllCheckedChanged(bool value)
        {
            foreach (var sheet in Sheets)
                sheet.OpenAfterPlacement = value;
        }
    }
}
