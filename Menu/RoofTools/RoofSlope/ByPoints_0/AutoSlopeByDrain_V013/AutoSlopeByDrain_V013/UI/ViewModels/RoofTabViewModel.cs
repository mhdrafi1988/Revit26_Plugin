// =======================================================
// File: RoofTabViewModel.cs
// Namespace: Revit26_Plugin.MultiRoofSlopeByDrain.V013.UI.ViewModels
// One tab per picked roof: the detected-drains grid, its size filter and
// selection commands, and (after Run) that roof's status and results.
// Changes vs V012:
//   - SelectedDrainsCount counts every checked drain. V012 counted only the
//     drains visible under the size filter, while Run used every checked
//     drain (GetSelectedDrains), so the count and the run could disagree.
//     HiddenSelectedCount says how many checked drains the filter hides.
//   - Status (Ready / No drains / Running / Done / Rolled back / Not started
//     / Skipped / Failed) for the pill on the tab header.
//   - Results for the side panel: Longest 10 and Skipped rows, same columns
//     as the Excel "Vertex Data" sheet.
//   - Timing uses decimal seconds.
//   - One DrainDetectionService for the filter (V012 created one per row).
// =======================================================

using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.MultiRoofSlopeByDrain.V013.Core.Models;
using Revit26_Plugin.MultiRoofSlopeByDrain.V013.Core.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;

namespace Revit26_Plugin.MultiRoofSlopeByDrain.V013.UI.ViewModels
{
    public enum RoofTabStatus
    {
        Ready,
        NoDrains,
        Running,
        Done,
        RolledBack,
        NotStarted,
        Skipped,
        Failed
    }

    public partial class RoofTabViewModel : ObservableObject
    {
        private const int ResultRowCount = 10;
        private readonly DrainDetectionService _detection = new DrainDetectionService();

        public RoofData RoofData { get; }
        public ElementId RoofId => RoofData.Roof.Id;
        public long RoofIdValue => RoofData.Roof.Id.Value;
        public string RoofName => RoofData.Roof.Name;
        public int TabNumber { get; }

        /// <summary>Tab header text, e.g. "Roof 1 — Generic 300mm".</summary>
        public string TabHeader { get; }

        public string RoofSubtitle { get; }

        public ObservableCollection<DrainItem> AllDrains { get; } = new ObservableCollection<DrainItem>();
        public ObservableCollection<string> SizeFilters { get; } = new ObservableCollection<string>();
        public ICollectionView FilteredDrainsView { get; }

