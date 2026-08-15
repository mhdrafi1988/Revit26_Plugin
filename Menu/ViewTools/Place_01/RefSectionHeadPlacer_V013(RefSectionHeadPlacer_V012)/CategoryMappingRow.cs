using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.RefSectionHeadPlacer.V013.Core.Models
{
    /// <summary>Display wrapper for a drafting view in the mapping ComboBox (Grid 3).
    /// V004: equality is by underlying view ElementId, NOT reference — LoadDraftingViews()
    /// constructs fresh instances on every call, and WPF's ComboBox SelectedItem matching
    /// plus dictionary lookups must treat two wrappers of the same ViewDrafting as equal,
    /// otherwise a rescan orphans every existing selection (Selector pushes null back
    /// through the two-way binding when SelectedItem isn't found in ItemsSource).</summary>
    public class DraftingViewOption
    {
        public string Name { get; }
        public ViewDrafting RevitView { get; }

        public DraftingViewOption(ViewDrafting revitView)
        {
            RevitView = revitView;
            Name = revitView.Name;
        }

        public override string ToString() => Name;

        public override bool Equals(object obj)
            => obj is DraftingViewOption other &&
               other.RevitView?.Id.Value == RevitView?.Id.Value;

        public override int GetHashCode()
            => RevitView?.Id.Value.GetHashCode() ?? 0;
    }

    /// <summary>
    /// Bindable row for Grid 3 — identity is (SourceLabel, Bic, TypeName), matching
    /// ElementTypeRow exactly. Bic is the stable routing key; Category is display
    /// only. Rows are generated from the user's Grid 2 selections (one per selected
    /// row, per link); the user then assigns each a target drafting view.
    ///
    /// V006 (confirmed, Rafi): auto-match removed. Every row starts blank and is
    /// mapped ONLY by explicit user pick — no name-based guessing.
    ///
    /// V010 (confirmed, Rafi): the V008 popover (button + Popup) is REMOVED. The
    /// picker is now an editable, searchable ComboBox living directly in the grid
    /// cell (see RefSectionHeadPlacerWindow.xaml, Grid 3 CellTemplate). Typed text
    /// filters FilteredDraftingViews by substring match (not just prefix, unlike
    /// WPF's native ComboBox text-search) — SearchText/FilteredDraftingViews are
    /// KEPT from V008 and simply re-purposed: they used to feed a Popup's ListBox,
    /// now they feed the ComboBox's own drop-down ItemsSource directly.
    ///
    /// IsPopoverOpen and SelectDraftingView(...)'s popover-closing responsibility
    /// are gone — an editable ComboBox opens/closes its own drop-down natively, and
    /// commits a pick via its own SelectionChanged, so no code-behind popover-find
    /// workaround is needed anymore either (see window code-behind diff).
    ///
    /// Un-mapping a row is NOT supported via the ComboBox (confirmed, Rafi) — to
    /// exclude a row from Run, untick its IsSelected checkbox instead; the mapped
    /// value is left as-is and simply ignored by the engine for unselected rows.
    ///
    /// V012 (confirmed, Rafi) — behavior fixes, same visual:
    ///   1. Typing auto-opens the drop-down (code-behind MappingCombo_TextChanged).
    ///   2. Dismissing without a pick reverts stray typed text to the mapped
    ///      view's name (RevertSearchTextToPick, called from code-behind).
    ///   3. FilteredDraftingViews mutates only when contents actually change
    ///      (ReplaceFiltered), and the current pick is re-highlighted on
    ///      DropDownOpened — no more lost selection from list churn.
    ///   4. The "isShowingCurrentPickUnedited" filter hack is removed;
    ///      DropDownOpened now explicitly requests the full list (ShowFullList).
    /// </summary>
    public partial class CategoryMappingRow : ObservableObject
    {
        [ObservableProperty]
        private bool isSelected = true;

        public string SourceLabel { get; }
        public string Category { get; }
        public BuiltInCategory Bic { get; }
        public string TypeName { get; }

        /// <summary>Combined label shown in the grid, e.g.
        /// "ARCH-Link.rvt · Plumbing Fixtures · Floor Drain 100mm".</summary>
        public string DisplayLabel => $"{SourceLabel} · {Category} · {TypeName}";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MappedDraftingViewDisplay))]
        private DraftingViewOption mappedDraftingView;

        /// <summary>Fallback placeholder text shown when nothing is mapped yet
        /// (used by the ComboBox's Text binding when MappedDraftingView is null).</summary>
        public string MappedDraftingViewDisplay => MappedDraftingView?.Name ?? "— pick —";

        // ── V010: per-row live-search state for the ComboBox drop-down ──────
        [ObservableProperty]
        private string searchText = string.Empty;

        partial void OnSearchTextChanged(string value) => ApplyFilter();

        /// <summary>Full drafting-view list — set once by the ViewModel, shared
        /// (same instance) across every row; this row only reads it to filter.</summary>
        private IReadOnlyList<DraftingViewOption> _allDraftingViews = Array.Empty<DraftingViewOption>();

        /// <summary>This row's own live-narrowed list, bound to the ComboBox's
        /// ItemsSource — narrows by substring as SearchText changes.</summary>
        public ObservableCollection<DraftingViewOption> FilteredDraftingViews { get; } = new();

        /// <summary>Called once by the ViewModel after construction (and again if the
        /// drafting-view list is refreshed) to give this row something to filter.</summary>
        public void SetAvailableDraftingViews(IReadOnlyList<DraftingViewOption> allViews)
        {
            _allDraftingViews = allViews ?? Array.Empty<DraftingViewOption>();

            // V012: if a pick was already restored (ctor set searchText to its
            // name before this ran), show the FULL list rather than narrowing to
            // just that name — same fix as SelectDraftingView/RevertSearchTextToPick,
            // otherwise a session-restored row starts narrowed-to-one and every
            // click on it rebuilds the list from scratch.
            if (string.IsNullOrEmpty(SearchText) || MappedDraftingView == null)
                ApplyFilter();
            else
                ShowFullList();
        }

        /// <summary>V012: plain substring narrowing — the V010
        /// "isShowingCurrentPickUnedited" special case is GONE. It existed only
        /// because nothing distinguished "box passively showing the pick" from
        /// "user typed this"; the window code-behind now calls ShowFullList() on
        /// DropDownOpened instead, which is the honest version of that intent.</summary>
        private void ApplyFilter()
        {
            var search = SearchText?.Trim() ?? string.Empty;
            ReplaceFiltered(search.Length == 0
                ? _allDraftingViews
                : _allDraftingViews
                    .Where(v => v.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList());
        }

        /// <summary>V012: called by the window code-behind on DropDownOpened —
        /// opening the drop-down always offers the FULL list regardless of what
        /// the box currently displays (typically the current pick's name, which
        /// would otherwise narrow the list to one item).</summary>
        public void ShowFullList() => ReplaceFiltered(_allDraftingViews);

        /// <summary>V012: mutates FilteredDraftingViews ONLY when the target set
        /// actually differs. The V010 unconditional Clear()+re-Add on every
        /// keystroke nulled the ComboBox's internal selection each time (no
        /// highlight on reopen) and caused caret/flicker churn mid-type.</summary>
        private void ReplaceFiltered(IReadOnlyList<DraftingViewOption> target)
        {
            if (FilteredDraftingViews.Count == target.Count &&
                FilteredDraftingViews.SequenceEqual(target))
                return;

            FilteredDraftingViews.Clear();
            foreach (var v in target)
                FilteredDraftingViews.Add(v);
        }

        /// <summary>V012: called by the window code-behind when the ComboBox is
        /// dismissed (drop-down closed / keyboard focus left) without a pick —
        /// reverts any stray typed text back to the mapped view's name (or empty
        /// if unmapped). Without this, typing "xyz" and clicking away left "xyz"
        /// in the box while the actual mapping was unchanged — the row LOOKED
        /// remapped. No-op after a genuine pick, since SelectDraftingView has
        /// already set SearchText to the picked name by then.
        ///
        /// Also restores the full list (see SelectDraftingView) so this row
        /// doesn't sit narrowed-to-one between visits, which was the source of
        /// the every-click rebuild delay.</summary>
        public void RevertSearchTextToPick()
        {
            var pickName = MappedDraftingView?.Name ?? string.Empty;
            if (!string.Equals(SearchText, pickName, StringComparison.Ordinal))
                SearchText = pickName;
            ShowFullList();
        }

        /// <summary>Bound to the ComboBox's SelectionChanged (code-behind) — commits
        /// the pick. Unlike V008 there is no popover to close; the ComboBox closes
        /// its own drop-down natively on selection.
        ///
        /// FIX (V010): SearchText is set to the PICKED NAME here, not cleared to
        /// empty. The ComboBox's Text is bound to SearchText (so typing filters
        /// live), which means an empty SearchText would show a blank box even for
        /// an already-mapped row. Setting it to the pick's name keeps the box
        /// showing what's mapped once the pick is made, matching the old popover
        /// button's display behavior.
        ///
        /// FIX (V012 — every-click delay): setting SearchText above fires
        /// OnSearchTextChanged -> ApplyFilter, which narrows FilteredDraftingViews
        /// down to just the picked name (usually 1 item). Every later click on
        /// this row's ComboBox then hit ShowFullList(), comparing that 1-item list
        /// against the full list — never equal, so ReplaceFiltered rebuilt on
        /// EVERY click, forever, for that row. Calling ShowFullList() here puts
        /// the full list back immediately after the pick, so by the time the user
        /// clicks again, DropDownOpened's ShowFullList() is comparing full-vs-full
        /// and skips the rebuild entirely.</summary>
        public void SelectDraftingView(DraftingViewOption view)
        {
            MappedDraftingView = view;
            SearchText = view?.Name ?? string.Empty;
            ShowFullList();
        }

        public CategoryMappingRow(
            string sourceLabel, string category, string typeName,
            BuiltInCategory bic, DraftingViewOption mappedDraftingView = null)
        {
            SourceLabel = sourceLabel;
            Category = category;
            TypeName = typeName;
            Bic = bic;
            this.mappedDraftingView = mappedDraftingView;

            // V010: if a pick is restored from session memory (RebuildMappings),
            // show its name in the box immediately rather than starting blank —
            // otherwise a restored mapping would look unmapped until the user
            // interacts with the row.
            searchText = mappedDraftingView?.Name ?? string.Empty;
        }
    }
}
