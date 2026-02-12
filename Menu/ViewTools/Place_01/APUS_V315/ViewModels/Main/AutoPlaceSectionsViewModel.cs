using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.APUS_V315.ExternalEvents;
using Revit26_Plugin.APUS_V315.Models.Enums;
using Revit26_Plugin.APUS_V315.Models.Requests;
using Revit26_Plugin.APUS_V315.Services.Abstractions;
using Revit26_Plugin.APUS_V315.ViewModels.Items;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Revit26_Plugin.APUS_V315.ViewModels.Main;

public sealed partial class AutoPlaceSectionsViewModel : ObservableObject, IDisposable
{
    private const string ALL = "All";

    private readonly UIDocument _uidoc;
    private readonly ILogService _logService;
    private readonly IDialogService _dialogService;
    private readonly IPlacementOrchestrator _orchestrator;
    private readonly ISectionCollector _sectionCollector;
    private readonly ITitleBlockCollector _titleBlockCollector;
    private readonly ExternalEvent _placementEvent;
    private readonly SectionPlacementHandler _handler;

    // ---------- Observable Properties ----------
    [ObservableProperty]
    private PluginState _currentState = PluginState.Idle;

    [ObservableProperty]
    private TitleBlockItemViewModel? _selectedTitleBlock;

    [ObservableProperty]
    private PlacementAlgorithm _selectedAlgorithm = PlacementAlgorithm.Grid;

    [ObservableProperty]
    private string _selectedPlacementScope = ALL;

    [ObservableProperty]
    private string _sheetNumberFilter = ALL;

    [ObservableProperty]
    private string _selectedPlacementState = ALL;

    [ObservableProperty]
    private double _leftMarginMm = 40;

    [ObservableProperty]
    private double _rightMarginMm = 150;

    [ObservableProperty]
    private double _topMarginMm = 40;

    [ObservableProperty]
    private double _bottomMarginMm = 100;

    [ObservableProperty]
    private double _horizontalGapMm = 10;

    [ObservableProperty]
    private double _verticalGapMm = 10;

    [ObservableProperty]
    private double _yToleranceMm = 10;

    [ObservableProperty]
    private bool _openSheetsAfterPlacement = true;

    [ObservableProperty]
    private bool _skipPlacedViews = true;

    [ObservableProperty]
    private bool _isLogExpanded = true;

    // ---------- Estimated Properties ----------
    [ObservableProperty]
    private int _estimatedSheets;

    [ObservableProperty]
    private string _estimatedTime = "0s";

    // ---------- Collections ----------
    public ObservableCollection<SectionItemViewModel> Sections { get; } = new();
    public ObservableCollection<TitleBlockItemViewModel> TitleBlocks { get; } = new();
    public ObservableCollection<LogEntryViewModel> LogEntries { get; } = new();
    public ObservableCollection<string> PlacementScopes { get; } = new();
    public ObservableCollection<string> SheetNumberOptions { get; } = new();
    public ObservableCollection<string> PlacementStates { get; } = new() { ALL, "Unplaced Only", "Placed Only" };
    public ObservableCollection<PlacementAlgorithm> AvailableAlgorithms { get; } = new();
    public PlacementProgressViewModel Progress { get; } = new();

    // ---------- Filtered Collections ----------
    public ICollectionView FilteredSections { get; }

    // ---------- Calculated Properties ----------
    public int SelectedSectionsCount => FilteredSections?.Cast<SectionItemViewModel>().Count(x => x.IsSelected) ?? 0;
    public bool IsProcessing => CurrentState == PluginState.Processing || CurrentState == PluginState.Cancelling;
    public bool IsUiEnabled => !IsProcessing;
    public bool ShowProgress => IsProcessing;
    public bool ShowCancelButton => CurrentState == PluginState.Processing;
    public bool CanPlace => !IsProcessing && SelectedSectionsCount > 0 && SelectedTitleBlock != null;
    public bool CanRefresh => !IsProcessing;

    // ---------- Commands ----------
    public IAsyncRelayCommand PlaceCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand SelectAllCommand { get; }
    public IRelayCommand ClearSelectionCommand { get; }
    public IRelayCommand ClearLogCommand { get; }
    public IRelayCommand ExportLogCommand { get; }
    public IRelayCommand CopyLogsCommand { get; }

