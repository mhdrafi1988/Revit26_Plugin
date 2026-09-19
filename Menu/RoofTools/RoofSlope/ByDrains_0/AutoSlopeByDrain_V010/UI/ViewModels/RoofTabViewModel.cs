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
using Revit26_Plugin.MultiRoofSlopeByDrain.V010.Core.Models;
using Revit26_Plugin.MultiRoofSlopeByDrain.V010.Core.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;

namespace Revit26_Plugin.MultiRoofSlopeByDrain.V010.UI.ViewModels
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

        /// <summary>
        /// NEW (V010), per Rafi's confirmed group-expand decision. Holds ONLY
        /// groups the user has explicitly expanded/collapsed by hand in the
        /// DataGrid, keyed by group name (e.g. "Circle"). A group absent from
        /// this dictionary falls back to the default rule — the FIRST group in
        /// the current (filtered) view starts expanded, every other group starts
        /// collapsed; since ShapeGroupOrder sorts Circle first when present, this
        /// naturally means "Circle open, Rectangle/Other closed" and, on a roof
        /// with no circles, whichever group sorts first instead. Cleared on a
        /// size-filter change (see OnSelectedSizeFilterChanged below); switching
        /// roof tabs already gets a fresh instance of this dictionary for free,
        /// since each RoofTabViewModel owns its own. NOT cleared by Select All/
        /// None/Invert or the per-group All/None buttons — those must not
        /// disturb whatever the user has manually expanded or collapsed.
        /// </summary>
        public Dictionary<string, bool> GroupExpandedOverride { get; } = new Dictionary<string, bool>();

        [ObservableProperty]
        private string selectedSizeFilter = "All";

        partial void OnSelectedSizeFilterChanged(string value)
        {
            GroupExpandedOverride.Clear();
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

        /// <summary>NEW (V010). "14:32:05 → 14:32:41 · 36 s", or "" before this roof has a result. Shown on the per-roof banner alongside LastRunStatus.</summary>
        [ObservableProperty]
        private string timingDisplay = "";

        /// <summary>
        /// NEW (V010), per Rafi's confirmed default-selection decision. Set once in
        /// the constructor: null if this roof has no circle drains at all, otherwise
        /// a one-line summary ("Default selection: 4 circle(s) at ⌀100 mm (of 9
        /// circle(s) detected)") that the parent ViewModel logs right after adding
        /// this tab.
        /// </summary>
        public string DefaultSelectionSummary { get; }

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

            // Must run AFTER FilteredDrainsView exists: setting IsSelected raises
            // OnDrainPropertyChanged -> UpdateSelectedCount, which enumerates the view.
            DefaultSelectionSummary = ApplyDefaultCircleSelection();

            TotalDetectedCount = roofData.DetectedDrains.Count;

            UpdateSelectedCount();
        }

        /// <summary>
        /// NEW (V010). Refines DrainItem's constructor default (ALL circles start
        /// checked) down to just the smallest ones: a circle stays checked only if
        /// its diameter is within 2mm of this roof's smallest circle diameter —
        /// per Rafi's confirmed decision, computed PER ROOF, not across the whole
        /// batch. Non-circle shapes are untouched (they already start unchecked).
        /// Returns null if this roof has no circle drains at all.
        /// </summary>
        private string ApplyDefaultCircleSelection()
        {
            const double toleranceMm = 2.0;

            var circles = AllDrains.Where(d => d.ShapeGroup == "Circle" && d.Diameter.HasValue).ToList();
            if (circles.Count == 0) return null;

            double smallestDiameterMm = circles.Min(d => d.Diameter.Value);
            int keptCount = 0;

            foreach (var circle in circles)
            {
                bool keep = circle.Diameter.Value <= smallestDiameterMm + toleranceMm;
                circle.IsSelected = keep;
                if (keep) keptCount++;
            }

            return $"Default selection: {keptCount} circle(s) at ⌀{smallestDiameterMm:F0} mm " +
                   $"(of {circles.Count} circle(s) detected)";
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
            LastRunStatus = result.Success
                ? "Completed"
                : result.WasCancelled ? "Cancelled" : $"Failed: {result.ErrorMessage}";

            // NEW (V010): StartTime/EndTime/RunDuration_sec are now populated on
            // every result — success, failure, or cancellation (see
            // AutoSlopeDrainEngine.Fail) — so the banner always has real timing.
            TimingDisplay = !string.IsNullOrEmpty(result.StartTime) && !string.IsNullOrEmpty(result.EndTime)
                ? $"{result.StartTime} → {result.EndTime} · {FormatDuration(result.RunDuration_sec)}"
                : "";

            if (result.Success)
            {
                LongestPath_m = result.LongestPath_m;
                HighestElevation_mm = result.HighestElevation_mm;
                ExportedFilePath = result.ExportedFilePath;
            }
        }

        /// <summary>NEW (V010). "36 s", "1 m 12 s" once over a minute, "&lt;1 s" for a very fast roof.</summary>
        private static string FormatDuration(int totalSec)
        {
            if (totalSec < 1) return "<1 s";
            if (totalSec < 60) return $"{totalSec} s";
            int minutes = totalSec / 60, seconds = totalSec % 60;
            return seconds == 0 ? $"{minutes} m" : $"{minutes} m {seconds} s";
        }
    }
}
