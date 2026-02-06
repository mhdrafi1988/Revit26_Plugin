using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.APUS_V313.ExternalEvents;
using Revit26_Plugin.APUS_V313.Models;
using Revit26_Plugin.APUS_V313.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace Revit26_Plugin.APUS_V313.ViewModels
{
    public class AutoPlaceSectionsViewModel : INotifyPropertyChanged
    {
        private const string ALL = "All";
        private readonly UIDocument _uidoc;

        // ---------------- DATA ----------------
        public ObservableCollection<SectionItemViewModel> Sections { get; } = new();
        public ICollectionView FilteredSections { get; }

        public ObservableCollection<TitleBlockItemViewModel> TitleBlocks { get; } = new();
        public ObservableCollection<LogEntryViewModel> LogEntries { get; } = new();
        public PlacementProgressViewModel Progress { get; } = new();

        // ---------------- FILTER SOURCES ----------------
        public ObservableCollection<string> PlacementScopes { get; } = new();
        public ObservableCollection<string> SheetNumberOptions { get; } = new();
        public ObservableCollection<string> PlacementStates { get; } = new() { ALL, "Unplaced Only", "Placed Only" };

        // ---------------- FILTER VALUES ----------------
        private string _selectedPlacementScope = ALL;
        public string SelectedPlacementScope
        {
            get => _selectedPlacementScope;
            set { _selectedPlacementScope = value; OnFilterChanged(); }
        }

        private string _sheetNumberFilter = ALL;
        public string SheetNumberFilter
        {
            get => _sheetNumberFilter;
            set { _sheetNumberFilter = value; OnFilterChanged(); }
        }

        private string _selectedPlacementState = ALL;
        public string SelectedPlacementState
        {
            get => _selectedPlacementState;
            set { _selectedPlacementState = value; OnFilterChanged(); }
        }

        // ---------------- UX OPTION ----------------
        private bool _openSheetsAfterPlacement = true;
        public bool OpenSheetsAfterPlacement
        {
            get => _openSheetsAfterPlacement;
            set { _openSheetsAfterPlacement = value; OnPropertyChanged(); }
        }

        // ---------------- TITLE BLOCK ----------------
        private TitleBlockItemViewModel _selectedTitleBlock;
        public TitleBlockItemViewModel SelectedTitleBlock
        {
            get => _selectedTitleBlock;
            set { _selectedTitleBlock = value; OnPropertyChanged(); }
        }

        // ===================== LAYOUT INPUTS (mm) =====================
        public double LeftMarginMm { get; set; } = 40;
        public double RightMarginMm { get; set; } = 150;
        public double TopMarginMm { get; set; } = 40;
        public double BottomMarginMm { get; set; } = 100;
        public double HorizontalGapMm { get; set; } = 10;
        public double VerticalGapMm { get; set; } = 10;
        public double YToleranceMm { get; set; } = 10;

        // ---------------- COMMANDS ----------------
        public IRelayCommand PlaceCommand { get; }
        public IRelayCommand CancelCommand { get; }

        // ---------------- PLACEMENT STATE ----------------
        private bool _isPlacing;
        public bool IsPlacing
        {
            get => _isPlacing;
            set
            {
                _isPlacing = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // ---------------- CTOR ----------------
        public AutoPlaceSectionsViewModel(UIDocument uidoc)
        {
            _uidoc = uidoc;

            FilteredSections = CollectionViewSource.GetDefaultView(Sections);
            FilteredSections.Filter = FilterPredicate;

            PlaceCommand = new RelayCommand(ExecutePlacement, CanExecutePlacement);
            CancelCommand = new RelayCommand(() => Progress.Cancel());

            CollectSections();
            CollectTitleBlocks();
        }

        // ---------------- COLLECTION ----------------
        private void CollectSections()
        {
            var items = new SectionCollectionService(_uidoc.Document).Collect();

            Sections.Clear();
            PlacementScopes.Clear();
            SheetNumberOptions.Clear();

            PlacementScopes.Add(ALL);
            SheetNumberOptions.Add(ALL);

            foreach (var item in items)
            {
                Sections.Add(item);

                if (!string.IsNullOrWhiteSpace(item.PlacementScope) &&
                    !PlacementScopes.Contains(item.PlacementScope))
                    PlacementScopes.Add(item.PlacementScope);

                if (item.IsPlaced &&
                    !string.IsNullOrWhiteSpace(item.SheetNumber) &&
                    !SheetNumberOptions.Contains(item.SheetNumber))
                    SheetNumberOptions.Add(item.SheetNumber);
            }
        }

        private void CollectTitleBlocks()
        {
            var items = new TitleBlockCollectionService(_uidoc.Document).Collect();
            TitleBlocks.Clear();
            foreach (var tb in items) TitleBlocks.Add(tb);
            SelectedTitleBlock = TitleBlocks.FirstOrDefault();
        }

        // ---------------- FILTER ----------------
        private bool FilterPredicate(object obj)
        {
            if (obj is not SectionItemViewModel item) return false;

            if (SelectedPlacementState != ALL)
            {
                if (SelectedPlacementState == "Placed Only" && !item.IsPlaced) return false;
                if (SelectedPlacementState == "Unplaced Only" && item.IsPlaced) return false;
            }

            if (SelectedPlacementScope != ALL &&
                !string.Equals(item.PlacementScope, SelectedPlacementScope,
                    StringComparison.OrdinalIgnoreCase))
                return false;

            if (SheetNumberFilter != ALL &&
                !string.Equals(item.SheetNumber, SheetNumberFilter,
                    StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        private void OnFilterChanged()
        {
            FilteredSections.Refresh();
        }

        // ---------------- PLACEMENT ----------------
        private bool CanExecutePlacement()
        {
            return !IsPlacing &&
                   FilteredSections.Cast<SectionItemViewModel>().Any(x => x.IsSelected) &&
                   SelectedTitleBlock != null;
        }

        private void ExecutePlacement()
        {
            var toPlace = FilteredSections
                .Cast<SectionItemViewModel>()
                .Where(x => x.IsSelected)
                .ToList();

            if (!toPlace.Any())
            {
                LogWarning("No sections selected for placement.");
                return;
            }

            if (SelectedTitleBlock == null)
            {
                LogWarning("Please select a title block.");
                return;
            }

            // Set placing state
            IsPlacing = true;
            Progress.Reset(toPlace.Count);

            // Clear previous logs and show starting message
            Application.Current.Dispatcher.Invoke(() =>
            {
                LogEntries.Clear();
                LogInfo($"Starting placement of {toPlace.Count} selected sections using Shelf/Row method...");
            });

            // Calculate placement area
            var placementArea = SheetLayoutService.Calculate(
                _uidoc.Document,
                SelectedTitleBlock.FamilySymbol,
                LeftMarginMm,
                RightMarginMm,
                TopMarginMm,
                BottomMarginMm);

            if (placementArea == null)
            {
                LogError("Failed to calculate placement area.");
                IsPlacing = false;
                return;
            }

            // Sort sections in human reading order (ONE TIME ONLY)
            var sortedSections = SectionReadingOrderSortingService.SortInReadingOrder(
                toPlace,
                YToleranceMm);

            LogInfo($"Sorted {sortedSections.Count} sections in human reading order.");

            // Setup handler and raise event
            AutoPlaceSectionsEventManager.Handler.ViewModel = this;
            AutoPlaceSectionsEventManager.Handler.SortedSections = sortedSections;
            AutoPlaceSectionsEventManager.Handler.TitleBlock = SelectedTitleBlock.FamilySymbol;
            AutoPlaceSectionsEventManager.Handler.PlacementArea = placementArea;
            AutoPlaceSectionsEventManager.Handler.HorizontalGapMm = HorizontalGapMm;
            AutoPlaceSectionsEventManager.Handler.VerticalGapMm = VerticalGapMm;

            AutoPlaceSectionsEventManager.ExternalEvent.Raise();
        }

        // ---------------- LOGGING ----------------
        public void LogInfo(string msg) => AddLog(LogLevel.Info, msg);
        public void LogWarning(string msg) => AddLog(LogLevel.Warning, msg);
        public void LogError(string msg) => AddLog(LogLevel.Error, msg);

        private void AddLog(LogLevel level, string msg)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var entry = new LogEntryViewModel(level, msg);
                LogEntries.Add(entry);
            });
        }

        // Call this method when placement is complete
        public void OnPlacementComplete()
        {
            IsPlacing = false;
            LogInfo("Placement operation completed.");
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}