    // ---------- Constructor ----------
    public AutoPlaceSectionsViewModel(
        UIDocument uidoc,
        ILogService logService,
        IDialogService dialogService,
        IPlacementOrchestrator orchestrator,
        ISectionCollector sectionCollector,
        ITitleBlockCollector titleBlockCollector)
    {
        _uidoc = uidoc ?? throw new ArgumentNullException(nameof(uidoc));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _sectionCollector = sectionCollector ?? throw new ArgumentNullException(nameof(sectionCollector));
        _titleBlockCollector = titleBlockCollector ?? throw new ArgumentNullException(nameof(titleBlockCollector));

        // Setup filtered view
        FilteredSections = CollectionViewSource.GetDefaultView(Sections);
        FilteredSections.Filter = FilterPredicate;

        // Initialize algorithms
        foreach (PlacementAlgorithm algo in Enum.GetValues(typeof(PlacementAlgorithm)))
            AvailableAlgorithms.Add(algo);

        // Initialize commands
        PlaceCommand = new AsyncRelayCommand(ExecutePlacementAsync, () => CanPlace);
        CancelCommand = new RelayCommand(ExecuteCancel, () => ShowCancelButton);
        RefreshCommand = new AsyncRelayCommand(RefreshDataAsync, () => CanRefresh);
        SelectAllCommand = new RelayCommand(ExecuteSelectAll, () => IsUiEnabled);
        ClearSelectionCommand = new RelayCommand(ExecuteClearSelection, () => IsUiEnabled);
        ClearLogCommand = new RelayCommand(ExecuteClearLog);
        ExportLogCommand = new RelayCommand(ExecuteExportLog);
        CopyLogsCommand = new RelayCommand(ExecuteCopyLogs);

        // Create external event handler - FIXED: Parameter order corrected
        _handler = new SectionPlacementHandler(_orchestrator, this);
        _placementEvent = ExternalEvent.Create(_handler);

        // Wire up log service
        _logService.LogEntryAdded += OnLogEntryAdded;

        // Initialize data
        _ = RefreshDataAsync();
    }

    // ---------- Command Execution ----------
    private async Task ExecutePlacementAsync()
    {
        if (SelectedTitleBlock == null)
        {
            _dialogService.ShowError("Please select a title block.", "No Title Block");
            return;
        }

        var selected = FilteredSections
            .Cast<SectionItemViewModel>()
            .Where(x => x.IsSelected && (!SkipPlacedViews || !x.IsPlaced))
            .ToList();

        if (!selected.Any())
        {
            _dialogService.ShowWarning("No sections selected for placement.", "No Sections");
            return;
        }

        // Update estimates
        EstimatedSheets = (int)Math.Ceiling(selected.Count / 6.0);
        EstimatedTime = $"{selected.Count * 2}s";

        CurrentState = PluginState.Processing;
        Progress.Reset(selected.Count);

        var request = new PlacementRequest(
            SelectedTitleBlock.SymbolId,
            selected,
            SelectedAlgorithm,
            new Margins(LeftMarginMm, RightMarginMm, TopMarginMm, BottomMarginMm),
            new Gaps(HorizontalGapMm, VerticalGapMm, YToleranceMm),
            SkipPlacedViews,
            OpenSheetsAfterPlacement);

        _handler.SetRequest(request);
        _placementEvent.Raise();

        await Task.CompletedTask;
    }

    private void ExecuteCancel()
    {
        _orchestrator.Cancel();
        CurrentState = PluginState.Cancelling;
        _logService.LogWarning("Cancellation requested by user.");
    }

    private async Task RefreshDataAsync()
    {
        if (IsProcessing)
            return;

        CurrentState = PluginState.Initializing;
        _logService.LogInfo("🔄 Refreshing data...");

        await Task.Run(() =>
        {
            var sections = _sectionCollector.Collect(_uidoc.Document);
            var titleBlocks = _titleBlockCollector.Collect(_uidoc.Document);

            App.Current.Dispatcher.Invoke(() =>
            {
                UpdateSections(sections);
                UpdateTitleBlocks(titleBlocks);
                UpdateFilterOptions();
                UpdateEstimates();
            });
        });

        CurrentState = Sections.Any(x => !x.IsPlaced) ? PluginState.ReadyToPlace : PluginState.Idle;
        _logService.LogInfo($"✅ Refresh complete. {Sections.Count} sections, {TitleBlocks.Count} title blocks.");
    }

    private void UpdateEstimates()
    {
        var unplacedCount = Sections.Count(x => !x.IsPlaced);
        EstimatedSheets = (int)Math.Ceiling(unplacedCount / 8.0);
        EstimatedTime = $"{unplacedCount * 1.5:F0}s";
    }

    private void ExecuteSelectAll()
    {
        foreach (var item in FilteredSections.Cast<SectionItemViewModel>())
            item.IsSelected = true;

        OnPropertyChanged(nameof(SelectedSectionsCount));
        _logService.LogInfo($"✅ Selected all {SelectedSectionsCount} filtered sections.");
    }

