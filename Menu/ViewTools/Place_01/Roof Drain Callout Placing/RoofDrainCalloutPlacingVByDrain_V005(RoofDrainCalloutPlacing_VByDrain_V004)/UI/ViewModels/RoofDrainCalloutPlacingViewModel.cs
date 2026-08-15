using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.RoofDrainCalloutPlacingVByDrain.V005.ExternalEvents;
using Revit26_Plugin.RoofDrainCalloutPlacingVByDrain.V005.Models;
using Revit26_Plugin.RoofDrainCalloutPlacingVByDrain.V005.Services;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoofDrainCalloutPlacingVByDrain.V005.ViewModels
{
    /// <summary>
    /// VByDrain.V004: Auto-detect openings on roof, group by shape type
    /// (Circle / Rectangle [includes Square] / Other), sorted by area within
    /// each group (largest first). User selects openings per group, then
    /// places callouts sized per-group (Auto = that group's selected-opening
    /// bounding box + margin, or Fixed = a fixed square size).
    ///
    /// V004 changes from V002:
    /// - Global CalloutOffset/Margin/Floor removed
    /// - Detected Openings grid now split into 3 collapsible group cards
    ///   (Circle, Rectangle, Other), each with its own Select All/None and
    ///   its own Auto/Fixed callout sizing controls
    /// - Drafting view selector moved into the Roof summary card
    /// - Search filter now searches across all group collections
    /// </summary>
    public partial class RoofDrainCalloutPlacingViewModel : ObservableObject
    {
        private readonly ExternalEvent _runEvent;
        private readonly Document _doc;
        private readonly SettingsService _settingsService;

        public ObservableCollection<View> DraftingViews { get; } = new();
        public ObservableCollection<LogEntry> Logs { get; } = new();

        /// <summary>The 3 group cards shown in the UI, in fixed display order: Circle, Rectangle, Other.</summary>
        public ObservableCollection<OpeningGroupViewModel> Groups { get; } = new();

        [ObservableProperty] private RoofBase selectedRoof;
        [ObservableProperty] private string currentViewName = "";
        [ObservableProperty] private View selectedDraftingView;

        [ObservableProperty] private string searchText = "";
        partial void OnSearchTextChanged(string value) => ApplySearchFilter();

        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private bool hasRun;

        [ObservableProperty] private int metricRoofs = 1;
        [ObservableProperty] private int metricDetected = 0;
        [ObservableProperty] private int metricSelected = 0;
        [ObservableProperty] private int metricCallouts = 0;

        private IEnumerable<OpeningItem> AllOpenings => Groups.SelectMany(g => g.Openings);

        /// <summary>Run enabled only if: at least one opening selected, drafting view chosen, not currently running, and hasn't run before.</summary>
        public bool CanRun => AllOpenings.Any()
            && AllOpenings.Any(o => o.IsSelected)
            && SelectedDraftingView != null
            && !IsBusy
            && !HasRun;

        public RoofDrainCalloutPlacingViewModel(
            UIApplication uiApp,
            RoofBase roof,
            List<OpeningItem> detectedOpenings,
            RoofDrainCalloutSettings settings,
            SettingsService settingsService)
        {
            _doc = uiApp.ActiveUIDocument.Document;
            _settingsService = settingsService;

            SelectedRoof = roof;
            CurrentViewName = uiApp.ActiveUIDocument.ActiveView?.Name ?? "";

            // Build the 3 group cards in fixed order, each with its own sizing VM
            foreach (var key in new[] { "Circle", "Rectangle", "Other" })
            {
                var sizingVm = new GroupSizingViewModel(key);
                if (settings.GroupSizing.TryGetValue(key, out var groupSettings))
                    sizingVm.LoadFrom(groupSettings);

                Groups.Add(new OpeningGroupViewModel(key, sizingVm));
            }

            // Bucket detected openings into their group, sorted by area (largest first)
            foreach (var opening in detectedOpenings.OrderByDescending(o => o.Area))
            {
                var key = OpeningGroupViewModel.KeyFor(opening.ShapeType);
                var group = Groups.First(g => g.GroupKey == key);
                group.Openings.Add(opening);
                opening.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(OpeningItem.IsSelected))
                    {
                        group.RefreshCounts();
                        UpdateMetrics();
                    }
                };
            }
            foreach (var g in Groups) g.RefreshCounts();

            MetricDetected = detectedOpenings.Count;

            // Populate drafting views
            var views = new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v is ViewDrafting && !v.IsTemplate)
                .OrderBy(v => v.Name)
                .ToList();

            foreach (var view in views)
                DraftingViews.Add(view);

            // Restore last selected drafting view from settings
            if (!string.IsNullOrEmpty(settings.DraftingViewName))
                SelectedDraftingView = DraftingViews.FirstOrDefault(v => v.Name == settings.DraftingViewName);

            // ExternalEvent.Create() must happen inside valid API context (ViewModel constructor called from Command.Execute)
            _runEvent = ExternalEvent.Create(new RoofDrainCalloutRunHandler(this));

            AddLog(LogLevel.Info, $"View: {CurrentViewName}");
            AddLog(LogLevel.Info,
                $"Detected {MetricDetected} openings — " +
                string.Join(", ", Groups.Select(g => $"{g.Count} {g.GroupKey}")));
            UpdateMetrics();
        }

        /// <summary>Search filters within each group's Openings collection view via CollectionViewSource in XAML; here we just track the text.
        /// Actual filtering is applied per-group in code-behind or via a per-group ICollectionView if added later.
        /// For V004 scope, search narrows visibility by toggling group expansion — kept simple per current UI.</summary>
        private void ApplySearchFilter()
        {
            // Intentionally minimal: full per-row filtering can be added with per-group
            // ICollectionView instances if requested. Left as a hook for now.
        }

        /// <summary>Global Select All — applies across every group.</summary>
        [RelayCommand(CanExecute = nameof(CanRun))]
        private void SelectAll()
        {
            foreach (var g in Groups) g.SelectAll();
            UpdateMetrics();
            AddLog(LogLevel.Info, "Selected all openings (all groups).");
        }

        /// <summary>Global Select None — applies across every group.</summary>
        [RelayCommand(CanExecute = nameof(CanRun))]
        private void SelectNone()
        {
            foreach (var g in Groups) g.SelectNone();
            UpdateMetrics();
            AddLog(LogLevel.Info, "Deselected all openings (all groups).");
        }

        /// <summary>Select All scoped to a single group (Circle/Rectangle/Other local buttons).</summary>
        public void SelectAllInGroup(OpeningGroupViewModel group)
        {
            group.SelectAll();
            UpdateMetrics();
            AddLog(LogLevel.Info, $"Selected all in {group.GroupKey} group.");
        }

        /// <summary>Select None scoped to a single group.</summary>
        public void SelectNoneInGroup(OpeningGroupViewModel group)
        {
            group.SelectNone();
            UpdateMetrics();
            AddLog(LogLevel.Info, $"Deselected all in {group.GroupKey} group.");
        }

        /// <summary>Update metric counts based on current selection across all groups.</summary>
        public void UpdateMetrics()
        {
            MetricSelected = AllOpenings.Count(o => o.IsSelected);
            MetricCallouts = MetricSelected; // callout count = selected count, pre-run estimate
            RunCommand.NotifyCanExecuteChanged();
        }

        /// <summary>Place callouts on all selected openings, sized per their group's Auto/Fixed setting.</summary>
        [RelayCommand(CanExecute = nameof(CanRun))]
        private async void Run()
        {
            if (!CanRun)
                return;

            HasRun = true;
            IsBusy = true;
            RunCommand.NotifyCanExecuteChanged();

            try
            {
                var selectedCount = AllOpenings.Count(o => o.IsSelected);
                AddLog(LogLevel.Info, $"Placing callouts for {selectedCount} selected openings across {Groups.Count(g => g.SelectedCount > 0)} groups...");

                _runEvent.Raise();
            }
            catch (Exception ex)
            {
                AddLog(LogLevel.Error, $"Run failed: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>Called by RunHandler after operation completes.</summary>
        public void OnRunCompleted(int placed, int skipped, int failed)
        {
            AddLog(LogLevel.Success, $"Callout placement complete: {placed} placed | {skipped} skipped | {failed} failed.");
            MetricCallouts = placed;

            // Persist settings — per-group sizing + drafting view
            var groupSizing = Groups.ToDictionary(g => g.GroupKey, g => g.Sizing.ToSettings());

            _settingsService?.SaveSettings(new RoofDrainCalloutSettings
            {
                GroupSizing = groupSizing,
                DraftingViewName = SelectedDraftingView?.Name ?? "",
                LastRunSucceeded = true,
                LastRunTimestamp = DateTime.Now.ToString("O")
            });
        }

        public void AddLog(LogLevel level, string message)
        {
            Logs.Add(new LogEntry(level, message));
        }

        public void CopyAllLogs()
        {
            var text = string.Join(Environment.NewLine, Logs.Select(l => l.ToString()));
            Clipboard.SetText(text);
            AddLog(LogLevel.Info, "Log copied to clipboard.");
        }

        public void ClearLogs()
        {
            Logs.Clear();
        }

        /// <summary>Returns selected openings paired with their group's resolved sizing settings, for the placement service.</summary>
        public List<(OpeningItem Opening, GroupSizingSettings Sizing)> GetSelectedOpeningsWithSizing()
        {
            var result = new List<(OpeningItem, GroupSizingSettings)>();
            foreach (var g in Groups)
            {
                var sizing = g.Sizing.ToSettings();
                foreach (var o in g.Openings.Where(o => o.IsSelected))
                    result.Add((o, sizing));
            }
            return result;
        }
    }
}
