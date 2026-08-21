using System.Collections.ObjectModel;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.SmartViewToSheetPlacer.V221.Models
{
    /// <summary>
    /// V213 UPDATE: one card per SHEET now, not per ViewType — confirmed
    /// with Rafi: "group by planned/available sheet numbers" means each
    /// collapsible card represents a single suggested sheet, identified by
    /// its own GeneratedName (e.g. "Floor Plans (3)"), with the inner
    /// DataGrid holding just that one sheet's own row. Sheets is therefore
    /// always Count==1 now — kept as a collection (not a single SheetGroup
    /// property) to avoid reworking the XAML's ItemsSource="{Binding Sheets}"
    /// binding inside each card's DataGrid.
    ///
    /// Confirmed with Rafi:
    ///   - Collapsible accordion-style group header (chevron), same pattern
    ///     as the 5 main stages / Activity Log.
    ///   - All groups start COLLAPSED by default when Stage 5 first loads.
    ///   - Global Select All / Clear Selection buttons only affect rows in
    ///     currently-EXPANDED groups — collapsed groups are left untouched.
    ///   - Each group header also gets its own "select all in this group"
    ///     checkbox, independent of the global buttons (now equivalent to a
    ///     single-sheet toggle, since each group is one sheet).
    /// Rebuilt every time RunPacking() rebuilds SuggestedSheets (Stage2.cs) —
    /// same rebuild point as InputCountsByType, so it always reflects the
    /// current suggested-sheet set.
    /// </summary>
    public partial class SheetTypeGroup : ObservableObject
    {
        public ViewType RevitViewType { get; }

        /// <summary>V213: now holds the sheet's own GeneratedName (e.g.
        /// "Floor Plans (3)") rather than a ViewType label — see HeaderText.
        /// Property name kept as ViewTypeLabel to avoid touching the XAML
        /// binding ({Binding ViewTypeLabel} is no longer used directly; see
        /// HeaderText instead) and other call sites unnecessarily.</summary>
        public string ViewTypeLabel { get; }

        /// <summary>The single sheet this card represents, wrapped in a
        /// collection so the existing XAML DataGrid binding
        /// (ItemsSource="{Binding Sheets}") keeps working unchanged — always
        /// Count==1 after the V213 grouping change.</summary>
        public ObservableCollection<SheetGroup> Sheets { get; } = new();

        /// <summary>Display text for the group header. V213 UPDATE: now just
        /// the sheet's own name (e.g. "Floor Plans (3)") — no longer appends
        /// a redundant "(N sheets)" suffix, since each card is always exactly
        /// one sheet after the grouping change.</summary>
        public string HeaderText => ViewTypeLabel;

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
