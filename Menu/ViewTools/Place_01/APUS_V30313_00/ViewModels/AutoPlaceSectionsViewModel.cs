using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.APUS_V313.Enums;
using Revit26_Plugin.APUS_V313.ExternalEvents;
using Revit26_Plugin.APUS_V313.Models;
using Revit26_Plugin.APUS_V313.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
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
        private ObservableCollection<SectionItemViewModel> _sections = new();
        public ObservableCollection<SectionItemViewModel> Sections
        {
            get => _sections;
            private set
            {
                _sections = value;
                OnPropertyChanged();
            }
        }

        public ICollectionView FilteredSections { get; private set; }

        private ObservableCollection<TitleBlockItemViewModel> _titleBlocks = new();
        public ObservableCollection<TitleBlockItemViewModel> TitleBlocks
        {
            get => _titleBlocks;
            private set
            {
                _titleBlocks = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<LogEntryViewModel> LogEntries { get; } = new();
        public PlacementProgressViewModel Progress { get; } = new();

        // ---------------- FILTER SOURCES ----------------
        public ObservableCollection<string> PlacementScopes { get; } = new();
        public ObservableCollection<string> SheetNumberOptions { get; } = new();
        public ObservableCollection<PlacementFilterState> PlacementStates { get; } = new()
        {
            PlacementFilterState.All,
            PlacementFilterState.PlacedOnly,
            PlacementFilterState.UnplacedOnly
        };

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

        private PlacementFilterState _selectedPlacementState = PlacementFilterState.All;
        public PlacementFilterState SelectedPlacementState
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
            set
            {
                _selectedTitleBlock = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // ===================== LAYOUT INPUTS (mm) =====================
        public double LeftMarginMm { get; set; } = 40;
        public double RightMarginMm { get; set; } = 150;
        public double TopMarginMm { get; set; } = 40;
        public double BottomMarginMm { get; set; } = 100;
        public double HorizontalGapMm { get; set; } = 10;
        public double VerticalGapMm { get; set; } = 10;
        public double YToleranceMm { get; set; } = 10;

        // ---------------- PLACEMENT AREA ----------------
        private SheetPlacementArea _placementArea;
        public SheetPlacementArea PlacementArea
        {
            get => _placementArea;
            private set
            {
                _placementArea = value;
                OnPropertyChanged();
            }
        }

        // ---------------- UI STATE ----------------
        private UiState _uiState = UiState.ReadyToPlace;
        public UiState UiState
        {
            get => _uiState;
            private set
            {
                if (_uiState == value) return;
                _uiState = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsPlacing));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // Keep for backward compatibility with XAML
        public bool IsPlacing => UiState == UiState.Placing;

        // ---------------- COMMANDS ----------------
        public IRelayCommand PlaceCommand { get; }
        public IRelayCommand CancelCommand { get; }
        public IRelayCommand DebugCommand { get; }
        public IRelayCommand SelectAllCommand { get; }
        public IRelayCommand SelectNoneCommand { get; }
        public IRelayCommand SelectUnplacedCommand { get; }

        // ---------------- CTOR ----------------
        public AutoPlaceSectionsViewModel(UIDocument uidoc)
        {
            _uidoc = uidoc;

            // Initialize FilteredSections
            FilteredSections = CollectionViewSource.GetDefaultView(Sections);
            FilteredSections.Filter = FilterPredicate;

            // Initialize commands - TEMPORARILY MAKE PLACE COMMAND ALWAYS ENABLED
            PlaceCommand = new RelayCommand(ExecutePlacement, () => true); // Always enabled
            CancelCommand = new RelayCommand(() =>
            {
                Progress.State = ProgressState.Cancelled;
                UiState = UiState.Cancelled;
            });
            DebugCommand = new RelayCommand(CheckPlacementState);
            SelectAllCommand = new RelayCommand(SelectAllSections);
            SelectNoneCommand = new RelayCommand(SelectNoneSections);
            SelectUnplacedCommand = new RelayCommand(SelectUnplacedSections);

            // Initialize filter options
            PlacementScopes.Add(ALL);
            SheetNumberOptions.Add(ALL);

            // Start as ReadyToPlace
            UiState = UiState.ReadyToPlace;

            // Subscribe to property changes to refresh commands
            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(UiState) ||
                    e.PropertyName == nameof(SelectedTitleBlock) ||
                    e.PropertyName == nameof(Sections))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            };
        }

        // ---------------- ASYNC DATA LOADING ----------------
        public async Task LoadDataAsync()
        {
            // Don't block UI during loading
            LogInfo("Loading sections and title blocks...");

            try
            {
                // Clear existing data first
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Sections.Clear();
                    TitleBlocks.Clear();
                    PlacementScopes.Clear();
                    SheetNumberOptions.Clear();

                    PlacementScopes.Add(ALL);
                    SheetNumberOptions.Add(ALL);
                });

                // Load sections
                var sectionsTask = Task.Run(() =>
                {
                    var items = new SectionCollectionService(_uidoc.Document).Collect();
                    Application.Current.Dispatcher.Invoke(() =>
                    {
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

                        // AUTO-SELECT UNPLACED SECTIONS BY DEFAULT (Best for Revit)
                        SelectUnplacedSections();
                    });
                });

                // Load title blocks
                var titleBlocksTask = Task.Run(() =>
                {
                    var items = new TitleBlockCollectionService(_uidoc.Document).Collect();
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var tb in items) TitleBlocks.Add(tb);
                        SelectedTitleBlock = TitleBlocks.FirstOrDefault();
                    });
                });

                // Wait for both to complete
                await Task.WhenAll(sectionsTask, titleBlocksTask);

                // Refresh FilteredSections
                Application.Current.Dispatcher.Invoke(() =>
                {
                    FilteredSections.Refresh();
                    OnPropertyChanged(nameof(FilteredSections));
                });

                var selectedCount = Sections.Count(s => s.IsSelected);
                var unplacedCount = Sections.Count(s => !s.IsPlaced);
                LogInfo($"Loaded {Sections.Count} sections ({unplacedCount} unplaced).");
                LogInfo($"Auto-selected {selectedCount} unplaced sections for placement.");

                // Check if we can place
                CheckPlacementState();
            }
            catch (Exception ex)
            {
                LogError($"Failed to load data: {ex.Message}");
                UiState = UiState.Error;
            }
        }

        // ---------------- SELECTION COMMANDS ----------------
        private void SelectAllSections()
        {
            foreach (var section in Sections)
            {
                section.IsSelected = true;
            }
            FilteredSections.Refresh();
            CommandManager.InvalidateRequerySuggested();
            LogInfo($"Selected all {Sections.Count} sections.");
        }

        private void SelectNoneSections()
        {
            foreach (var section in Sections)
            {
                section.IsSelected = false;
            }
            FilteredSections.Refresh();
            CommandManager.InvalidateRequerySuggested();
            LogInfo($"Cleared all section selections.");
        }

        private void SelectUnplacedSections()
        {
            int selectedCount = 0;
            foreach (var section in Sections)
            {
                // Select only unplaced sections by default
                section.IsSelected = !section.IsPlaced;
                if (section.IsSelected) selectedCount++;
            }
            FilteredSections.Refresh();
            CommandManager.InvalidateRequerySuggested();
            LogInfo($"Selected {selectedCount} unplaced sections.");
        }

        // ---------------- FILTER ----------------
        private bool FilterPredicate(object obj)
        {
            if (obj is not SectionItemViewModel item) return false;

            // For now, accept all items to debug
            return true;

            /* Original filter logic - comment out for debugging
            if (SelectedPlacementState != PlacementFilterState.All)
            {
                if (SelectedPlacementState == PlacementFilterState.PlacedOnly && !item.IsPlaced) return false;
                if (SelectedPlacementState == PlacementFilterState.UnplacedOnly && item.IsPlaced) return false;
            }

            if (SelectedPlacementScope != ALL &&
                !string.Equals(item.PlacementScope, SelectedPlacementScope,
                    StringComparison.OrdinalIgnoreCase))
                return false;

            if (SheetNumberFilter != ALL &&
                !string.IsNullOrWhiteSpace(item.SheetNumber) &&
                !string.Equals(item.SheetNumber, SheetNumberFilter,
                    StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
            */
        }

        private void OnFilterChanged()
        {
            FilteredSections.Refresh();
            CommandManager.InvalidateRequerySuggested();
        }

        // ---------------- PLACEMENT ----------------
        private void ExecutePlacement()
        {
            var toPlace = FilteredSections
                .Cast<SectionItemViewModel>()
                .Where(x => x.IsSelected)
                .ToList();

            // Fallback to all sections if filtered is empty
            if (!toPlace.Any())
            {
                toPlace = Sections.Where(x => x.IsSelected).ToList();
            }

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

            // Set UI state
            UiState = UiState.Placing;
            Progress.Reset(toPlace.Count);

            // Clear previous logs and show starting message
            Application.Current.Dispatcher.Invoke(() =>
            {
                LogEntries.Clear();
                LogInfo($"Starting placement of {toPlace.Count} selected sections using Shelf/Row method...");
            });

            // Calculate placement area
            PlacementArea = SheetLayoutService.Calculate(
                _uidoc.Document,
                SelectedTitleBlock.FamilySymbol,
                LeftMarginMm,
                RightMarginMm,
                TopMarginMm,
                BottomMarginMm);

            if (PlacementArea == null)
            {
                LogError("Failed to calculate placement area.");
                UiState = UiState.Error;
                return;
            }

            // Sort sections in human reading order
            var sortedSections = SectionReadingOrderSortingService.SortInReadingOrder(
                toPlace,
                YToleranceMm);

            LogInfo($"Sorted {sortedSections.Count} sections in human reading order.");

            // Setup handler and raise event
            AutoPlaceSectionsEventManager.Handler.ViewModel = this;
            AutoPlaceSectionsEventManager.Handler.SortedSections = sortedSections;
            AutoPlaceSectionsEventManager.Handler.TitleBlock = SelectedTitleBlock.FamilySymbol;
            AutoPlaceSectionsEventManager.Handler.PlacementArea = PlacementArea;
            AutoPlaceSectionsEventManager.Handler.HorizontalGapMm = HorizontalGapMm;
            AutoPlaceSectionsEventManager.Handler.VerticalGapMm = VerticalGapMm;

            AutoPlaceSectionsEventManager.ExternalEvent.Raise();
        }

        // ---------------- DEBUG ----------------
        public void CheckPlacementState()
        {
            LogInfo("=== PLACEMENT STATE DEBUG ===");
            LogInfo($"UiState: {UiState}");
            LogInfo($"SelectedTitleBlock: {(SelectedTitleBlock != null ? SelectedTitleBlock.DisplayName : "NULL")}");

            var totalSections = Sections.Count;
            var filteredCount = 0;
            var selectedCount = 0;
            var unplacedCount = Sections.Count(s => !s.IsPlaced);
            var placedCount = Sections.Count(s => s.IsPlaced);

            try
            {
                filteredCount = FilteredSections.Cast<SectionItemViewModel>().Count();
                selectedCount = FilteredSections.Cast<SectionItemViewModel>().Count(x => x.IsSelected);
            }
            catch
            {
                filteredCount = Sections.Count;
                selectedCount = Sections.Count(x => x.IsSelected);
            }

            LogInfo($"Total sections: {totalSections}");
            LogInfo($"Unplaced sections: {unplacedCount}");
            LogInfo($"Placed sections: {placedCount}");
            LogInfo($"Filtered sections: {filteredCount}");
            LogInfo($"Selected sections: {selectedCount}");

            if (selectedCount == 0 && unplacedCount > 0)
            {
                LogInfo("⚠️ No sections selected! Click 'Select Unplaced' or check checkboxes.");
            }

            LogInfo("=== END DEBUG ===");

            CommandManager.InvalidateRequerySuggested();
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
            UiState = Progress.State == ProgressState.Cancelled ? UiState.Cancelled : UiState.Completed;
            LogInfo("Placement operation completed.");
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}