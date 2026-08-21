using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA003.Core.Models
{
    /// <summary>
    /// Root node grouping Category items under a single linked model instance.
    /// Only appears when 2+ links are checked in Section 1 (merged-tree display);
    /// with a single link checked, the UI may choose to hide this header row.
    ///
    /// Section 2's UI (Phase 5 redesign) shows three always-visible expanders, one
    /// per RepresentationGroup, instead of a single tree behind a tab switcher.
    /// Categories/*Categories below are display-only filtered views over the same
    /// underlying Categories collection/objects (not a copy) so the existing
    /// checkbox-cascade wiring in MainViewModel keeps working unchanged.
    /// </summary>
    public partial class LinkTreeNode : ObservableObject
    {
        public long LinkInstanceId { get; set; }
        public string LinkDisplayName { get; set; } = string.Empty;

        [ObservableProperty]
        private bool _isExpanded = true;

        public ObservableCollection<CategoryTreeItem> Categories { get; set; } = new();

        public IEnumerable<CategoryTreeItem> ProfileCategories => Categories.Where(c => c.Group == RepresentationGroup.Profile);
        public IEnumerable<CategoryTreeItem> LinearCategories => Categories.Where(c => c.Group == RepresentationGroup.Linear);
        public IEnumerable<CategoryTreeItem> PointCategories => Categories.Where(c => c.Group == RepresentationGroup.Point);

        public bool HasProfileCategories => ProfileCategories.Any();
        public bool HasLinearCategories => LinearCategories.Any();
        public bool HasPointCategories => PointCategories.Any();
    }

    /// <summary>
    /// Category-level node (e.g. "Floors", "Walls", "Structural Columns").
    /// IsChecked is tri-state driven by child Family/Type checked states
    /// (implemented in MainViewModel's checkbox-cascade logic, Phase 1 UI-only).
    /// </summary>
    public partial class CategoryTreeItem : ObservableObject
    {
        public string CategoryName { get; set; } = string.Empty;

        /// <summary>Which RepresentationGroup tab (Profile/Linear/Point) this category belongs under.</summary>
        public RepresentationGroup Group { get; set; }

        [ObservableProperty]
        private bool _isExpanded = true;

        [ObservableProperty]
        private bool? _isChecked = false;

        public ObservableCollection<FamilyTreeItem> Families { get; set; } = new();
    }

    /// <summary>Family-level node (e.g. "Generic Floor", "Basic Wall").</summary>
    public partial class FamilyTreeItem : ObservableObject
    {
        public string FamilyName { get; set; } = string.Empty;

        [ObservableProperty]
        private bool _isExpanded = true;

        [ObservableProperty]
        private bool? _isChecked = false;

        public ObservableCollection<TypeTreeItem> Types { get; set; } = new();
    }

    /// <summary>
    /// Leaf Type node (e.g. "150mm Floor"). Checking this is what actually
    /// creates/removes a row in the Mapping Grid (Section 3).
    /// </summary>
    public partial class TypeTreeItem : ObservableObject
    {
        public long TypeId { get; set; }
        public string TypeName { get; set; } = string.Empty;

        [ObservableProperty]
        private bool _isChecked;
    }
}
