using System;
using System.Linq;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.Shared.Models;
using Revit26_Plugin.SmartViewToSheetPlacer.V204.Infrastructure.ExternalEvents;
using Revit26_Plugin.SmartViewToSheetPlacer.V204.Models;
using Revit26_Plugin.SmartViewToSheetPlacer.V204.Services;

namespace Revit26_Plugin.SmartViewToSheetPlacer.V204.ViewModels
{
    /// <summary>Stage 1: Select Views — view/titleblock loading, selection, filtering.</summary>
    public partial class SmartViewToSheetPlacerViewModel
    {
        // ---- Stage 1 state ----
        [ObservableProperty] private string _viewNameFilter = string.Empty;
        [ObservableProperty] private TitleblockOption? _selectedTitleblock;
        [ObservableProperty] private double _marginTopMm = 10.0;
        [ObservableProperty] private double _marginBottomMm = 10.0;
        [ObservableProperty] private double _marginLeftMm = 10.0;
        [ObservableProperty] private double _marginRightMm = 10.0;
        [ObservableProperty] private int _selectedViewCount;
        [ObservableProperty] private int _totalViewCount;
        [ObservableProperty] private bool _stage1Complete;
        public string Stage1StatusLabel => Stage1Complete ? "Complete" : "In Progress";

        // ---- Stage 1: View Name always-visible filter, View Type popover filter ----
        [ObservableProperty] private bool _isViewTypeFilterOpen;

        /// <summary>True when the View Name filter box has an active (non-empty) value.</summary>
        public bool IsViewNameFilterActive => !string.IsNullOrWhiteSpace(ViewNameFilter);

        /// <summary>True when at least one View Type is excluded from the filter.</summary>
        public bool IsViewTypeFilterActive => ViewTypeFilters.Any(f => !f.IsChecked);

        private void RunLoadViews()
        {
            IsBusy = true;
            BusyMessage = "Loading project views...";
            Logs.Add(new LogEntry(LogLevel.Info, "Requesting view + titleblock load from Revit."));

            _handler.Request = SmartViewToSheetPlacerRequest.LoadViews;
            _event.Raise();
        }

        [RelayCommand]
        private void RefreshViews() => RunLoadViews();

        [RelayCommand]
        private void SelectAllViews()
        {
            foreach (ViewInfo v in ViewsView)
                v.IsSelected = true;
            RecomputeSelectedCount();
        }

        [RelayCommand]
        private void ClearSelection()
        {
            foreach (ViewInfo v in ViewsView)
                v.IsSelected = false;
            RecomputeSelectedCount();
        }

        public void RecomputeSelectedCount()
        {
            SelectedViewCount = AllViews.Count(v => v.IsSelected);
        }

        /// <summary>Bound to the "All" link inside the View Type header popover.</summary>
        [RelayCommand]
        private void CheckAllViewTypes()
        {
            foreach (var f in ViewTypeFilters)
                f.IsChecked = true;
        }

        /// <summary>Bound to the "None" link inside the View Type header popover.</summary>
        [RelayCommand]
        private void UncheckAllViewTypes()
        {
            foreach (var f in ViewTypeFilters)
                f.IsChecked = false;
        }

        partial void OnStage1CompleteChanged(bool value)
        {
            OnPropertyChanged(nameof(Stage1StatusLabel));
            OnPropertyChanged(nameof(Stage2StatusLabel));
        }

        partial void OnViewNameFilterChanged(string value)
        {
            ViewsView.Refresh();
            OnPropertyChanged(nameof(IsViewNameFilterActive));
        }

        private bool FilterViews(object obj)
        {
            if (obj is not ViewInfo v) return false;

            if (!string.IsNullOrWhiteSpace(ViewNameFilter) &&
                v.Name.IndexOf(ViewNameFilter, StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            var typeFilter = ViewTypeFilters.FirstOrDefault(f => f.RevitViewType == v.RevitViewType);
            if (typeFilter != null && !typeFilter.IsChecked)
                return false;

            return true;
        }

        [RelayCommand(CanExecute = nameof(CanGoToStage2))]
        private void NextToStage2()
        {
            RecomputeSelectedCount();
            if (SelectedViewCount == 0)
            {
                Logs.Add(new LogEntry(LogLevel.Warning, "No views selected — cannot proceed to Suggested Placement."));
                return;
            }
            if (SelectedTitleblock == null)
            {
                Logs.Add(new LogEntry(LogLevel.Warning, "No titleblock selected — cannot proceed."));
                return;
            }

            SelectedTitleblock.ApplyMargins(MarginTopMm, MarginBottomMm, MarginLeftMm, MarginRightMm);
            SaveSettings();

            RunPacking();

            Stage1Complete = true;
            Stage1Expanded = false;
            Stage2Expanded = true;
        }

        private bool CanGoToStage2() => !IsBusy;

        /// <summary>
        /// Fires whenever a View Type filter checkbox is toggled in the column-header
        /// popover. Re-applies the ViewsView filter predicate and updates the header
        /// icon's "active" styling.
        /// </summary>
        private void OnViewTypeFilterOptionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ViewTypeFilterOption.IsChecked)) return;
            ViewsView.Refresh();
            OnPropertyChanged(nameof(IsViewTypeFilterActive));
        }

        /// <summary>
        /// Populates AllViews/ViewTypeFilters/Titleblocks from the handler's
        /// LoadViews output. Restores last-used titleblock by name and
        /// preserves View Type filter checked-state across a Refresh.
        /// </summary>
        private void HandleLoadViewsCompleted()
        {
            AllViews.Clear();
            foreach (var v in _handler.LoadedViews)
                AllViews.Add(v);
            TotalViewCount = AllViews.Count;

            // Preserve each type's checked state across a Refresh (match by RevitViewType)
            // so re-loading views doesn't silently clear a filter the user had set.
            var previousCheckedState = ViewTypeFilters.ToDictionary(f => f.RevitViewType, f => f.IsChecked);

            // Unsubscribe from any options left over from a prior load (e.g. Refresh),
            // mirroring the AllPlacements subscribe/unsubscribe pattern used in Stage 2.
            foreach (var existing in ViewTypeFilters)
                existing.PropertyChanged -= OnViewTypeFilterOptionPropertyChanged;

            ViewTypeFilters.Clear();
            foreach (var vt in AllViews.Select(v => v.RevitViewType).Distinct())
            {
                var option = new ViewTypeFilterOption(vt, ViewTypeLabelHelper.Label(vt));
                if (previousCheckedState.TryGetValue(vt, out var wasChecked))
                    option.IsChecked = wasChecked;
                option.PropertyChanged += OnViewTypeFilterOptionPropertyChanged;
                ViewTypeFilters.Add(option);
            }
            OnPropertyChanged(nameof(IsViewTypeFilterActive));

            Titleblocks.Clear();
            foreach (var tb in _handler.LoadedTitleblocks)
                Titleblocks.Add(tb);

            // Restore last-used titleblock by name, falling back to first available.
            SelectedTitleblock = Titleblocks.FirstOrDefault(t => t.Name == _settings.LastTitleblockName)
                                 ?? Titleblocks.FirstOrDefault();

            RecomputeSelectedCount();
        }
    }
}
