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
using Revit26_Plugin.RefSectionHeadPlacer.V001.Core.Engine;
using Revit26_Plugin.RefSectionHeadPlacer.V001.Core.Models;
using Revit26_Plugin.RefSectionHeadPlacer.V001.Core.Services;
using Revit26_Plugin.RefSectionHeadPlacer.V001.Infrastructure.ExternalEvents;
using Revit26_Plugin.RefSectionHeadPlacer.V001.Infrastructure.Helpers;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RefSectionHeadPlacer.V001.UI.ViewModels
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
        public ObservableCollection<ElementTypeRow> ElementTypes { get; } = new();

        // ── Grid 3: Type -> drafting view mapping (auto-built from Grid 2) ──
        public ObservableCollection<CategoryMappingRow> CategoryMappings { get; } = new();
        public ObservableCollection<DraftingViewOption> DraftingViews { get; } = new();

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

        /// <summary>Rebuilds Grid 2 from current view + link selection, then Grid 3.</summary>
        public void RefreshElementTypes()
        {
            foreach (var row in ElementTypes) row.PropertyChanged -= OnElementTypeRowChanged;
            ElementTypes.Clear();

            var selectedViews = _allPlanViews.Where(v => v.IsSelected).ToList();
            var selectedLinks = Links.Where(l => l.IsSelected).ToList();
            foreach (var row in _collectorService.CollectElementTypes(selectedViews, selectedLinks))
            {
                row.PropertyChanged += OnElementTypeRowChanged;
                ElementTypes.Add(row);
            }

            ElementCount = ElementTypes.Sum(t => t.Count);
            RebuildMappings();
            OnPropertyChanged(nameof(PlanViewSelectionSummary));
        }

        private void OnElementTypeRowChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ElementTypeRow.IsSelected))
                RebuildMappings();
        }

        /// <summary>
        /// Grid 3 = one row per SELECTED Grid 2 type. Existing drafting-view picks
        /// are preserved across rebuilds by matching on (Category, TypeName).
        /// </summary>
        private void RebuildMappings()
        {
            // Preserve prior drafting-view picks across rebuilds, keyed on the stable
            // (SourceLabel, Bic, TypeName) identity.
            var previous = CategoryMappings.ToDictionary(
                m => (m.SourceLabel, m.Bic, m.TypeName), m => m.MappedDraftingView);

            foreach (var m in CategoryMappings) m.PropertyChanged -= OnMappingRowChanged;
            CategoryMappings.Clear();

            foreach (var t in ElementTypes.Where(t => t.IsSelected))
            {
                previous.TryGetValue((t.SourceLabel, t.Bic, t.TypeName), out var keep);
                var row = new CategoryMappingRow(t.SourceLabel, t.Category, t.TypeName, t.Bic, keep);
                row.PropertyChanged += OnMappingRowChanged;
                CategoryMappings.Add(row);
            }

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
            => MappedCount = CategoryMappings.Count(m => m.IsSelected && m.MappedDraftingView != null);

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
            var selectedTypes = ElementTypes.Where(t => t.IsSelected).ToList();
            if (selectedTypes.Count == 0) { Log(LogLevel.Warning, "No elements selected — nothing to run."); return; }
            if (CategoryMappings.Any(m => m.IsSelected && m.MappedDraftingView == null))
            { Log(LogLevel.Warning, "One or more selected types have no drafting view mapped."); return; }

            _cancelRequested = false;
            IsRunning = true;
            PlacedCount = 0; SkippedCount = 0; ProgressPercent = 0;
            ProgressStatusText = "Starting…";

            _eventHandler.PendingRequest = new PlaceSectionsEventHandler.RequestArgs
            {
                SelectedTypes = selectedTypes,
                Mappings = CategoryMappings.ToList(),
                SectionViewFamilyTypeId = SelectedSectionType?.Id ?? ElementId.InvalidElementId,
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
                Log(LogLevel.Error, $"Run failed: {ex.Message}");
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
            foreach (var row in ElementTypes) row.PropertyChanged -= OnElementTypeRowChanged;
            foreach (var row in Links) row.PropertyChanged -= OnLinkRowChanged;
            foreach (var row in CategoryMappings) row.PropertyChanged -= OnMappingRowChanged;
            foreach (var opt in _allPlanTypeOptions) opt.PropertyChanged -= OnPlanTypeOptionChanged;

            _externalEvent?.Dispose();
        }
    }
}
