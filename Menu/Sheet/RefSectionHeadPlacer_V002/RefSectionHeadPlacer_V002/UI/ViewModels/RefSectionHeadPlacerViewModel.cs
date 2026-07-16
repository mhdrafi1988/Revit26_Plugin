using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.RefSectionHeadPlacer.V002.Core.Engine;
using Revit26_Plugin.RefSectionHeadPlacer.V002.Core.Models;
using Revit26_Plugin.RefSectionHeadPlacer.V002.Core.Services;
using Revit26_Plugin.RefSectionHeadPlacer.V002.Infrastructure.ExternalEvents;
using Revit26_Plugin.RefSectionHeadPlacer.V002.Infrastructure.Helpers;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RefSectionHeadPlacer.V002.UI.ViewModels
{
    /// <summary>
    /// ViewModel for RefSectionHeadPlacerWindow.
    ///
    /// MEMORY: implements IDisposable. The ViewModel subscribes to the long-lived
    /// ExternalEvent handler's events; if it never unsubscribed, the handler would
    /// keep the ViewModel (and every collection/LogEntry) alive after the window
    /// closed — a classic WPF/Revit leak. Dispose() unsubscribes and disposes the
    /// ExternalEvent. The window calls Dispose() in OnClosed.
    ///
    /// THREADING: engine callbacks arrive on Revit's API thread. We marshal to the
    /// UI thread with the dispatcher captured at construction (the VM is built on
    /// the UI thread). Application.Current is NOT used — it is frequently null in a
    /// Revit add-in, which would NRE.
    /// </summary>
    public partial class RefSectionHeadPlacerViewModel : ObservableObject, IDisposable
    {
        private const int MaxLogEntries = 2000; // cap so an all-day modeless session can't grow unbounded

        private readonly Document _doc;
        private readonly ElementCollectorService _collectorService;
        private readonly PlaceSectionsEventHandler _eventHandler;
        private readonly ExternalEvent _externalEvent;
        private readonly Dispatcher _uiDispatcher;

        private List<PlanViewRow> _allPlanViews = new();
        private volatile bool _cancelRequested; // written on UI thread, read on API thread
        private bool _disposed;

        /// <summary>Raised when the Close command runs; the window subscribes and closes itself.</summary>
        public event Action CloseRequested;

        // ── Selectors ─────────────────────────────────────────────────
        public ObservableCollection<ViewFamilyType> SectionTypes { get; } = new();
        [ObservableProperty] private ViewFamilyType selectedSectionType;

        // ── Reference section tail length (head->tail marker length) ──
        private const double DefaultTailLengthMm = 2000.0;
        private const double MmPerFoot = 304.8;

        [ObservableProperty]
        private string tailLengthMmText = DefaultTailLengthMm.ToString("F0");

        /// <summary>Parsed, validated tail length in mm. Falls back to the 2000 mm default
        /// on empty/invalid/non-positive input rather than blocking Run — flagged via log.</summary>
        public double TailLengthMm =>
            double.TryParse(TailLengthMmText, out var v) && v > 0 ? v : DefaultTailLengthMm;

        // ── Boundary buffer (roof bounding-box expansion for Grid 2 link scoping) ──
        private const double DefaultBoundaryBufferMm = 100.0;

        [ObservableProperty]
        private string boundaryBufferMmText = DefaultBoundaryBufferMm.ToString("F0");

        /// <summary>Parsed, validated boundary buffer in mm. Falls back to the 100 mm default
        /// on empty/invalid/non-positive input — flagged via log, doesn't block Run.</summary>
        public double BoundaryBufferMm =>
            double.TryParse(BoundaryBufferMmText, out var v) && v > 0 ? v : DefaultBoundaryBufferMm;

        /// <summary>Grid 2 header meta text — reflects the live buffer value.</summary>
        public string ElementScopeSummary => $"roofs + linked items within {BoundaryBufferMm:F0}mm";

        partial void OnBoundaryBufferMmTextChanged(string value)
        {
            OnPropertyChanged(nameof(ElementScopeSummary));
            RefreshElementTypes();
        }

        // ── Roof type filter (popover) — narrows which roofs anchor the boundary buffer ──
        private List<RoofTypeOption> _allRoofTypeOptions = new();
        private bool _suppressRoofTypeRecalc;

        /// <summary>Visible rows in the popover — narrowed by RoofTypeSearchText.</summary>
        public ObservableCollection<RoofTypeOption> RoofTypeOptions { get; } = new();

        [ObservableProperty]
        private string roofTypeSearchText = string.Empty;

        [ObservableProperty]
        private bool isRoofTypePopoverOpen;

        /// <summary>Button label — "All types" / "No types" / "2 of 3".</summary>
        public string RoofTypeSelectionSummary
        {
            get
            {
                var total = _allRoofTypeOptions.Count;
                if (total == 0) return "No types";
                var checkedCount = _allRoofTypeOptions.Count(t => t.IsChecked);
                if (checkedCount == total) return "All types";
                if (checkedCount == 0) return "No types";
                return $"{checkedCount} of {total}";
            }
        }

        public ObservableCollection<LinkRow> Links { get; } = new();
        public string LinkSelectionSummary =>
            $"{Links.Count(l => l.IsSelected)} of {Links.Count} selected";

        // ── Grid 1: Plan views ────────────────────────────────────────
        public ObservableCollection<PlanViewRow> PlanViews { get; } = new();
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PlanViewSelectionSummary))]
        private string planViewFilter = string.Empty;
        public string PlanViewSelectionSummary =>
            $"{_allPlanViews.Count(v => v.IsSelected)} of {_allPlanViews.Count} selected";

        // ── Grid 1: Plan Type filter (popover) ─────────────────────────
        private List<PlanTypeOption> _allPlanTypeOptions = new();
        private bool _suppressPlanTypeRecalc;

        /// <summary>Visible rows in the popover — narrowed by PlanTypeSearchText.</summary>
        public ObservableCollection<PlanTypeOption> PlanTypeOptions { get; } = new();

        [ObservableProperty]
        private string planTypeSearchText = string.Empty;

        [ObservableProperty]
        private bool isPlanTypePopoverOpen;

        /// <summary>Button label — "All types" / "No types" / "2 of 4".</summary>
        public string PlanTypeSelectionSummary
        {
            get
            {
                var total = _allPlanTypeOptions.Count;
                if (total == 0) return "No types";
                var checkedCount = _allPlanTypeOptions.Count(t => t.IsChecked);
                if (checkedCount == total) return "All types";
                if (checkedCount == 0) return "No types";
                return $"{checkedCount} of {total}";
            }
        }

        // ── Grid 2: Element categories & types ────────────────────────
        private List<ElementTypeRow> _allElementTypeRows = new();
        public ObservableCollection<ElementTypeRow> ElementTypes { get; } = new();

        [ObservableProperty]
        private string elementTypeFilter = string.Empty;

        partial void OnElementTypeFilterChanged(string value) => ApplyElementTypeFilter();

        // ── Grid 3: Type -> drafting view mapping (auto-built from Grid 2) ──
        private List<CategoryMappingRow> _allCategoryMappingRows = new();
        public ObservableCollection<CategoryMappingRow> CategoryMappings { get; } = new();
        public ObservableCollection<DraftingViewOption> DraftingViews { get; } = new();

        [ObservableProperty]
        private string categoryMappingFilter = string.Empty;

        partial void OnCategoryMappingFilterChanged(string value) => ApplyCategoryMappingFilter();

        // ── Metrics ───────────────────────────────────────────────────
        [ObservableProperty] private int elementCount;
        [ObservableProperty] private int mappedCount;
        [ObservableProperty] private int placedCount;
        [ObservableProperty] private int skippedCount;

        // ── Run state / progress ──────────────────────────────────────
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RunCommand))]
        private bool isRunning;
        [ObservableProperty] private string progressStatusText = string.Empty;
        [ObservableProperty] private string progressPercentText = "0%";
        [ObservableProperty] private double progressPercent;

        // ── Log ───────────────────────────────────────────────────────
        public ObservableCollection<LogEntry> LogEntries { get; } = new();

        public RefSectionHeadPlacerViewModel(Document doc)
        {
            _doc = doc;
            _uiDispatcher = Dispatcher.CurrentDispatcher;
            _collectorService = new ElementCollectorService(doc);

            _eventHandler = new PlaceSectionsEventHandler();
            _eventHandler.LogEmitted += OnEngineLog;
            _eventHandler.ProgressChanged += OnEngineProgress;
            _eventHandler.RunCompleted += OnRunCompleted;
            _eventHandler.RunFaulted += OnRunFaulted;
            _externalEvent = ExternalEvent.Create(_eventHandler);

            LoadInitialData();
        }

        private void LoadInitialData()
        {
            foreach (var vft in GeometryHelper.GetSectionViewFamilyTypes(_doc))
                SectionTypes.Add(vft);
            SelectedSectionType = SectionTypes.FirstOrDefault();

            foreach (var link in _collectorService.LoadLinkInstances())
            {
                link.PropertyChanged += OnLinkRowChanged;
                Links.Add(link);
            }

            _allPlanViews = _collectorService.LoadPlanViews();

            _allPlanTypeOptions = _allPlanViews
                .Select(v => v.ViewType)
                .Distinct()
                .OrderBy(t => t)
                .Select(t => new PlanTypeOption(t, FriendlyPlanType(t)))
                .ToList();
            foreach (var opt in _allPlanTypeOptions)
                opt.PropertyChanged += OnPlanTypeOptionChanged;
            ApplyPlanTypeSearch();

            ApplyPlanViewFilter();

            foreach (var dv in _collectorService.LoadDraftingViews())
                DraftingViews.Add(dv);

            Log(LogLevel.Info, Links.Count == 0
                ? "Ready. No loaded links found — link a model to target doors/plumbing/walls."
                : "Ready. Select plan view(s) to load elements.");
        }

        partial void OnPlanViewFilterChanged(string value) => ApplyPlanViewFilter();
        private void OnLinkRowChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LinkRow.IsSelected))
            {
                OnPropertyChanged(nameof(LinkSelectionSummary));
                RefreshElementTypes();
            }
        }

        [RelayCommand]
        private void SelectAllLinks()
        {
            foreach (var l in Links) l.IsSelected = true;
        }

        [RelayCommand]
        private void ClearLinks()
        {
            foreach (var l in Links) l.IsSelected = false;
        }

        private void ApplyPlanViewFilter()
        {
            // Detach handlers from rows leaving the visible set, attach to incoming.
            foreach (var row in PlanViews) row.PropertyChanged -= OnPlanViewRowChanged;
            PlanViews.Clear();

            var nameFilter = PlanViewFilter?.Trim() ?? string.Empty;
            var checkedTypes = new HashSet<string>(
                _allPlanTypeOptions.Where(t => t.IsChecked).Select(t => t.RawViewType));

            foreach (var row in _allPlanViews)
            {
                bool matchesName = nameFilter.Length == 0 ||
                    row.ViewName.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) >= 0;
                bool matchesType = checkedTypes.Contains(row.ViewType);

                if (matchesName && matchesType)
                {
                    row.PropertyChanged += OnPlanViewRowChanged;
                    PlanViews.Add(row);
                }
            }
            OnPropertyChanged(nameof(PlanViewSelectionSummary));
        }

        partial void OnPlanTypeSearchTextChanged(string value) => ApplyPlanTypeSearch();

        /// <summary>Narrows the popover's checkbox list by DisplayName — does NOT touch the main grid.</summary>
        private void ApplyPlanTypeSearch()
        {
            PlanTypeOptions.Clear();
            var search = PlanTypeSearchText?.Trim() ?? string.Empty;
            foreach (var opt in _allPlanTypeOptions)
            {
                if (search.Length == 0 || opt.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                    PlanTypeOptions.Add(opt);
            }
        }

        private void OnPlanTypeOptionChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PlanTypeOption.IsChecked) && !_suppressPlanTypeRecalc)
            {
                OnPropertyChanged(nameof(PlanTypeSelectionSummary));
                ApplyPlanViewFilter();
            }
        }

        [RelayCommand]
        private void SelectAllPlanTypes() => SetAllPlanTypes(true);

        [RelayCommand]
        private void ClearPlanTypes() => SetAllPlanTypes(false);

        private void SetAllPlanTypes(bool value)
        {
            _suppressPlanTypeRecalc = true;
            foreach (var opt in _allPlanTypeOptions) opt.IsChecked = value;
            _suppressPlanTypeRecalc = false;
            OnPropertyChanged(nameof(PlanTypeSelectionSummary));
            ApplyPlanViewFilter();
        }

        /// <summary>Maps a raw Revit ViewType to a friendlier popover label. Falls back to
        /// inserting spaces before capitals for any ViewType not explicitly listed.</summary>
        private static string FriendlyPlanType(string rawViewType) => rawViewType switch
        {
            "FloorPlan" => "Floor Plan",
            "CeilingPlan" => "Ceiling Plan",
            "AreaPlan" => "Area Plan",
            "EngineeringPlan" => "Engineering Plan",
            _ => System.Text.RegularExpressions.Regex.Replace(rawViewType, "(?<!^)([A-Z])", " $1")
        };

        private void OnPlanViewRowChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PlanViewRow.IsSelected))
                RefreshElementTypes();
        }

        [RelayCommand]
        private void SelectAllPlanViews()
        {
            foreach (var row in PlanViews) row.IsSelected = true;
            RefreshElementTypes();
        }

        [RelayCommand]
        private void ClearPlanViews()
        {
            foreach (var row in PlanViews) row.IsSelected = false;
            RefreshElementTypes();
        }

        /// <summary>Rebuilds the Roof Type popover from roofs found in currently selected
        /// plan views. Prior ticked/unticked state is preserved by matching on type name;
        /// new type names default to ticked (no filtering) like the Plan Type filter.</summary>
        private void RefreshRoofTypeOptions()
        {
            var previousChecked = _allRoofTypeOptions.ToDictionary(o => o.RawTypeName, o => o.IsChecked);
            foreach (var opt in _allRoofTypeOptions) opt.PropertyChanged -= OnRoofTypeOptionChanged;

            var selectedViews = _allPlanViews.Where(v => v.IsSelected).ToList();
            var names = _collectorService.GetRoofTypeNames(selectedViews);

            _allRoofTypeOptions = names
                .Select(n => new RoofTypeOption(n, !previousChecked.TryGetValue(n, out var wasChecked) || wasChecked))
                .ToList();
            foreach (var opt in _allRoofTypeOptions) opt.PropertyChanged += OnRoofTypeOptionChanged;

            ApplyRoofTypeSearch();
            OnPropertyChanged(nameof(RoofTypeSelectionSummary));
        }

        partial void OnRoofTypeSearchTextChanged(string value) => ApplyRoofTypeSearch();

        /// <summary>Narrows the popover's checkbox list by DisplayName — does NOT touch Grid 2.</summary>
        private void ApplyRoofTypeSearch()
        {
            RoofTypeOptions.Clear();
            var search = RoofTypeSearchText?.Trim() ?? string.Empty;
            foreach (var opt in _allRoofTypeOptions)
            {
                if (search.Length == 0 || opt.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                    RoofTypeOptions.Add(opt);
            }
        }

        private void OnRoofTypeOptionChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RoofTypeOption.IsChecked) && !_suppressRoofTypeRecalc)
            {
                OnPropertyChanged(nameof(RoofTypeSelectionSummary));
                RefreshElementTypes();
            }
        }

        [RelayCommand]
        private void SelectAllRoofTypes() => SetAllRoofTypes(true);

        [RelayCommand]
        private void ClearRoofTypes() => SetAllRoofTypes(false);

        private void SetAllRoofTypes(bool value)
        {
            _suppressRoofTypeRecalc = true;
            foreach (var opt in _allRoofTypeOptions) opt.IsChecked = value;
            _suppressRoofTypeRecalc = false;
            OnPropertyChanged(nameof(RoofTypeSelectionSummary));
            RefreshElementTypes();
        }

        /// <summary>Rebuilds Grid 2 from current view + link + roof-type selection, then Grid 3.
        /// Logs diagnostic Info/Warning entries at each stage so a "nothing collected" or
        /// "nothing placed" report can be traced back to its actual cause.</summary>
        public void RefreshElementTypes()
        {
            RefreshRoofTypeOptions();

            foreach (var row in _allElementTypeRows) row.PropertyChanged -= OnElementTypeRowChanged;

            var selectedViews = _allPlanViews.Where(v => v.IsSelected).ToList();
            var selectedLinks = Links.Where(l => l.IsSelected).ToList();

            if (selectedViews.Count == 0)
            {
                _allElementTypeRows = new List<ElementTypeRow>();
                ApplyElementTypeFilter();
                ElementCount = 0;
                RebuildMappings();
                OnPropertyChanged(nameof(PlanViewSelectionSummary));
                return;
            }

            var rawRoofCount = _collectorService.CountRoofsInViews(selectedViews);
            var allowedRoofTypeNames = new HashSet<string>(
                _allRoofTypeOptions.Where(t => t.IsChecked).Select(t => t.RawTypeName));

            Log(LogLevel.Info,
                $"Scan · {selectedViews.Count} view(s), {selectedLinks.Count} link(s) selected · " +
                $"{rawRoofCount} roof(s) found, {_allRoofTypeOptions.Count} distinct type(s), " +
                $"{allowedRoofTypeNames.Count} ticked · buffer {BoundaryBufferMm:F0}mm.");

            if (rawRoofCount == 0)
                Log(LogLevel.Warning, "No roofs found in the selected plan view(s) — check the view's crop region/view range, or select a different view.");
            else if (allowedRoofTypeNames.Count == 0)
                Log(LogLevel.Warning, "All roof types are unticked in the Roof type filter — nothing will anchor the boundary buffer.");

            _allElementTypeRows = _collectorService.CollectElementTypes(
                selectedViews, selectedLinks, allowedRoofTypeNames, BoundaryBufferMm);
            foreach (var row in _allElementTypeRows) row.PropertyChanged += OnElementTypeRowChanged;

            var qualifyingRoofCount = _allElementTypeRows
                .Where(r => r.Bic == BuiltInCategory.OST_Roofs).Sum(r => r.Count);
            if (rawRoofCount > 0 && allowedRoofTypeNames.Count > 0 && qualifyingRoofCount == 0)
                Log(LogLevel.Warning, "Roof Type filter excluded every roof found — Grid 2 will be empty.");

            foreach (var linkRow in selectedLinks)
            {
                var linkRows = _allElementTypeRows.Where(r => r.SourceLabel == linkRow.Name).ToList();
                if (linkRows.Count == 0)
                {
                    Log(LogLevel.Info, $"Link '{linkRow.Name}' · 0 items collected (nothing within {BoundaryBufferMm:F0}mm of a qualifying roof).");
                    continue;
                }
                var byCategory = string.Join(", ", linkRows
                    .GroupBy(r => r.Category)
                    .Select(g => $"{g.Sum(r => r.Count)} {g.Key}"));
                Log(LogLevel.Info, $"Link '{linkRow.Name}' · {byCategory}.");
            }

            ApplyElementTypeFilter();

            ElementCount = _allElementTypeRows.Sum(t => t.Count);
            RebuildMappings();
            OnPropertyChanged(nameof(PlanViewSelectionSummary));
        }

        /// <summary>Narrows Grid 2's visible rows by ElementTypeFilter — does NOT touch selection state.</summary>
        private void ApplyElementTypeFilter()
        {
            ElementTypes.Clear();
            var filter = ElementTypeFilter?.Trim() ?? string.Empty;
            foreach (var row in _allElementTypeRows)
            {
                if (filter.Length == 0 ||
                    row.TypeName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    row.Category.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    ElementTypes.Add(row);
            }
        }

        [RelayCommand]
        private void SelectAllElementTypes()
        {
            foreach (var row in ElementTypes) row.IsSelected = true;
        }

        [RelayCommand]
        private void ClearElementTypes()
        {
            foreach (var row in ElementTypes) row.IsSelected = false;
        }

        private void OnElementTypeRowChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ElementTypeRow.IsSelected))
                RebuildMappings();
        }

        /// <summary>
        /// Grid 3 = one row per SELECTED Grid 2 type (across the FULL set, not just
        /// currently filtered/visible rows). Existing drafting-view picks are preserved
        /// across rebuilds by matching on (SourceLabel, Bic, TypeName).
        /// </summary>
        private void RebuildMappings()
        {
            var previous = _allCategoryMappingRows.ToDictionary(
                m => (m.SourceLabel, m.Bic, m.TypeName), m => m.MappedDraftingView);

            foreach (var m in _allCategoryMappingRows) m.PropertyChanged -= OnMappingRowChanged;

            _allCategoryMappingRows = _allElementTypeRows.Where(t => t.IsSelected).Select(t =>
            {
                previous.TryGetValue((t.SourceLabel, t.Bic, t.TypeName), out var keep);
                var row = new CategoryMappingRow(t.SourceLabel, t.Category, t.TypeName, t.Bic, keep);
                row.PropertyChanged += OnMappingRowChanged;
                return row;
            }).ToList();

            ApplyCategoryMappingFilter();
            RecomputeMappedCount();
        }

        /// <summary>Narrows Grid 3's visible rows by CategoryMappingFilter — does NOT touch selection state.</summary>
        private void ApplyCategoryMappingFilter()
        {
            CategoryMappings.Clear();
            var filter = CategoryMappingFilter?.Trim() ?? string.Empty;
            foreach (var row in _allCategoryMappingRows)
            {
                if (filter.Length == 0 ||
                    row.TypeName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    row.Category.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    CategoryMappings.Add(row);
            }
        }

        [RelayCommand]
        private void SelectAllMappings()
        {
            foreach (var row in CategoryMappings) row.IsSelected = true;
            RecomputeMappedCount();
        }

        [RelayCommand]
        private void ClearMappings()
        {
            foreach (var row in CategoryMappings) row.IsSelected = false;
            RecomputeMappedCount();
        }

        // Keeps the "Mapped" metric live as the user assigns drafting views in Grid 3.
        private void OnMappingRowChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CategoryMappingRow.MappedDraftingView) ||
                e.PropertyName == nameof(CategoryMappingRow.IsSelected))
                RecomputeMappedCount();
        }

        private void RecomputeMappedCount()
            => MappedCount = _allCategoryMappingRows.Count(m => m.IsSelected && m.MappedDraftingView != null);

        // ── Log commands (Copy Selected is in code-behind, per convention) ──
        [RelayCommand]
        private void CopyAllLogs()
            => System.Windows.Clipboard.SetText(string.Join(Environment.NewLine, LogEntries.Select(e => e.ToString())));

        [RelayCommand]
        private void ExportLogs()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Text file (*.txt)|*.txt",
                FileName = $"RefSectionHeadPlacer_Logs_{DateTime.Now:yyyy-MM-dd_HH-mm}.txt"
            };
            if (dialog.ShowDialog() == true)
                System.IO.File.WriteAllText(dialog.FileName,
                    string.Join(Environment.NewLine, LogEntries.Select(e => e.ToString())));
        }

        [RelayCommand]
        private void ClearLogs() => LogEntries.Clear();

        // ── Run / Cancel / Close ──────────────────────────────────────
        [RelayCommand(CanExecute = nameof(CanRun))]
        private void Run()
        {
            var selectedTypes = _allElementTypeRows.Where(t => t.IsSelected).ToList();
            if (selectedTypes.Count == 0) { Log(LogLevel.Warning, "No elements selected — nothing to run."); return; }
            if (_allCategoryMappingRows.Any(m => m.IsSelected && m.MappedDraftingView == null))
            { Log(LogLevel.Warning, "One or more selected types have no drafting view mapped."); return; }

            if (!double.TryParse(TailLengthMmText, out var parsedTailMm) || parsedTailMm <= 0)
                Log(LogLevel.Warning, $"Tail length \"{TailLengthMmText}\" is invalid — using default {DefaultTailLengthMm:F0} mm.");
            if (!double.TryParse(BoundaryBufferMmText, out var parsedBufferMm) || parsedBufferMm <= 0)
                Log(LogLevel.Warning, $"Boundary buffer \"{BoundaryBufferMmText}\" is invalid — using default {DefaultBoundaryBufferMm:F0} mm.");

            Log(LogLevel.Info,
                $"Run · {selectedTypes.Count} type(s) selected, {selectedTypes.Sum(t => t.Count)} element(s) total, " +
                $"section type '{SelectedSectionType?.Name ?? "(none)"}', tail {TailLengthMm:F0}mm.");

            _cancelRequested = false;
            IsRunning = true;
            PlacedCount = 0; SkippedCount = 0; ProgressPercent = 0;
            ProgressStatusText = "Starting…";

            _eventHandler.PendingRequest = new PlaceSectionsEventHandler.RequestArgs
            {
                SelectedTypes = selectedTypes,
                Mappings = _allCategoryMappingRows.ToList(),
                SectionViewFamilyTypeId = SelectedSectionType?.Id ?? ElementId.InvalidElementId,
                TailLengthFeet = TailLengthMm / MmPerFoot,
                CancellationRequested = () => _cancelRequested
            };
            _externalEvent.Raise();
        }

        private bool CanRun() => !IsRunning;

        [RelayCommand]
        private void Cancel() => _cancelRequested = true;

        [RelayCommand]
        private void Close() => CloseRequested?.Invoke();

        // ── Engine callbacks (API thread -> marshal to UI thread) ──────
        private void OnEngineLog(LogEntry entry)
            => _uiDispatcher.BeginInvoke(new Action(() =>
            {
                LogEntries.Add(entry);
                while (LogEntries.Count > MaxLogEntries) LogEntries.RemoveAt(0);
            }));

        private void OnEngineProgress(int current, int total)
            => _uiDispatcher.BeginInvoke(new Action(() =>
            {
                ProgressStatusText = $"Processing… {current} of {total}";
                ProgressPercent = total == 0 ? 0 : (double)current / total * 100;
                ProgressPercentText = $"{ProgressPercent:F0}%";
            }));

        private void OnRunCompleted(RunSummary summary)
            => _uiDispatcher.BeginInvoke(new Action(() =>
            {
                PlacedCount = summary.PlacedCount;
                SkippedCount = summary.SkippedCount;
                IsRunning = false;
                ProgressStatusText =
                    $"Completed — {summary.PlacedCount} placed | {summary.SkippedCount} skipped | {summary.FailedCount} failed";
            }));

        private void OnRunFaulted(Exception ex)
            => _uiDispatcher.BeginInvoke(new Action(() =>
            {
                IsRunning = false;
                var detail = ex.InnerException != null
                    ? $"{ex.GetType().Name}: {ex.Message} | Inner: {ex.InnerException.Message}"
                    : $"{ex.GetType().Name}: {ex.Message}";
                Log(LogLevel.Error, $"Run failed: {detail}");
            }));

        private void Log(LogLevel level, string message) => LogEntries.Add(new LogEntry(level, message));

        // ── Cleanup ───────────────────────────────────────────────────
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _eventHandler.LogEmitted -= OnEngineLog;
            _eventHandler.ProgressChanged -= OnEngineProgress;
            _eventHandler.RunCompleted -= OnRunCompleted;
            _eventHandler.RunFaulted -= OnRunFaulted;

            foreach (var row in PlanViews) row.PropertyChanged -= OnPlanViewRowChanged;
            foreach (var row in _allElementTypeRows) row.PropertyChanged -= OnElementTypeRowChanged;
            foreach (var row in Links) row.PropertyChanged -= OnLinkRowChanged;
            foreach (var row in _allCategoryMappingRows) row.PropertyChanged -= OnMappingRowChanged;
            foreach (var opt in _allPlanTypeOptions) opt.PropertyChanged -= OnPlanTypeOptionChanged;
            foreach (var opt in _allRoofTypeOptions) opt.PropertyChanged -= OnRoofTypeOptionChanged;

            _externalEvent?.Dispose();
        }
    }
}
