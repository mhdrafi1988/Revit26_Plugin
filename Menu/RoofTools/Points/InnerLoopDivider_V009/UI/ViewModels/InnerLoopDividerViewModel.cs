// =======================================================
// File: InnerLoopDividerViewModel.cs
// Location: UI/ViewModels/
// Renamed from RoofLoopAnalyzerViewModel (V007) for consistency with the
// tool's own name — the class name previously had nothing to do with
// "Inner Loop Divider".
//
// CHANGES vs V007:
//   Constructor now takes (UIApplication, ElementId, initial loops)
//   instead of (Document, RoofBase) — a modeless window cannot hold a
//   live Document/RoofBase and call Revit API methods on it directly
//   from a button click. The roof is re-resolved from RoofId inside
//   InnerLoopDividerEngine, which only ever runs inside a valid API
//   context (Command or ExternalEvent handler).
//   Analyze() and ApplyDivision() now raise the ExternalEvent instead of
//   calling RoofGeometryService/LoopDivisionService directly. The FIRST
//   analysis pass is done by the Command before the window opens (see
//   InnerLoopDividerCommand) and passed in via initialLoops — only
//   "Re-analyze" goes through the event.
// =======================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.InnerLoopDivider.V009.Core.Models;
using Revit26_Plugin.InnerLoopDivider.V009.Infrastructure.ExternalEvents;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;

namespace Revit26_Plugin.InnerLoopDivider.V009.UI.ViewModels
{
    /// <summary>
    /// ViewModel for Inner Loop Divider.
    /// Provides a global "set all" division control plus per-row editable overrides.
    /// Defaults: Circular → 6, Rectangle/Other → 4.
    /// </summary>
    public partial class InnerLoopDividerViewModel : ObservableObject
    {
        private readonly UIApplication _app;
        private readonly ElementId _roofId;

