using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CalloutCOP.V018.ViewModels
{
    /// <summary>
    /// Wraps a ViewDrafting for combo-box binding, adding detail number(s) and
    /// sheet placement(s) so the dropdown list can show more than just the name.
    /// Closed/selected combo box still displays only <see cref="Name"/>
    /// (bound via DisplayMemberPath); the rich columns are for the open
    /// dropdown row template only (Name | Detail # | Sheet).
    /// </summary>
    public class DraftingViewItemViewModel
    {
        /// <summary>The underlying Revit drafting view.</summary>
        public ViewDrafting View { get; }

        /// <summary>Drafting view name - the only thing shown in the closed combo box.</summary>
        public string Name { get; }

        /// <summary>Comma-joined detail numbers across all sheet placements, or "—" if unplaced.</summary>
        public string DetailNumbers { get; }

        /// <summary>Comma-joined sheet numbers, or "Unplaced" if not on any sheet.</summary>
        public string SheetNumbers { get; }

        /// <summary>True if this drafting view is placed on at least one sheet.</summary>
        public bool IsPlaced { get; }

        public DraftingViewItemViewModel(ViewDrafting view, IReadOnlyList<(string SheetNumber, string DetailNumber)> placements)
        {
            View = view;
            Name = view.Name;

            IsPlaced = placements != null && placements.Any();

            SheetNumbers = IsPlaced
                ? string.Join(", ", placements.Select(p => p.SheetNumber))
                : "Unplaced";

            DetailNumbers = IsPlaced
                ? string.Join(", ", placements.Select(p => p.DetailNumber).Where(d => !string.IsNullOrWhiteSpace(d)))
                : "—";

            if (IsPlaced && string.IsNullOrWhiteSpace(DetailNumbers))
                DetailNumbers = "—";
        }
    }
}
