using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.LinkedDetailLineGenerator.VA010.Core.Models;
using Revit26_Plugin.LinkedDetailLineGenerator.VA010.Core.Services;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA010.UI.ViewModels
{
    public partial class MainViewModel
    {
        // ── Section 1: Linked Models & Instances ─────────────────────────────
        [ObservableProperty]
        private bool _isLinkedModelsExpanded = true;

        public ObservableCollection<LinkedModelItem> LinkedModels { get; } = new();
        public ICollectionView LinkedModelsView { get; }

        public int SelectedLinkCount => LinkedModels.Count(l => l.IsSelected);

        [ObservableProperty] private string _linkSearchText = string.Empty;
        [ObservableProperty] private LinkStatusFilter _linkStatusFilter = LinkStatusFilter.All;
        [ObservableProperty] private LinkSortMode _linkSortMode = LinkSortMode.Name;

        partial void OnLinkSearchTextChanged(string value) => LinkedModelsView.Refresh();
        partial void OnLinkStatusFilterChanged(LinkStatusFilter value) => LinkedModelsView.Refresh();

        partial void OnLinkSortModeChanged(LinkSortMode value)
        {
            LinkedModelsView.SortDescriptions.Clear();
            LinkedModelsView.SortDescriptions.Add(value == LinkSortMode.Status
                ? new SortDescription(nameof(LinkedModelItem.Status), ListSortDirection.Ascending)
                : new SortDescription(nameof(LinkedModelItem.DocumentTitle), ListSortDirection.Ascending));
            LinkedModelsView.Refresh();
        }

        private bool FilterLinkedModel(object obj)
        {
            if (obj is not LinkedModelItem link) return false;
            bool statusOk = LinkStatusFilter switch
            {
                LinkStatusFilter.Loaded => link.Status == LinkedFileStatus.Loaded,
                LinkStatusFilter.NeedsReview => link.Status == LinkedFileStatus.CanBeUpgraded,
                _ => true
            };
            if (!statusOk) return false;
            if (string.IsNullOrWhiteSpace(LinkSearchText)) return true;
            return link.DocumentTitle.Contains(LinkSearchText, System.StringComparison.OrdinalIgnoreCase)
                || link.InstanceName.Contains(LinkSearchText, System.StringComparison.OrdinalIgnoreCase);
        }

        // ── Section 2: Element Selection ─────────────────────────────────────
        [ObservableProperty] private bool _isProfileSelectionExpanded = true;
        [ObservableProperty] private bool _isLinearSelectionExpanded = true;
        [ObservableProperty] private bool _isPointSelectionExpanded = true;

        public ObservableCollection<LinkTreeNode> ElementTree { get; } = new();

        [ObservableProperty]
        private string _treeSearchText = string.Empty;

        // ── VA007 Categories panel: Width/Height/Perimeter/Area filters ───────
        public static readonly double[] RangeFilterSteps =
            Enumerable.Range(2, 19).Select(i => i * 50.0).ToArray();
        public double[] RangeFilterStepsList => RangeFilterSteps;

        [ObservableProperty] private double? _widthMinFt;
        [ObservableProperty] private double? _widthMaxFt;
        [ObservableProperty] private double? _heightMinFt;
        [ObservableProperty] private double? _heightMaxFt;
        [ObservableProperty] private double? _perimeterMinFt;
        [ObservableProperty] private double? _perimeterMaxFt;
        [ObservableProperty] private double? _areaMinSqFt;
        [ObservableProperty] private double? _areaMaxSqFt;

        [ObservableProperty]
        private bool _groupElementsByType;

        public ObservableCollection<TypeGroupItem> ProfileTypeGroups { get; } = new();
        public ObservableCollection<TypeGroupItem> LinearTypeGroups { get; } = new();
        public ObservableCollection<TypeGroupItem> PointTypeGroups { get; } = new();

        partial void OnTreeSearchTextChanged(string value) => RecomputeTreeVisibility();
        partial void OnWidthMinFtChanged(double? v) => RecomputeTreeVisibility();
        partial void OnWidthMaxFtChanged(double? v) => RecomputeTreeVisibility();
        partial void OnHeightMinFtChanged(double? v) => RecomputeTreeVisibility();
        partial void OnHeightMaxFtChanged(double? v) => RecomputeTreeVisibility();
        partial void OnPerimeterMinFtChanged(double? v) => RecomputeTreeVisibility();
        partial void OnPerimeterMaxFtChanged(double? v) => RecomputeTreeVisibility();
        partial void OnAreaMinSqFtChanged(double? v) => RecomputeTreeVisibility();
        partial void OnAreaMaxSqFtChanged(double? v) => RecomputeTreeVisibility();

        // ── Live data load ────────────────────────────────────────────────────

        private void LoadLiveData()
        {
            Document hostDoc = _uiApp.ActiveUIDocument.Document;
            Autodesk.Revit.DB.View activeView = _uiApp.ActiveUIDocument.ActiveView;
            CurrentViewName = activeView.Name;

            AvailableLineStyleNames.Clear();
            foreach (var style in _lineStyleService.GetAvailableLineStyles(hostDoc))
                AvailableLineStyleNames.Add(style.Name);

            AvailableColorNames.Clear();
            AvailableColorNames.Add("None");
            foreach (var colorName in _graphicsOverrideService.AvailableColorNames)
                AvailableColorNames.Add(colorName);

            var links = _linkService.GetLinkedModels(hostDoc, msg => AddLog(LogLevel.Info, msg));
            foreach (var link in links)
            {
                link.PropertyChanged += OnLinkSelectionChanged;
                LinkedModels.Add(link);
            }
            foreach (var link in LinkedModels.Where(l => l.IsLoaded))
                link.IsSelected = true;

            RebuildElementTree();
        }

        private void OnLinkSelectionChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LinkedModelItem.IsSelected))
            {
                OnPropertyChanged(nameof(SelectedLinkCount));
                RebuildElementTree();
            }
        }

        internal void RebuildElementTree()
        {
            Document hostDoc = _uiApp.ActiveUIDocument.Document;
            var selectedIds = LinkedModels.Where(l => l.IsSelected).Select(l => l.LinkInstanceId).ToList();

            foreach (var node in ElementTree)
                foreach (var cat in node.Categories)
                    foreach (var fam in cat.Families)
                        foreach (var t in fam.Types)
                            t.PropertyChanged -= OnTypeCheckedChanged;

            ElementTree.Clear();
            ProfileTypeGroups.Clear();
            LinearTypeGroups.Clear();
            PointTypeGroups.Clear();

            List<XYZ>? scopeBoundary = null;
            if (RestrictToBoundary)
            {
                var (boundary, _) = GetActiveProcessingBoundary();
                scopeBoundary = boundary;
            }

            var nodes = _linkService.BuildElementTree(hostDoc, selectedIds, scopeBoundary, msg => AddLog(LogLevel.Info, msg));
            foreach (var node in nodes)
            {
                foreach (var cat in node.Categories)
                    foreach (var fam in cat.Families)
                        foreach (var t in fam.Types)
                        {
                            t.IsChecked = Mappings.Any(m => m.LinkInstanceId == node.LinkInstanceId && m.TypeId == t.TypeId);
                            t.PropertyChanged += OnTypeCheckedChanged;
                        }
                ElementTree.Add(node);
            }

            // Option 3: tree building helpers extracted to TreeVisibilityFilter
            TreeVisibilityFilter.BuildTypeGroups(nodes, ProfileTypeGroups, LinearTypeGroups, PointTypeGroups);
            RecomputeTreeVisibility();

            var toRemove = Mappings.Where(m => !selectedIds.Contains(m.LinkInstanceId)).ToList();
            foreach (var m in toRemove) Mappings.Remove(m);

            AddLog(LogLevel.Info, $"{LinkedModels.Count(l => l.IsSelected)} linked instance(s) selected — element tree loaded ({nodes.Sum(n => n.Categories.Sum(c => c.Families.Sum(f => f.Types.Count)))} type(s) available).");
        }

        private void RecomputeTreeVisibility()
        {
            var filters = new TreeVisibilityFilter.Filters(
                TreeSearchText, WidthMinFt, WidthMaxFt, HeightMinFt, HeightMaxFt,
                PerimeterMinFt, PerimeterMaxFt, AreaMinSqFt, AreaMaxSqFt);

            TreeVisibilityFilter.Apply(ElementTree, ProfileTypeGroups, LinearTypeGroups, PointTypeGroups, filters);
        }

        // ── Type tree interaction ─────────────────────────────────────────────

        private void OnTypeCheckedChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(TypeTreeItem.IsChecked)) return;
            if (sender is not TypeTreeItem typeItem) return;

            foreach (var node in ElementTree)
                foreach (var cat in node.Categories)
                    foreach (var fam in cat.Families)
                        if (fam.Types.Contains(typeItem))
                        {
                            if (typeItem.IsChecked)
                                AddMappingIfMissing(node, cat, fam, typeItem);
                            else
                                RemoveMapping(node.LinkInstanceId, typeItem.TypeId);
                            return;
                        }
        }

        [RelayCommand]
        private void SelectAllInCategory(CategoryTreeItem category)
        {
            foreach (var fam in category.Families)
                foreach (var t in fam.Types)
                    t.IsChecked = true;
        }

        [RelayCommand]
        private void SelectNoneInCategory(CategoryTreeItem category)
        {
            foreach (var fam in category.Families)
                foreach (var t in fam.Types)
                    t.IsChecked = false;
        }

        [RelayCommand]
        private void SelectAllInTypeGroup(TypeGroupItem group)
        {
            foreach (var t in group.Members) t.IsChecked = true;
        }

        [RelayCommand]
        private void SelectNoneInTypeGroup(TypeGroupItem group)
        {
            foreach (var t in group.Members) t.IsChecked = false;
        }
    }
}
