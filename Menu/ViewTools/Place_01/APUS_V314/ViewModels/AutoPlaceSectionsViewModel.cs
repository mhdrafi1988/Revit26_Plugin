// File: AutoPlaceSectionsViewModel.cs
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.APUS_V314.ExternalEvents;
using Revit26_Plugin.APUS_V314.Models;
using Revit26_Plugin.APUS_V314.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace Revit26_Plugin.APUS_V314.ViewModels
{
    public class AutoPlaceSectionsViewModel : BaseViewModel
    {
        private const string ALL = "All";
        private readonly UIDocument _uidoc;
        private PluginState _currentState = PluginState.Idle;
        private string _statusMessage = "Ready";

        // ---------------- STATE MANAGEMENT ----------------
        public PluginState CurrentState
        {
            get => _currentState;
            set
            {
                if (SetField(ref _currentState, value))
                {
                    OnPropertyChanged(nameof(IsUiEnabled));
                    OnPropertyChanged(nameof(IsProcessing));
                    OnPropertyChanged(nameof(ShowProgress));
                    OnPropertyChanged(nameof(ShowCancelButton));
                    OnPropertyChanged(nameof(CanPlace));
                    OnPropertyChanged(nameof(CanRefresh));
                    CommandManager.InvalidateRequerySuggested();
                    UpdateStatusMessage();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetField(ref _statusMessage, value);
        }

        // UI State Properties - ALL ENABLED
        public bool IsUiEnabled => true; // ALWAYS TRUE

        public bool IsProcessing => CurrentState == PluginState.Processing ||
                                   CurrentState == PluginState.Cancelling;

        public bool ShowProgress => IsProcessing;
        public bool ShowCancelButton => IsProcessing;
        public bool CanPlace => true; // ALWAYS TRUE
        public bool CanRefresh => true; // ALWAYS TRUE

        // ---------------- DATA COLLECTIONS ----------------
        public ObservableCollection<SectionItemViewModel> Sections { get; } = new();
        public ICollectionView FilteredSections { get; }
        public ObservableCollection<TitleBlockItemViewModel> TitleBlocks { get; } = new();
        public ObservableCollection<LogEntryViewModel> LogEntries { get; } = new();
        public PlacementProgressViewModel Progress { get; } = new PlacementProgressViewModel();

        // ---------------- FILTER SOURCES ----------------
        public ObservableCollection<string> PlacementScopes { get; } = new();
        public ObservableCollection<string> SheetNumberOptions { get; } = new();
        public ObservableCollection<string> PlacementStates { get; } =
            new() { ALL, "Unplaced Only", "Placed Only" };

        // ---------------- FILTER VALUES ----------------
        private string _selectedPlacementScope = ALL;
        public string SelectedPlacementScope
        {
            get => _selectedPlacementScope;
            set { SetField(ref _selectedPlacementScope, value); OnFilterChanged(); }
        }

        private string _sheetNumberFilter = ALL;
        public string SheetNumberFilter
        {
            get => _sheetNumberFilter;
            set { SetField(ref _sheetNumberFilter, value); OnFilterChanged(); }
        }

        private string _selectedPlacementState = ALL;
        public string SelectedPlacementState
        {
            get => _selectedPlacementState;
            set { SetField(ref _selectedPlacementState, value); OnFilterChanged(); }
        }

        // ---------------- UI OPTIONS ----------------
        private bool _openSheetsAfterPlacement = true;
        public bool OpenSheetsAfterPlacement
        {
            get => _openSheetsAfterPlacement;
            set => SetField(ref _openSheetsAfterPlacement, value);
        }

        private bool _maintainAspectRatio = true;
        public bool MaintainAspectRatio
        {
            get => _maintainAspectRatio;
            set => SetField(ref _maintainAspectRatio, value);
        }

        private bool _skipPlacedViews = true;
        public bool SkipPlacedViews
        {
            get => _skipPlacedViews;
            set => SetField(ref _skipPlacedViews, value);
        }

        // ---------------- TITLE BLOCK ----------------
        private TitleBlockItemViewModel _selectedTitleBlock;
        public TitleBlockItemViewModel SelectedTitleBlock
        {
            get => _selectedTitleBlock;
            set => SetField(ref _selectedTitleBlock, value);
        }

        // ---------------- PLACEMENT ALGORITHM ----------------
        private PlacementAlgorithm _selectedAlgorithm = PlacementAlgorithm.Grid;
        public PlacementAlgorithm SelectedAlgorithm
        {
            get => _selectedAlgorithm;
            set => SetField(ref _selectedAlgorithm, value);
        }

        public ObservableCollection<PlacementAlgorithm> AvailableAlgorithms { get; } =
            new ObservableCollection<PlacementAlgorithm>
            {
                PlacementAlgorithm.Grid,
                PlacementAlgorithm.BinPacking,
                PlacementAlgorithm.Ordered,
                PlacementAlgorithm.AdaptiveGrid
            };

        // ===================== LAYOUT SETTINGS (mm) =====================
        public double LeftMarginMm { get; set; } = 40;
        public double RightMarginMm { get; set; } = 150;
        public double TopMarginMm { get; set; } = 40;
        public double BottomMarginMm { get; set; } = 100;
        public double HorizontalGapMm { get; set; } = 10;
        public double VerticalGapMm { get; set; } = 10;
        public double YToleranceMm { get; set; } = 10;

        // ---------------- CALCULATED PROPERTIES ----------------
        public int SelectedSectionsCount => FilteredSections?.Cast<SectionItemViewModel>().Count(x => x.IsSelected) ?? 0;
        public int EstimatedSheets => CalculateEstimatedSheets();
        public string EstimatedTime => CalculateEstimatedTime();

        // ---------------- COMMANDS ----------------
        public IRelayCommand PlaceCommand { get; }
        public IRelayCommand CancelCommand { get; }
        public IRelayCommand RefreshCommand { get; }
        public IRelayCommand SelectAllCommand { get; }
        public IRelayCommand ClearSelectionCommand { get; }
        public IRelayCommand ClearLogCommand { get; }
        public IRelayCommand ExportLogCommand { get; }

        // ---------------- CONSTRUCTOR ----------------
        public AutoPlaceSectionsViewModel(UIDocument uidoc)
        {
            _uidoc = uidoc ?? throw new ArgumentNullException(nameof(uidoc));

            FilteredSections = CollectionViewSource.GetDefaultView(Sections);
            FilteredSections.Filter = FilterPredicate;

            // Initialize commands - NO VALIDATION
            PlaceCommand = new RelayCommand(ExecutePlacement); // REMOVED: () => CanPlace
            CancelCommand = new RelayCommand(ExecuteCancel); // REMOVED: () => ShowCancelButton
            RefreshCommand = new RelayCommand(ExecuteRefresh); // REMOVED: () => CanRefresh
            SelectAllCommand = new RelayCommand(ExecuteSelectAll); // REMOVED: () => IsUiEnabled
            ClearSelectionCommand = new RelayCommand(ExecuteClearSelection); // REMOVED: () => IsUiEnabled
            ClearLogCommand = new RelayCommand(ExecuteClearLog);
            ExportLogCommand = new RelayCommand(ExecuteExportLog);

            // Set initial state to Idle
            CurrentState = PluginState.Idle;
        }

        // ---------------- INITIALIZATION ----------------
        public void InitializeData()
        {
            CurrentState = PluginState.Initializing;
            LogInfo("Initializing APUS V314...");

            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    CollectSections();
                    CollectTitleBlocks();
                });

                CurrentState = PluginState.ReadyToPlace;
                LogInfo($"Ready: {Sections.Count} sections, {TitleBlocks.Count} title blocks loaded.");
            }
            catch (Exception ex)
            {
                CurrentState = PluginState.Error;
                LogError($"Initialization failed: {ex.Message}");
            }
        }

        private void UpdateStatusMessage()
        {
            StatusMessage = CurrentState switch
            {
                PluginState.Idle => "Ready to initialize",
                PluginState.Initializing => "Loading data...",
                PluginState.ReadyToPlace => $"Ready to place ({SelectedSectionsCount} selected)",
                PluginState.Processing => "Placing sections...",
                PluginState.Cancelling => "Cancelling...",
                PluginState.Completed => "Placement completed",
                PluginState.Error => "Error occurred",
                _ => "Unknown state"
            };
        }

        // ---------------- DATA COLLECTION ----------------
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

            OnPropertyChanged(nameof(SelectedSectionsCount));
        }

        private void CollectTitleBlocks()
        {
            var items = new TitleBlockCollectionService(_uidoc.Document).Collect();
            TitleBlocks.Clear();
            foreach (var tb in items) TitleBlocks.Add(tb);
            SelectedTitleBlock = TitleBlocks.FirstOrDefault();
        }

        // ---------------- FILTERING ----------------
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
            OnPropertyChanged(nameof(SelectedSectionsCount));
            UpdateReadyState();
        }

        private void UpdateReadyState()
        {
            if (CurrentState == PluginState.Idle && SelectedSectionsCount > 0)
                CurrentState = PluginState.ReadyToPlace;
        }

        // ---------------- CALCULATIONS ----------------
        private int CalculateEstimatedSheets()
        {
            if (SelectedSectionsCount == 0) return 0;

            // Simple estimation: 8-12 views per sheet depending on algorithm
            int viewsPerSheet = SelectedAlgorithm switch
            {
                PlacementAlgorithm.Grid => 8,
                PlacementAlgorithm.BinPacking => 12,
                PlacementAlgorithm.Ordered => 10,
                PlacementAlgorithm.AdaptiveGrid => 11,
                _ => 10
            };

            return (int)Math.Ceiling(SelectedSectionsCount / (double)viewsPerSheet);
        }

        private string CalculateEstimatedTime()
        {
            if (SelectedSectionsCount == 0) return "0";

            // Estimation: 2-5 seconds per view depending on complexity
            double secondsPerView = SelectedAlgorithm switch
            {
                PlacementAlgorithm.Grid => 2.0,
                PlacementAlgorithm.BinPacking => 3.5,
                PlacementAlgorithm.Ordered => 2.5,
                PlacementAlgorithm.AdaptiveGrid => 3.0,
                _ => 2.5
            };

            int totalSeconds = (int)(SelectedSectionsCount * secondsPerView);
            return totalSeconds.ToString();
        }

        // ---------------- COMMAND EXECUTION ----------------
        private void ExecutePlacement()
        {
            var toPlace = FilteredSections
                .Cast<SectionItemViewModel>()
                .Where(x => x.IsSelected && (!SkipPlacedViews || !x.IsPlaced))
                .ToList();

            if (!toPlace.Any())
            {
                LogWarning("No sections selected for placement.");
                return;
            }

            // Update state
            CurrentState = PluginState.Processing;
            Progress.Reset(toPlace.Count);

            // Clear previous logs and show starting message
            Application.Current.Dispatcher.Invoke(() =>
            {
                LogEntries.Clear();
                LogInfo($"Starting {SelectedAlgorithm} placement of {toPlace.Count} sections...");
                LogInfo($"Estimated: {EstimatedSheets} sheets, {EstimatedTime} seconds");
            });

            // Configure handler based on selected algorithm
            var handler = new AutoPlaceSectionsHandler
            {
                ViewModel = this,
                SectionsToPlace = toPlace,
                Algorithm = SelectedAlgorithm
            };

            AutoPlaceSectionsEventManager.Handler = handler;
            AutoPlaceSectionsEventManager.ExternalEvent.Raise();
        }

        private void ExecuteCancel()
        {
            CurrentState = PluginState.Cancelling;
            Progress.Cancel();
            LogWarning("Cancellation requested...");
        }

        private void ExecuteRefresh()
        {
            InitializeData();
        }

        private void ExecuteSelectAll()
        {
            foreach (var item in FilteredSections.Cast<SectionItemViewModel>())
            {
                item.IsSelected = true;
            }
            OnPropertyChanged(nameof(SelectedSectionsCount));
        }

        private void ExecuteClearSelection()
        {
            foreach (var item in FilteredSections.Cast<SectionItemViewModel>())
            {
                item.IsSelected = false;
            }
            OnPropertyChanged(nameof(SelectedSectionsCount));
        }

        private void ExecuteClearLog()
        {
            LogEntries.Clear();
            LogInfo("Log cleared");
        }

        private void ExecuteExportLog()
        {
            // Simple export implementation
            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                    FileName = $"APUS_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var lines = LogEntries.Select(entry => $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss} [{entry.Level}] {entry.Message}");
                    System.IO.File.WriteAllLines(saveDialog.FileName, lines);
                    LogInfo($"Log exported to: {saveDialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to export log: {ex.Message}");
            }
        }

        // ---------------- LOGGING ----------------
        public void LogInfo(string msg) => AddLog(LogLevel.Info, msg);
        public void LogWarning(string msg) => AddLog(LogLevel.Warning, msg);
        public void LogError(string msg) => AddLog(LogLevel.Error, msg);
        public void LogSuccess(string msg) => AddLog(LogLevel.Success, msg);

        private void AddLog(LogLevel level, string msg)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var entry = new LogEntryViewModel(level, msg);
                LogEntries.Add(entry);

                // Auto-scroll and limit log size
                if (LogEntries.Count > 500)
                {
                    LogEntries.RemoveAt(0);
                }
            });
        }

        // ---------------- PLACEMENT COMPLETION ----------------
        public void OnPlacementComplete(bool success, string message = "")
        {
            if (CurrentState == PluginState.Cancelling)
            {
                CurrentState = PluginState.Idle;
                LogInfo("Placement cancelled by user.");
            }
            else if (success)
            {
                CurrentState = PluginState.Completed;
                LogSuccess($"Placement completed successfully! {message}");

                // Auto-refresh after successful placement
                Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(1000);
                    if (CurrentState == PluginState.Completed)
                    {
                        ExecuteRefresh();
                    }
                });
            }
            else
            {
                CurrentState = PluginState.Error;
                LogError($"Placement failed: {message}");
            }
        }

        public void OnWindowClosing()
        {
            // Clean up any resources if needed
            if (IsProcessing)
            {
                Progress.Cancel();
            }
        }
    }
}