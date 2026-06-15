using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using Revit26_Plugin.CalloutCOP_V06.ExternalEvents;
using Revit26_Plugin.CalloutCOP_V06.Models;
using Revit26_Plugin.CalloutCOP_V06.Services;

namespace Revit26_Plugin.CalloutCOP_V06.ViewModels
{
    public partial class CalloutCOPViewModel : ObservableObject
    {
        public ObservableCollection<ViewItemViewModel> Views { get; }
        public ICollectionView ViewsCollection { get; }

        public ObservableCollection<ViewDrafting> DraftingViews { get; }
        public ObservableCollection<string> SheetFilterItems { get; }

        public ObservableCollection<CopLogEntry> Logs { get; } = new();

        private readonly ExternalEvent _externalEvent;
        private readonly CalloutPlacementExternalEvent _handler;

        [ObservableProperty] private string _sheetFilterText = "ALL";
        [ObservableProperty] private bool _showPlaced = true;
        [ObservableProperty] private bool _showUnplaced = true;
        [ObservableProperty] private bool _showSections = true;
        [ObservableProperty] private bool _showElevations = true;

        [ObservableProperty] private ViewDrafting _selectedDraftingView;

        [ObservableProperty] private double _calloutSize;
        [ObservableProperty] private bool _isSizeAutoSuggested = true;

        // STEP-10 execution state
        [ObservableProperty] private bool _isRunning;
        [ObservableProperty] private string _progressText = string.Empty;

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
                () => SelectedDraftingView,
                () => CalloutSize,
                OnPlacementFinished);

            _externalEvent = ExternalEvent.Create(_handler);

            UpdateSuggestedCalloutSize();
            LogInfo("Callout COP V06 initialized.");
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
            if (SelectedDraftingView == null)
            {
                LogWarning("Select a Drafting View first.");
                return;
            }

            var count = Views.Count(v => v.IsSelected);
            if (count == 0)
            {
                LogWarning("No target views selected.");
                return;
            }

            IsRunning = true;
            ProgressText = $"Running… {count} view(s)";
            _externalEvent.Raise();
        }

        private void OnPlacementFinished(int success, int failed)
        {
            IsRunning = false;
            ProgressText = string.Empty;

            LogInfo($"Placement complete. Success: {success}, Failed: {failed}");
            PlaceCalloutsCommand.NotifyCanExecuteChanged();
        }

        private void OnViewItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewItemViewModel.IsSelected))
                UpdateSuggestedCalloutSize();
        }

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
            => Logs.Add(new CopLogEntry(CopLogLevel.Info, msg));

        private void LogWarning(string msg)
            => Logs.Add(new CopLogEntry(CopLogLevel.Warning, msg));

        private void LogError(string msg)
            => Logs.Add(new CopLogEntry(CopLogLevel.Error, msg));
    }
}
