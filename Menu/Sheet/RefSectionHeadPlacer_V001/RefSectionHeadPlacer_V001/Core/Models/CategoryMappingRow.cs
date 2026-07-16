using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.RefSectionHeadPlacer.V001.Core.Models
{
    /// <summary>Display wrapper for a drafting view in the mapping ComboBox (Grid 3).</summary>
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
    }

    /// <summary>
    /// Bindable row for Grid 3 — identity is (SourceLabel, Bic, TypeName), matching
    /// ElementTypeRow exactly. Bic is the stable routing key; Category is display
    /// only. Rows are generated from the user's Grid 2 selections (one per selected
    /// row, per link); the user then assigns each a target drafting view.
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
        private DraftingViewOption mappedDraftingView;

        public CategoryMappingRow(
            string sourceLabel, string category, string typeName,
            BuiltInCategory bic, DraftingViewOption mappedDraftingView = null)
        {
            SourceLabel = sourceLabel;
            Category = category;
            TypeName = typeName;
            Bic = bic;
            this.mappedDraftingView = mappedDraftingView;
        }
    }
}