        /// <summary>
        /// Groups the user expanded or collapsed by hand, by group name. A group
        /// not in here starts expanded only when it is the first group in the
        /// current view. Cleared when the size filter changes.
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
        [NotifyPropertyChangedFor(nameof(SelectionNote))]
        private int selectedDrainsCount;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SelectionNote))]
        private int hiddenSelectedCount;

        public int TotalDetectedCount { get; }

        public bool HasNoDrainsSelected => SelectedDrainsCount == 0;

        public string SelectionNote => SelectedDrainsCount == 0
            ? "No drains checked on this roof, so it will be skipped. Tick at least one drain to include it."
            : $"{SelectedDrainsCount} of {TotalDetectedCount} drains checked · Roof Id {RoofIdValue}"
              + (HiddenSelectedCount > 0 ? $" · {HiddenSelectedCount} checked drain(s) hidden by the size filter will still run" : string.Empty);

        /// <summary>One-line default-selection summary, or null when the roof has no circle drains.</summary>
        public string DefaultSelectionSummary { get; }

        // ── Status (tab pill) ──────────────────────────────────────────────
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusText))]
        [NotifyPropertyChangedFor(nameof(DisplayStatus))]
        [NotifyPropertyChangedFor(nameof(ResultsEmptyText))]
        private RoofTabStatus status = RoofTabStatus.Ready;

        /// <summary>Status, with "Ready" shown as "No drains" when nothing is checked.</summary>
        public RoofTabStatus DisplayStatus =>
            Status == RoofTabStatus.Ready && SelectedDrainsCount == 0 ? RoofTabStatus.NoDrains : Status;

        public string StatusText => DisplayStatus switch
        {
            RoofTabStatus.NoDrains => "No drains",
            RoofTabStatus.Running => "Running",
            RoofTabStatus.Done => "Done",
            RoofTabStatus.RolledBack => "Rolled back",
            RoofTabStatus.NotStarted => "Not started",
            RoofTabStatus.Skipped => "Skipped",
            RoofTabStatus.Failed => "Failed",
            _ => "Ready"
        };

        // ── Last result (banner + side panel) ──────────────────────────────
        [ObservableProperty] private bool hasRunResult;
        [ObservableProperty] private string bannerText = string.Empty;
        [ObservableProperty] private string timingDisplay = string.Empty;

        public AutoSlopeDrainResult LastResult { get; private set; }

        public ObservableCollection<DrainResultRow> LongestRows { get; } = new ObservableCollection<DrainResultRow>();
        public ObservableCollection<DrainResultRow> SkippedRows { get; } = new ObservableCollection<DrainResultRow>();

        [ObservableProperty] private int skippedTotal;

        public bool HasResults => LastResult?.Success == true;

        public string ResultsEmptyText => DisplayStatus switch
        {
            RoofTabStatus.Running => "Running… the table fills when this roof finishes.",
            RoofTabStatus.NotStarted => "This roof has not run yet in this batch.",
            RoofTabStatus.RolledBack => "This roof was rolled back, so there is nothing to show.",
            RoofTabStatus.Failed => "This roof's run failed, so there is nothing to show.",
            RoofTabStatus.Skipped => "This roof was skipped (no drains checked).",
            RoofTabStatus.NoDrains => "No drains are checked on this roof, so it will not run.",
            RoofTabStatus.Done => "No rows.",
            _ => "Run AutoSlope to see this roof's results."
        };

        public RoofTabViewModel(RoofData roofData, int tabNumber)
        {
            RoofData = roofData;
            TabNumber = tabNumber;
            string shortName = (roofData.Roof.Name ?? "Roof").Replace("Basic Roof – ", "").Replace("Basic Roof - ", "");
            TabHeader = $"Roof {tabNumber} — {shortName}";
            RoofSubtitle = $"Roof: {roofData.Roof.Name} (Id {roofData.Roof.Id.Value})";

            foreach (var drain in roofData.DetectedDrains)
            {
                AllDrains.Add(drain);
                drain.PropertyChanged += OnDrainPropertyChanged;
            }

            foreach (var category in _detection.GenerateSizeCategories(roofData.DetectedDrains))
                SizeFilters.Add(category);

            FilteredDrainsView = CollectionViewSource.GetDefaultView(AllDrains);
            FilteredDrainsView.Filter = FilterDrainItem;
            FilteredDrainsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(DrainItem.ShapeGroup)));
            FilteredDrainsView.SortDescriptions.Add(new SortDescription(nameof(DrainItem.ShapeGroupOrder), ListSortDirection.Ascending));
            FilteredDrainsView.SortDescriptions.Add(new SortDescription(nameof(DrainItem.SizeSortKey), ListSortDirection.Ascending));

            // After FilteredDrainsView exists: setting IsSelected raises UpdateSelectedCount.
            DefaultSelectionSummary = ApplyDefaultCircleSelection();
            TotalDetectedCount = roofData.DetectedDrains.Count;
            UpdateSelectedCount();
        }

        /// <summary>Selects circles with a diameter of 90–110 mm; other circles start unchecked.</summary>
        private string ApplyDefaultCircleSelection()
        {
            const double minMm = 90.0;
            const double maxMm = 110.0;

            var circles = AllDrains.Where(d => d.ShapeGroup == "Circle" && d.Diameter.HasValue).ToList();
            if (circles.Count == 0) return null;

            int kept = 0;
            foreach (var circle in circles)
            {
                bool keep = circle.Diameter.Value >= minMm && circle.Diameter.Value <= maxMm;
                circle.IsSelected = keep;
                if (keep) kept++;
            }
            return $"Default selection: {kept} circle(s) with ⌀ 90–110 mm (of {circles.Count} circle(s) detected).";
        }

        private void OnDrainPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(DrainItem.IsSelected)) return;
            UpdateSelectedCount();
        }

        /// <summary>Raised whenever the checked set changes, so the parent can re-evaluate Run.</summary>
        public event System.Action SelectionChanged;

        private bool PassesFilter(DrainItem drain) =>
            SelectedSizeFilter == "All" || _detection.FilterDrainsBySize(new List<DrainItem> { drain }, SelectedSizeFilter).Any();

        private bool FilterDrainItem(object obj) => obj is DrainItem drain && PassesFilter(drain);

        private void ForVisible(System.Action<DrainItem> action)
        {
            foreach (var d in FilteredDrainsView.Cast<DrainItem>().ToList()) action(d);
            FilteredDrainsView.Refresh();
            UpdateSelectedCount();
        }

        [RelayCommand] private void SelectAll() => ForVisible(d => d.IsSelected = true);
        [RelayCommand] private void SelectNone() => ForVisible(d => d.IsSelected = false);
        [RelayCommand] private void InvertSelection() => ForVisible(d => d.IsSelected = !d.IsSelected);

        [RelayCommand]
        private void SelectAllInGroup(object groupName)
        {
            if (groupName is string name) ForVisible(d => { if (d.ShapeGroup == name) d.IsSelected = true; });
        }

        [RelayCommand]
        private void SelectNoneInGroup(object groupName)
        {
            if (groupName is string name) ForVisible(d => { if (d.ShapeGroup == name) d.IsSelected = false; });
        }

        private void UpdateSelectedCount()
        {
            SelectedDrainsCount = AllDrains.Count(d => d.IsSelected);
            HiddenSelectedCount = AllDrains.Count(d => d.IsSelected && !PassesFilter(d));
            OnPropertyChanged(nameof(HasNoDrainsSelected));
            OnPropertyChanged(nameof(DisplayStatus));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(ResultsEmptyText));
            SelectionChanged?.Invoke();
        }

        /// <summary>Every checked drain, including any the size filter hides (the count above says how many).</summary>
        public List<DrainItem> GetSelectedDrains() => AllDrains.Where(d => d.IsSelected).ToList();

        // ── Run lifecycle ──────────────────────────────────────────────────

        /// <summary>Called when a batch starts: runnable tabs wait, the rest are skipped.</summary>
        public void PrepareForRun(bool willRun)
        {
            ClearResults();
            Status = willRun ? RoofTabStatus.NotStarted : RoofTabStatus.Skipped;
            BannerText = willRun ? string.Empty : "Skipped · no drains were checked on this roof";
            HasRunResult = !willRun;
        }

        public void MarkRunning()
        {
            Status = RoofTabStatus.Running;
            BannerText = string.Empty;
            HasRunResult = false;
        }

        /// <summary>Called when a batch ends: roofs that never started say so.</summary>
        public void FinishBatch()
        {
            if (Status == RoofTabStatus.NotStarted)
            {
                BannerText = "Not started · the run was cancelled before this roof";
                HasRunResult = true;
            }
            else if (Status == RoofTabStatus.Running)
            {
                Status = RoofTabStatus.RolledBack;
                BannerText = "Rolled back · this roof is exactly as it was before Run";
                HasRunResult = true;
            }
        }

        public void ApplyRunResult(AutoSlopeDrainResult result)
        {
            LastResult = result;
            HasRunResult = true;
            TimingDisplay = result.StartedAt == default
                ? string.Empty
                : $"{result.StartedAt:HH:mm:ss} → {result.EndedAt:HH:mm:ss} · {result.DurationSeconds:0.0} s";

            if (result.Success)
            {
                Status = RoofTabStatus.Done;
                BannerText = $"Done · {result.VerticesProcessed:N0} processed, {result.VerticesSkipped:N0} skipped · " +
                             $"longest {result.LongestPath_m:0.00} m · highest {result.HighestElevation_mm:0} mm";
                FillResults(result);
            }
            else
            {
                string timing = TimingDisplay;
                ClearResults();
                TimingDisplay = timing;
                Status = result.WasCancelled ? RoofTabStatus.RolledBack : RoofTabStatus.Failed;
                BannerText = result.WasCancelled
                    ? "Rolled back · cancelled while this roof was running; it is exactly as it was before Run"
                    : $"Failed · {result.ErrorMessage} · rolled back";
            }
        }

        private void FillResults(AutoSlopeDrainResult r)
        {
            LongestRows.Clear();
            SkippedRows.Clear();

            int n = 1;
            foreach (var v in r.Vertices.Where(v => v.WasProcessed).OrderByDescending(v => v.PathLengthMeters).Take(ResultRowCount))
                LongestRows.Add(DrainResultRow.FromProcessed(v, n++, r.SlopePercent));

            var skipped = r.Vertices.Where(v => !v.WasProcessed && !v.IsDrain)
                .OrderBy(v => v.SkipReason == "Beyond distance" ? 1 : 0)
                .ThenBy(v => v.PathLengthMeters)
                .ToList();
            SkippedTotal = skipped.Count;
            n = 1;
            foreach (var v in skipped.Take(ResultRowCount))
                SkippedRows.Add(DrainResultRow.FromSkipped(v, n++));

            OnPropertyChanged(nameof(HasResults));
        }

        private void ClearResults()
        {
            LastResult = null;
            LongestRows.Clear();
            SkippedRows.Clear();
            SkippedTotal = 0;
            TimingDisplay = string.Empty;
            OnPropertyChanged(nameof(HasResults));
        }
    }
}