    private void ExecuteClearSelection()
    {
        foreach (var item in FilteredSections.Cast<SectionItemViewModel>())
            item.IsSelected = false;

        OnPropertyChanged(nameof(SelectedSectionsCount));
        _logService.LogInfo("🧹 Cleared all selections.");
    }

    private void ExecuteClearLog()
    {
        _logService.Clear();
        App.Current.Dispatcher.Invoke(() => LogEntries.Clear());
        _logService.LogInfo("🧹 Log cleared.");
    }

    private void ExecuteExportLog()
    {
        var fileName = _dialogService.ShowSaveFileDialog(
            "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            $"APUS_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

        if (!string.IsNullOrEmpty(fileName))
        {
            var logText = string.Join(Environment.NewLine, _logService.Entries.Select(e =>
                $"[{e.Timestamp:HH:mm:ss}] {e.Message}"));
            System.IO.File.WriteAllText(fileName, logText);
            _logService.LogSuccess($"✅ Log exported to: {fileName}");
        }
    }

    private void ExecuteCopyLogs()
    {
        var logText = string.Join(Environment.NewLine, _logService.Entries.Select(e =>
            $"[{e.Timestamp:HH:mm:ss}] {e.Message}"));
        _dialogService.CopyToClipboard(logText);
        _logService.LogSuccess("📋 Logs copied to clipboard.");
    }

    // ---------- Private Methods ----------
    private bool FilterPredicate(object obj)
    {
        if (obj is not SectionItemViewModel item)
            return false;

        if (SelectedPlacementState != ALL)
        {
            if (SelectedPlacementState == "Placed Only" && !item.IsPlaced) return false;
            if (SelectedPlacementState == "Unplaced Only" && item.IsPlaced) return false;
        }

        if (SelectedPlacementScope != ALL &&
            !string.Equals(item.PlacementScope, SelectedPlacementScope, StringComparison.OrdinalIgnoreCase))
            return false;

        if (SheetNumberFilter != ALL &&
            !string.Equals(item.SheetNumber, SheetNumberFilter, StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    private void UpdateSections(IReadOnlyList<SectionItemViewModel> sections)
    {
        Sections.Clear();
        foreach (var section in sections)
            Sections.Add(section);
    }

    private void UpdateTitleBlocks(IReadOnlyList<TitleBlockItemViewModel> titleBlocks)
    {
        TitleBlocks.Clear();
        foreach (var tb in titleBlocks)
            TitleBlocks.Add(tb);

        SelectedTitleBlock = TitleBlocks.FirstOrDefault();
    }

    private void UpdateFilterOptions()
    {
        PlacementScopes.Clear();
        SheetNumberOptions.Clear();

        PlacementScopes.Add(ALL);
        SheetNumberOptions.Add(ALL);

        foreach (var section in Sections)
        {
            if (!string.IsNullOrWhiteSpace(section.PlacementScope) &&
                !PlacementScopes.Contains(section.PlacementScope))
                PlacementScopes.Add(section.PlacementScope);

            if (section.IsPlaced &&
                !string.IsNullOrWhiteSpace(section.SheetNumber) &&
                !SheetNumberOptions.Contains(section.SheetNumber))
                SheetNumberOptions.Add(section.SheetNumber);
        }
    }

    private void OnLogEntryAdded(object? sender, LogEntry entry)
    {
        App.Current.Dispatcher.BeginInvoke(() =>
        {
            LogEntries.Add(new LogEntryViewModel(entry));
            while (LogEntries.Count > 1000)
                LogEntries.RemoveAt(0);
        });
    }

    partial void OnSelectedPlacementScopeChanged(string value) => FilteredSections?.Refresh();
    partial void OnSheetNumberFilterChanged(string value) => FilteredSections?.Refresh();
    partial void OnSelectedPlacementStateChanged(string value) => FilteredSections?.Refresh();
    partial void OnSelectedAlgorithmChanged(PlacementAlgorithm value) => PlaceCommand.NotifyCanExecuteChanged();

    // ---------- Public Methods ----------
    public void OnPlacementComplete(PlacementResult result)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            if (result.Success)
            {
                CurrentState = PluginState.Completed;
                _logService.LogSuccess($"✅ Placement completed: {result.ViewsPlaced} views placed on {result.SheetNumbers.Count} sheets.");
                _ = RefreshDataAsync();
            }
            else
            {
                CurrentState = PluginState.Error;
                _logService.LogError($"❌ Placement failed: {result.ErrorMessage ?? "Unknown error"}");
            }

            PlaceCommand.NotifyCanExecuteChanged();
        });
    }

    // ---------- IDisposable ----------
    public void Dispose()
    {
        _logService.LogEntryAdded -= OnLogEntryAdded;
        _placementEvent?.Dispose();
        _handler.SetRequest(null!);
    }
}