using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using Revit26_Plugin.CalloutCOP.V015.ExternalEvents;
using Revit26_Plugin.CalloutCOP.V015.Services;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.CalloutCOP.V015.ViewModels
{
    public partial class CalloutCOPViewModel : ObservableObject
    {
        public ObservableCollection<ViewItemViewModel> Views { get; }
        public ICollectionView ViewsCollection { get; }

        public ObservableCollection<ViewDrafting> DraftingViews { get; }
        public ObservableCollection<string> SheetFilterItems { get; }

        public ObservableCollection<LogEntry> Logs { get; } = new();

        private readonly ExternalEvent _externalEvent;
        private readonly CalloutPlacementExternalEvent _handler;

        [ObservableProperty] private string _sheetFilterText = "ALL";
        [ObservableProperty] private bool _showPlaced = true;
        [ObservableProperty] private bool _showUnplaced = true;
        [ObservableProperty] private bool _showSections = true;
        [ObservableProperty] private bool _showElevations = true;

        // Bulk-fill toolbar - applies to all checked (IsSelected) rows on demand.
        // A null slot here is left untouched on target rows; only non-null slots overwrite.
        [ObservableProperty] private ViewDrafting _bulkFillLeftView;
        [ObservableProperty] private ViewDrafting _bulkFillCenterView;
        [ObservableProperty] private ViewDrafting _bulkFillRightView;

        // Default callout size, mm. No fallback view anymore - Center is
        // blank unless a row (or bulk-fill) explicitly sets it.
        [ObservableProperty] private double _calloutSize = 500;
        [ObservableProperty] private bool _isSizeAutoSuggested = true;

        // Execution state
        [ObservableProperty] private bool _isRunning;
        [ObservableProperty] private string _progressText = string.Empty;

        // Summary card counts
        [ObservableProperty] private int _selectedCount;
        [ObservableProperty] private int _placedCount;
        [ObservableProperty] private int _runSuccessCount;
        [ObservableProperty] private int _runFailedCount;
        [ObservableProperty] private int _runSkippedCount;

        public CalloutCOPViewModel(ExternalCommandData data)
        {
            var doc = data.Application.ActiveUIDocument.Document;

            Views = ViewCollectionService.CollectViews(doc);
            DraftingViews = ViewCollectionService.CollectDraftingViews(doc);

            SheetFilterItems = ViewCollectionService.CollectSheetNumbers(doc);
            if (!SheetFilterItems.Contains("ALL"))
                SheetFilterItems.Insert(0, "ALL");

            ViewsCollection = CollectionViewSource.GetDefaultView(Views);
            ViewsCollection.Filter = FilterViews;

            foreach (var vm in Views)
                vm.PropertyChanged += OnViewItemPropertyChanged;

            _handler = new CalloutPlacementExternalEvent(
                doc,
                Views,
                Logs,
                () => CalloutSize,
                OnPlacementFinished);

            _externalEvent = ExternalEvent.Create(_handler);

            PlacedCount = Views.Count(v => v.IsPlaced);
            UpdateSelectedCount();
            UpdateSuggestedCalloutSize();
            LogInfo("Callout COP V015 initialized.");
        }

        partial void OnSheetFilterTextChanged(string value) => ViewsCollection.Refresh();
        partial void OnShowPlacedChanged(bool value) => ViewsCollection.Refresh();
        partial void OnShowUnplacedChanged(bool value) => ViewsCollection.Refresh();
        partial void OnShowSectionsChanged(bool value) => ViewsCollection.Refresh();
        partial void OnShowElevationsChanged(bool value) => ViewsCollection.Refresh();
        partial void OnCalloutSizeChanged(double value) => IsSizeAutoSuggested = false;

        private bool CanPlaceCallouts() => !IsRunning;

        [RelayCommand(CanExecute = nameof(CanPlaceCallouts))]
        private void PlaceCallouts()
        {
            var count = Views.Count(v => v.IsSelected);
            if (count == 0)
            {
                LogWarning("No target views selected.");
                return;
            }

            IsRunning = true;
            ProgressText = $"Running - {count} view(s)";
            _externalEvent.Raise();
        }

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var vm in VisibleViews())
                vm.IsSelected = true;
        }

        [RelayCommand]
        private void SelectNone()
        {
            foreach (var vm in VisibleViews())
                vm.IsSelected = false;
        }

        [RelayCommand]
        private void InverseSelection()
        {
            foreach (var vm in VisibleViews())
                vm.IsSelected = !vm.IsSelected;
        }

        // Selection controls only ever touch rows currently visible under the
        // active filters - hidden/filtered-out rows are left untouched.
        private IEnumerable<ViewItemViewModel> VisibleViews()
            => ViewsCollection.Cast<ViewItemViewModel>().ToList();

        [RelayCommand]
        private void ApplyBulkFill()
        {
            var targets = Views.Where(v => v.IsSelected).ToList();
            if (!targets.Any())
            {
                LogWarning("No target views selected for bulk-fill.");
                return;
            }

            if (BulkFillLeftView == null && BulkFillCenterView == null && BulkFillRightView == null)
            {
                LogWarning("Bulk-fill: nothing set in Left/Center/Right - nothing to apply.");
                return;
            }

            foreach (var vm in targets)
            {
                if (BulkFillLeftView != null) vm.LeftView = BulkFillLeftView;
                if (BulkFillCenterView != null) vm.CenterView = BulkFillCenterView;
                if (BulkFillRightView != null) vm.RightView = BulkFillRightView;
            }

            LogInfo($"Bulk-fill applied to {targets.Count} view(s).");
        }

        [RelayCommand]
        private void ResetSizeToSuggested()
        {
            IsSizeAutoSuggested = true;
            UpdateSuggestedCalloutSize();
        }

        [RelayCommand]
        private void CopyAllLogs()
        {
            if (Logs.Count == 0)
                return;

            var text = string.Join(System.Environment.NewLine, Logs.Select(l => l.ToString()));
            Clipboard.SetText(text);
        }

        [RelayCommand]
        private void ClearLogs() => Logs.Clear();

        private void OnPlacementFinished(int success, int failed, int skipped)
        {
            IsRunning = false;
            ProgressText = string.Empty;
            RunSuccessCount = success;
            RunFailedCount = failed;
            RunSkippedCount = skipped;

            LogInfo($"Placement complete. Placed: {success}, Failed: {failed}, Skipped: {skipped}");
            PlaceCalloutsCommand.NotifyCanExecuteChanged();
        }

        private void OnViewItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewItemViewModel.IsSelected))
            {
                UpdateSuggestedCalloutSize();
                UpdateSelectedCount();
            }
        }

        private void UpdateSelectedCount()
            => SelectedCount = Views.Count(v => v.IsSelected);

        private void UpdateSuggestedCalloutSize()
        {
            if (!IsSizeAutoSuggested)
                return;

            var views = Views.Where(v => v.IsSelected).Select(v => v.View).ToList();
            if (!views.Any())
                return;

            CalloutSize = CalloutSizeSuggestionService.GetSuggestedSizeMm(views);
        }

        private bool FilterViews(object obj)
        {
            if (obj is not ViewItemViewModel vm)
                return false;

            if (!SheetFilterText.Equals("ALL") &&
                !vm.SheetNumbers.Contains(SheetFilterText))
                return false;

            if (!ShowPlaced && vm.IsPlaced) return false;
            if (!ShowUnplaced && !vm.IsPlaced) return false;
            if (vm.ViewType == ViewType.Section && !ShowSections) return false;
            if (vm.ViewType == ViewType.Elevation && !ShowElevations) return false;

            return true;
        }

        private void LogInfo(string msg)
            => Logs.Add(new LogEntry(LogLevel.Info, msg));

        private void LogWarning(string msg)
            => Logs.Add(new LogEntry(LogLevel.Warning, msg));

        private void LogError(string msg)
            => Logs.Add(new LogEntry(LogLevel.Error, msg));
    }
}
