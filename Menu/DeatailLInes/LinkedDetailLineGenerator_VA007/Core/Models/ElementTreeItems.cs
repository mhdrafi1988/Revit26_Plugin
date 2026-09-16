using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA007.Core.Models
{
    /// <summary>Per-Type geometric metrics backing the Categories panel's Width/
    /// Height/Perimeter/Area filters. Computed once per Type from a single
    /// representative instance (ElementMetricsService) — cheap, and Types are
    /// near-always geometrically uniform within themselves (that's what makes
    /// them a "Type").</summary>
    public class ElementMetrics
    {
        public double WidthFeet { get; set; }
        public double HeightFeet { get; set; }

        /// <summary>Profile-group (Floor/Roof) only — null for Linear/Point Types,
        /// whose instances have no closed footprint to measure.</summary>
        public double? PerimeterFeet { get; set; }
        public double? AreaSqFt { get; set; }
    }

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

        // VA007: "any visible" versions of the three group splits above, so the
        // three Section 2 expanders hide a link node entirely once every one of
        // its categories/types has been filtered out by the Categories panel's
        // Width/Height/Perimeter/Area filters or search text (RecomputeTreeVisibility
        // in MainViewModel keeps CategoryTreeItem.IsVisible in sync with its children).
        public bool HasVisibleProfileCategories => ProfileCategories.Any(c => c.IsVisible);
        public bool HasVisibleLinearCategories => LinearCategories.Any(c => c.IsVisible);
        public bool HasVisiblePointCategories => PointCategories.Any(c => c.IsVisible);

        public bool HasProfileCategories => ProfileCategories.Any();
        public bool HasLinearCategories => LinearCategories.Any();
        public bool HasPointCategories => PointCategories.Any();

        /// <summary>Called by MainViewModel.RecomputeTreeVisibility after updating
        /// every descendant CategoryTreeItem.IsVisible — OnPropertyChanged is
        /// protected, so the recompute lives here instead of in the view model.</summary>
        public void RefreshVisibilityFlags()
        {
            OnPropertyChanged(nameof(HasVisibleProfileCategories));
            OnPropertyChanged(nameof(HasVisibleLinearCategories));
            OnPropertyChanged(nameof(HasVisiblePointCategories));
        }
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

        /// <summary>VA007: true when at least one descendant Type passes the
        /// Categories panel's current filters — recomputed by MainViewModel's
        /// RecomputeTreeVisibility, never set directly from XAML.</summary>
        [ObservableProperty]
        private bool _isVisible = true;

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

        /// <summary>VA007: true when at least one child Type passes the current filters.</summary>
        [ObservableProperty]
        private bool _isVisible = true;

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

        // VA007: parent names echoed here (set once in LinkService.BuildCategoryNode)
        // so the "Group by Type" flat view can label each entry without walking back
        // up the tree, and so the Categories panel can filter/search without it either.
        public string CategoryName { get; set; } = string.Empty;
        public string FamilyName { get; set; } = string.Empty;
        public RepresentationGroup Group { get; set; }

        /// <summary>VA007: geometric metrics from a representative instance of this
        /// Type, backing the Categories panel's Width/Height/Perimeter/Area filters.
        /// Populated once in LinkService.BuildCategoryNode via ElementMetricsService.</summary>
        public ElementMetrics? Metrics { get; set; }

        [ObservableProperty]
        private bool _isChecked;

        /// <summary>VA007: true when this Type passes the Categories panel's current
        /// Width/Height/Perimeter/Area filters and search text. Set by MainViewModel's
        /// RecomputeTreeVisibility — never bound as a two-way setter from XAML.</summary>
        [ObservableProperty]
        private bool _isVisible = true;
    }

    /// <summary>
    /// VA007 "Group by Type" flat entry — one per distinct Type NAME within a
    /// RepresentationGroup (Profile/Linear/Point), expandable to its Members: the
    /// same TypeTreeItem instances the Category>Family>Type tree already uses, so
    /// checking a member here or there is the exact same action (no duplicated
    /// checked-state). Members.Count is almost always 1 — it's 2+ only when the
    /// same Type name happens to be reused across different Categories/Families/
    /// links (e.g. two loadable families that both ship a Type called "Type 1").
    /// </summary>
    public partial class TypeGroupItem : ObservableObject
    {
        public string TypeName { get; set; } = string.Empty;
        public RepresentationGroup Group { get; set; }

        [ObservableProperty]
        private bool _isExpanded = true;

        [ObservableProperty]
        private bool _isVisible = true;

        public ObservableCollection<TypeTreeItem> Members { get; set; } = new();
    }
}
