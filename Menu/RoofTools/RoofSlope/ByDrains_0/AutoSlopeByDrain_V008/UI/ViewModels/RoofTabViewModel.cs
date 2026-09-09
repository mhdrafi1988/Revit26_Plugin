// File: RoofTabViewModel.cs
// Location: UI/ViewModels/
//
// NEW (V008), per Rafi's confirmed multi-roof decision (2026-09-08). Holds
// everything that used to live directly on AutoSlopeDrainViewModel but is
// now per-roof: the detected-drains grid, its size filter, its selection
// commands, and (after Run) that roof's own result metrics + exported file
// path. One instance per roof the user picked in the Command, displayed as
// one TabItem in the window. Slope %, thresholds, marker styles and the
// export folder stay on the parent ViewModel — shared across every roof,
// per Rafi's confirmed decision.

using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.MultiRoofSlopeByDrain.Core.Models;
using Revit26_Plugin.MultiRoofSlopeByDrain.Core.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;

namespace Revit26_Plugin.MultiRoofSlopeByDrain.UI.ViewModels
{
    public partial class RoofTabViewModel : ObservableObject
    {
        public RoofData RoofData { get; }
        public ElementId RoofId => RoofData.Roof.Id;
        public string RoofName => RoofData.Roof.Name;

        /// <summary>Short label for the TabItem header, e.g. "Roof 1 (Id 123456)".</summary>
        public string TabHeader { get; }

        public string RoofSubtitle { get; }

        public ObservableCollection<DrainItem> AllDrains { get; } = new ObservableCollection<DrainItem>();
        public ObservableCollection<string> SizeFilters { get; } = new ObservableCollection<string>();
        public ICollectionView FilteredDrainsView { get; }

        [ObservableProperty]
        private string selectedSizeFilter = "All";

        partial void OnSelectedSizeFilterChanged(string value)
        {
            FilteredDrainsView.Refresh();
            UpdateSelectedCount();
        }

        [ObservableProperty]
        private int selectedDrainsCount;

        [ObservableProperty]
        private int totalDetectedCount;

        public bool HasNoDrainsDetected => TotalDetectedCount == 0;

        // ── Result metrics — populated after a successful Run for this roof ────
        [ObservableProperty]
        private bool hasRunResult;

        [ObservableProperty]
        private bool lastRunSucceeded;

        [ObservableProperty]
        private string lastRunStatus = "Not run yet";

        [ObservableProperty]
        private double longestPath_m;

        [ObservableProperty]
        private double highestElevation_mm;

        [ObservableProperty]
        private string exportedFilePath;

        public RoofTabViewModel(RoofData roofData, int tabIndex)
        {
            RoofData = roofData;
            TabHeader = $"Roof {tabIndex} — {roofData.Roof.Name}";
            RoofSubtitle = $"Roof: {roofData.Roof.Name} (Id {roofData.Roof.Id.Value})";

            var detectionService = new DrainDetectionService();

            foreach (var drain in roofData.DetectedDrains)
            {
                AllDrains.Add(drain);
                drain.PropertyChanged += OnDrainPropertyChanged;
            }

            foreach (var category in detectionService.GenerateSizeCategories(roofData.DetectedDrains))
                SizeFilters.Add(category);

            FilteredDrainsView = CollectionViewSource.GetDefaultView(AllDrains);
            FilteredDrainsView.Filter = FilterDrainItem;

            FilteredDrainsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(DrainItem.ShapeGroup)));
            FilteredDrainsView.SortDescriptions.Add(new SortDescription(nameof(DrainItem.ShapeGroupOrder), ListSortDirection.Ascending));
            FilteredDrainsView.SortDescriptions.Add(new SortDescription(nameof(DrainItem.SizeSortKey), ListSortDirection.Ascending));

            TotalDetectedCount = roofData.DetectedDrains.Count;

            UpdateSelectedCount();
        }

        private void OnDrainPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(DrainItem.IsSelected)) return;
            UpdateSelectedCount();
            SelectionChanged?.Invoke();
        }

        /// <summary>Raised whenever a drain's checked state changes, so the parent ViewModel can re-evaluate Run's CanExecute.</summary>
        public event System.Action SelectionChanged;

        private bool FilterDrainItem(object obj)
        {
            if (!(obj is DrainItem drain)) return false;
            if (SelectedSizeFilter == "All") return true;

            var svc = new DrainDetectionService();
            return svc.FilterDrainsBySize(new List<DrainItem> { drain }, SelectedSizeFilter).Any();
        }

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var d in FilteredDrainsView.Cast<DrainItem>()) d.IsSelected = true;
            FilteredDrainsView.Refresh();
            UpdateSelectedCount();
        }

        [RelayCommand]
        private void SelectNone()
        {
            foreach (var d in FilteredDrainsView.Cast<DrainItem>()) d.IsSelected = false;
            FilteredDrainsView.Refresh();
            UpdateSelectedCount();
        }

        [RelayCommand]
        private void InvertSelection()
        {
            foreach (var d in FilteredDrainsView.Cast<DrainItem>()) d.IsSelected = !d.IsSelected;
            FilteredDrainsView.Refresh();
            UpdateSelectedCount();
        }

        [RelayCommand]
        private void SelectAllInGroup(object groupName)
        {
            if (!(groupName is string name)) return;
            foreach (var d in FilteredDrainsView.Cast<DrainItem>().Where(d => d.ShapeGroup == name))
                d.IsSelected = true;
            FilteredDrainsView.Refresh();
            UpdateSelectedCount();
        }

        [RelayCommand]
        private void SelectNoneInGroup(object groupName)
        {
            if (!(groupName is string name)) return;
            foreach (var d in FilteredDrainsView.Cast<DrainItem>().Where(d => d.ShapeGroup == name))
                d.IsSelected = false;
            FilteredDrainsView.Refresh();
            UpdateSelectedCount();
        }

        private void UpdateSelectedCount()
        {
            SelectedDrainsCount = FilteredDrainsView.Cast<DrainItem>().Count(d => d.IsSelected);
        }

        public List<DrainItem> GetSelectedDrains() => AllDrains.Where(d => d.IsSelected).ToList();

        public void ApplyRunResult(AutoSlopeDrainResult result)
        {
            HasRunResult = true;
            LastRunSucceeded = result.Success;
            LastRunStatus = result.Success ? "Completed" : $"Failed: {result.ErrorMessage}";

            if (result.Success)
            {
                LongestPath_m = result.LongestPath_m;
                HighestElevation_mm = result.HighestElevation_mm;
                ExportedFilePath = result.ExportedFilePath;
            }
        }
    }
}