        public ObservableCollection<RoofLoopModel> Loops { get; } = new();
        public ICollectionView LoopsView { get; private set; }
        public ObservableCollection<LogEntry> Log { get; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasLoops))]
        [NotifyPropertyChangedFor(nameof(TotalOpeningsCount))]
        private int innerLoopCount;

        [ObservableProperty]
        private int queuedPointCount;

        [ObservableProperty]
        private int selectedShapeCount;

        public bool HasLoops => InnerLoopCount > 0;
        public int TotalOpeningsCount => InnerLoopCount;

        /// <summary>
        /// Global "set all" control. Pushing a value overwrites every loop's
        /// RecommendedPoints immediately. Per-row values remain editable individually.
        /// </summary>
        [ObservableProperty]
        private int globalDivisionPoints = 6;

        // ── Size filters (circles) ────────────────────────────────────────────
        public const string AllSizes = "All sizes";
        public const string NonCircle = "Non-circle";

        /// <summary>Distinct diameters (mm) found on the roof, ascending, for the filter drop-down.</summary>
        public ObservableCollection<string> DiameterOptions { get; } = new() { AllSizes };

        /// <summary>Distinct radii (mm) found on the roof, ascending, for the filter drop-down.</summary>
        public ObservableCollection<string> RadiusOptions { get; } = new() { AllSizes };

        [ObservableProperty]
        private string selectedDiameterFilter = AllSizes;

        [ObservableProperty]
        private string selectedRadiusFilter = AllSizes;

        partial void OnSelectedDiameterFilterChanged(string value) => OnFilterChanged();
        partial void OnSelectedRadiusFilterChanged(string value) => OnFilterChanged();

        private void OnFilterChanged()
        {
            LoopsView?.Refresh();
            RecomputeCounts();
            ApplyDivisionCommand.NotifyCanExecuteChanged();
        }

        private bool PassesFilter(RoofLoopModel l) =>
            MatchesSize(SelectedDiameterFilter, l.DiameterMm.HasValue, l.DiameterText) &&
            MatchesSize(SelectedRadiusFilter, l.RadiusMm.HasValue, l.RadiusText);

        private static bool MatchesSize(string filter, bool isCircle, string text) =>
            string.IsNullOrEmpty(filter) || filter == AllSizes ||
            (filter == NonCircle ? !isCircle : isCircle && text == filter);

        /// <summary>Loops currently shown in the grid (i.e. passing the size filters).</summary>
        private IEnumerable<RoofLoopModel> VisibleLoops => Loops.Where(PassesFilter);

        private void RebuildSizeOptions()
        {
            Fill(DiameterOptions, Loops.Where(l => l.DiameterMm.HasValue).OrderBy(l => l.DiameterMm).Select(l => l.DiameterText),
                Loops.Any(l => !l.DiameterMm.HasValue), SelectedDiameterFilter, v => SelectedDiameterFilter = v);
            Fill(RadiusOptions, Loops.Where(l => l.RadiusMm.HasValue).OrderBy(l => l.RadiusMm).Select(l => l.RadiusText),
                Loops.Any(l => !l.RadiusMm.HasValue), SelectedRadiusFilter, v => SelectedRadiusFilter = v);

            static void Fill(ObservableCollection<string> target, IEnumerable<string> sizes, bool hasNonCircle,
                             string current, Action<string> setSelected)
            {
                target.Clear();
                target.Add(AllSizes);
                foreach (var s in sizes.Distinct()) target.Add(s);
                if (hasNonCircle) target.Add(NonCircle);
                setSelected(target.Contains(current) ? current : AllSizes);
            }
        }

        // ── Sorting ───────────────────────────────────────────────────────────
        /// <summary>
        /// Sorts within each shape group by the given member. Group order is always kept
        /// first so the grouped view doesn't fragment.
        /// </summary>
        public void SortBy(string memberPath, ListSortDirection direction)
        {
            using (LoopsView.DeferRefresh())
            {
                LoopsView.SortDescriptions.Clear();
                LoopsView.SortDescriptions.Add(new SortDescription(nameof(RoofLoopModel.CategoryRank), ListSortDirection.Ascending));
                LoopsView.SortDescriptions.Add(new SortDescription(memberPath, direction));
                if (memberPath != nameof(RoofLoopModel.PerimeterMm))
                    LoopsView.SortDescriptions.Add(new SortDescription(nameof(RoofLoopModel.PerimeterMm), ListSortDirection.Ascending));
            }
        }

        partial void OnGlobalDivisionPointsChanged(int value)
        {
            foreach (var loop in VisibleLoops)
                loop.RecommendedPoints = value;

            RecomputeCounts();
            ApplyDivisionCommand.NotifyCanExecuteChanged();
        }

        // When true, only Circular loops start selected; Rectangular/Other start unselected.
        // Opt-in so Combined Roof Tools (which shares this ViewModel) keeps select-all.
        private readonly bool _circlesOnlyByDefault;

        public InnerLoopDividerViewModel(UIApplication app, ElementId roofId, List<RoofLoopModel> initialLoops,
                                         bool circlesOnlyByDefault = false)
        {
            _app = app;
            _roofId = roofId;
            _circlesOnlyByDefault = circlesOnlyByDefault;

            SetupCollectionView();
            PopulateLoops(initialLoops);

            AddLog(LogLevel.Success, $"Analysis complete — {Loops.Count} inner loop(s) found.");
            LogShapeBreakdown();

            InnerLoopDividerEventManager.Init();
        }

        private void SetupCollectionView()
        {
            LoopsView = CollectionViewSource.GetDefaultView(Loops);
            LoopsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(RoofLoopModel.ShapeCategory)));
            LoopsView.Filter = o => o is RoofLoopModel l && PassesFilter(l);
            // Default: circles by diameter (smallest first), then perimeter.
            SortBy(nameof(RoofLoopModel.DiameterMm), ListSortDirection.Ascending);
        }

        private void PopulateLoops(List<RoofLoopModel> loops)
        {
            DetachLoopHandlers();
            Loops.Clear();

            foreach (var loop in loops)
            {
                loop.IsSelected = !_circlesOnlyByDefault || loop.LoopShapeType == "Circular";
                // Circular → 6, everything else → 4
                loop.RecommendedPoints = loop.LoopShapeType == "Circular" ? 6 : 4;

                loop.PropertyChanged += OnLoopPropertyChanged;
                Loops.Add(loop);
            }

            RebuildSizeOptions();
            RecomputeCounts();
            ApplyDivisionCommand.NotifyCanExecuteChanged();
        }

        private void LogShapeBreakdown()
        {
            int circular   = Loops.Count(l => l.LoopShapeType == "Circular");
            int rectangles = Loops.Count(l => l.LoopShapeType == "Rectangle");
            int others     = Loops.Count(l => l.LoopShapeType == "Other");

            AddLog(LogLevel.Info, $"Circular: {circular}  ·  Rectangular: {rectangles}  ·  Other: {others}");
            AddLog(LogLevel.Info, "Defaults — Circular: 6 pts  ·  Rectangular/Other: 4 pts");
        }

        // ── Re-analyze (via ExternalEvent) ───────────────────────────────────
        [RelayCommand]
        private void Analyze()
        {
            Log.Clear();
            AddLog(LogLevel.Info, "Re-analyzing...");

            InnerLoopDividerHandler.Payload = new InnerLoopDividerPayload
            {
                RoofId = _roofId,
                Operation = InnerLoopDividerOperation.Analyze,
                Log = AddLog,
                OnCompleted = result =>
                {
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (!result.Success)
                        {
                            AddLog(LogLevel.Error, $"Analysis failed: {result.ErrorMessage}");
                            return;
                        }

                        PopulateLoops(result.Loops);
                        LogShapeBreakdown();
                    }));
                }
            };

            InnerLoopDividerEventManager.Event.Raise();
        }

        /// <summary>Raised after ApplyDivision's ExternalEvent round-trip completes
        /// (true = success), so callers outside this ViewModel (e.g. the Combined
        /// Roof Tools "Run All" orchestrator) can await completion without polling.</summary>
        public event Action<bool> ApplyDivisionCompleted;

        // ── Apply (via ExternalEvent) ─────────────────────────────────────────
        [RelayCommand(CanExecute = nameof(CanApply))]
        private void ApplyDivision()
        {
            var validLoops = VisibleLoops
                .Where(l => l.IsSelected && l.RecommendedPoints >= 1)
                .ToList();

            int totalPoints = validLoops.Sum(l => l.RecommendedPoints);
            AddLog(LogLevel.Info, $"Applying {totalPoints} point(s) to {validLoops.Count} loop(s)…");

            InnerLoopDividerHandler.Payload = new InnerLoopDividerPayload
            {
                RoofId = _roofId,
                Operation = InnerLoopDividerOperation.Apply,
                SelectedLoops = validLoops,
                Log = AddLog,
                OnCompleted = result =>
                {
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (!result.Success)
                            AddLog(LogLevel.Error, $"Apply failed: {result.ErrorMessage}");

                        ApplyDivisionCompleted?.Invoke(result.Success);
                    }));
                }
            };

            InnerLoopDividerEventManager.Event.Raise();
        }

        [RelayCommand]
        private void SelectAll() => SetAllSelected(true);

        [RelayCommand]
        private void ClearSelection() => SetAllSelected(false);

        public void SelectGroupLoops(string category)
        {
            foreach (var loop in VisibleLoops.Where(l => l.ShapeCategory == category))
                loop.IsSelected = true;
        }

        public void ClearGroupLoops(string category)
        {
            foreach (var loop in VisibleLoops.Where(l => l.ShapeCategory == category))
                loop.IsSelected = false;
        }

        [RelayCommand]
        private void ClearLog() => Log.Clear();

        private bool CanApply() => VisibleLoops.Any(l => l.IsSelected && l.RecommendedPoints >= 1);

        private void SetAllSelected(bool value)
        {
            foreach (var loop in VisibleLoops)
                loop.IsSelected = value;
        }

        private void OnLoopPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RoofLoopModel.IsSelected) ||
                e.PropertyName == nameof(RoofLoopModel.RecommendedPoints))
            {
                RecomputeCounts();
                ApplyDivisionCommand.NotifyCanExecuteChanged();
            }
        }

        private void RecomputeCounts()
        {
            InnerLoopCount     = Loops.Count;
            SelectedShapeCount = VisibleLoops.Count(l => l.IsSelected);
            QueuedPointCount   = VisibleLoops.Where(l => l.IsSelected).Sum(l => l.RecommendedPoints);
        }

        private void DetachLoopHandlers()
        {
            foreach (var loop in Loops)
                loop.PropertyChanged -= OnLoopPropertyChanged;
        }

        // ── AddLog (thread-safe) ──────────────────────────────────────────────
        private void AddLog(LogLevel level, string message) => AddLog(new LogEntry(level, message));

        private void AddLog(LogEntry entry)
        {
            var dispatcher = System.Windows.Application.Current.Dispatcher;
            if (dispatcher.CheckAccess())
                Log.Add(entry);
            else
                dispatcher.BeginInvoke(new Action(() => Log.Add(entry)));
        }
    }
}
