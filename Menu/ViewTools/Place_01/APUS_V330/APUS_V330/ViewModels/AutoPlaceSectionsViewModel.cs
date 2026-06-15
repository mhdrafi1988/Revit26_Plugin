// File: ViewModels/AutoPlaceSectionsViewModel.cs
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.APUS_V330.Commands;
using Revit26_Plugin.APUS_V330.ExternalEvents;
using Revit26_Plugin.APUS_V330.Models;
using Revit26_Plugin.APUS_V330.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace Revit26_Plugin.APUS_V330.ViewModels
{
    public class AutoPlaceSectionsViewModel : BaseViewModel
    {
        private const string ALL = "All";

        private readonly UIDocument              _uidoc;
        private readonly SectionPlacementHandler _placementHandler;
        private readonly ExternalEvent           _placementEvent;

        private PluginState _currentState   = PluginState.ReadyToPlace;
        private string      _statusMessage  = "Ready";
        private string      _copyableLogText = string.Empty;
        private bool        _isLogExpanded  = true;

        // ===================== GRID PLACEMENT PROPERTIES =====================
        private int  _gridColumns    = 4;
        private int  _gridRows       = 4;
        private bool _enableGridInput = true;

        public int GridColumns
        {
            get => _gridColumns;
            set
            {
                if (value >= 1 && value <= 8 && SetField(ref _gridColumns, value))
                {
                    OnPropertyChanged(nameof(MaxViewsPerSheet));
                    OnPropertyChanged(nameof(EstimatedSheets));
                    OnPropertyChanged(nameof(EstimatedTime));
                }
            }
        }

        public int GridRows
        {
            get => _gridRows;
            set
            {
                if (value >= 1 && value <= 8 && SetField(ref _gridRows, value))
                {
                    OnPropertyChanged(nameof(MaxViewsPerSheet));
                    OnPropertyChanged(nameof(EstimatedSheets));
                    OnPropertyChanged(nameof(EstimatedTime));
                }
            }
        }

        public bool EnableGridInput
        {
            get => _enableGridInput;
            private set => SetField(ref _enableGridInput, value);
        }

        public int MaxViewsPerSheet => GridColumns * GridRows;

        // ---------------- DATA COLLECTIONS ----------------
        public ObservableCollection<SectionItemViewModel>   Sections    { get; } = new();
        public ICollectionView                              FilteredSections { get; }
        public ObservableCollection<TitleBlockItemViewModel> TitleBlocks { get; } = new();
        public ObservableCollection<LogEntryViewModel>      LogEntries  { get; } = new();
        public PlacementProgressViewModel                   Progress    { get; } = new();

        // ---------------- FILTER SOURCES ----------------
        public ObservableCollection<string> PlacementScopes   { get; } = new();
        public ObservableCollection<string> SheetNumberOptions { get; } = new();
        public ObservableCollection<string> PlacementStates   { get; } =
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

        private bool _allowRotation = false;
        public bool AllowRotation
        {
            get => _allowRotation;
            set => SetField(ref _allowRotation, value);
        }

        private bool _placeToMultipleSheets = false;
        public bool PlaceToMultipleSheets
        {
            get => _placeToMultipleSheets;
            set => SetField(ref _placeToMultipleSheets, value);
        }

        // ---------------- TITLE BLOCK ----------------
        private TitleBlockItemViewModel _selectedTitleBlock;
        public TitleBlockItemViewModel SelectedTitleBlock
        {
            get => _selectedTitleBlock;
            set
            {
                if (SetField(ref _selectedTitleBlock, value))
                {
                    OnPropertyChanged(nameof(CanPlace));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        // ---------------- LAYOUT SETTINGS (mm) ----------------
        private double _leftMarginMm   = 40;
        public double LeftMarginMm
        {
            get => _leftMarginMm;
            set => SetField(ref _leftMarginMm, Math.Max(0, value));
        }

        private double _rightMarginMm  = 150;
        public double RightMarginMm
        {
            get => _rightMarginMm;
            set => SetField(ref _rightMarginMm, Math.Max(0, value));
        }

        private double _topMarginMm    = 40;
        public double TopMarginMm
        {
            get => _topMarginMm;
            set => SetField(ref _topMarginMm, Math.Max(0, value));
        }

        private double _bottomMarginMm = 100;
        public double BottomMarginMm
        {
            get => _bottomMarginMm;
            set => SetField(ref _bottomMarginMm, Math.Max(0, value));
        }

        private double _horizontalGapMm = 10;
        public double HorizontalGapMm
        {
            get => _horizontalGapMm;
            set => SetField(ref _horizontalGapMm, Math.Max(0, value));
        }

        private double _verticalGapMm = 10;
        public double VerticalGapMm
        {
            get => _verticalGapMm;
            set => SetField(ref _verticalGapMm, Math.Max(0, value));
        }

        private double _yToleranceMm = 10;
        public double YToleranceMm
        {
            get => _yToleranceMm;
            set => SetField(ref _yToleranceMm, Math.Max(1, value));
        }

        // ---------------- STATE ----------------
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

                    if (value == PluginState.Processing)
                        IsLogExpanded = true;
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetField(ref _statusMessage, value);
        }

        public bool IsUiEnabled =>
            CurrentState == PluginState.Idle          ||
            CurrentState == PluginState.ReadyToPlace  ||
            CurrentState == PluginState.Completed     ||
            CurrentState == PluginState.Error;

        public bool IsProcessing =>
            CurrentState == PluginState.Processing ||
            CurrentState == PluginState.Cancelling;

        public bool ShowProgress     => IsProcessing;
        public bool ShowCancelButton => IsProcessing;

        public bool CanPlace   => !IsProcessing && SelectedSectionsCount > 0 && SelectedTitleBlock != null;
        public bool CanRefresh => !IsProcessing;

        public string CopyableLogText
        {
            get => _copyableLogText;
            private set => SetField(ref _copyableLogText, value);
        }

        public bool IsLogExpanded
        {
            get => _isLogExpanded;
            set => SetField(ref _isLogExpanded, value);
        }

        // ---------------- CALCULATED PROPERTIES ----------------
        /// <summary>
        /// Count of items in the current filtered view that have IsSelected = true.
        /// This is the source of truth — driven by the checkboxes, not by WPF row selection.
        /// </summary>
        public int SelectedSectionsCount =>
            FilteredSections?.Cast<SectionItemViewModel>().Count(x => x.IsSelected) ?? 0;

        public int EstimatedSheets
        {
            get
            {
                if (SelectedSectionsCount == 0) return 0;
                return PlaceToMultipleSheets
                    ? (int)Math.Ceiling(SelectedSectionsCount / (double)MaxViewsPerSheet)
                    : 1;
            }
        }

        public string EstimatedTime
        {
            get
            {
                if (SelectedSectionsCount == 0) return "0s";
                int totalSeconds = (int)(SelectedSectionsCount * 2.0);
                if (totalSeconds > 60)
                {
                    int minutes = totalSeconds / 60;
                    int seconds = totalSeconds % 60;
                    return $"{minutes}m {seconds}s";
                }
                return $"{totalSeconds}s";
            }
        }

        // ---------------- COMMANDS ----------------
        public IRelayCommand PlaceCommand             { get; }
        public IRelayCommand CancelCommand            { get; }
        public IRelayCommand RefreshCommand           { get; }
        /// <summary>Checks ALL rows currently visible in the filtered DataGrid.</summary>
        public IRelayCommand SelectAllFilteredCommand { get; }
        /// <summary>Unchecks ALL rows currently visible in the filtered DataGrid.</summary>
        public IRelayCommand SelectNoneFilteredCommand { get; }
        public IRelayCommand ClearLogCommand          { get; }
        public IRelayCommand ExportLogCommand         { get; }
        public IRelayCommand CopyLogsCommand          { get; }
        public IRelayCommand CollapseLogCommand       { get; }
        public IRelayCommand ExpandLogCommand         { get; }

        // ---------------- CONSTRUCTOR ----------------
        public AutoPlaceSectionsViewModel(
            UIDocument uidoc,
            System.Collections.Generic.List<SectionItemViewModel>   preloadedSections,
            System.Collections.Generic.IList<TitleBlockItemViewModel> preloadedTitleBlocks,
            System.Collections.Generic.List<string>                 preloadedSheetNumbers,
            System.Collections.Generic.List<string>                 preloadedPlacementScopes)
        {
            _uidoc = uidoc ?? throw new ArgumentNullException(nameof(uidoc));

            _placementHandler = new SectionPlacementHandler { ViewModel = this };
            _placementEvent   = ExternalEvent.Create(_placementHandler);

            // Populate filter sources
            PlacementScopes.Add(ALL);
            SheetNumberOptions.Add(ALL);

            foreach (var scope in preloadedPlacementScopes)
                if (!string.IsNullOrWhiteSpace(scope) && !PlacementScopes.Contains(scope))
                    PlacementScopes.Add(scope);

            foreach (var sheet in preloadedSheetNumbers)
                if (!string.IsNullOrWhiteSpace(sheet) && !SheetNumberOptions.Contains(sheet))
                    SheetNumberOptions.Add(sheet);

            foreach (var section in preloadedSections)
                Sections.Add(section);

            foreach (var tb in preloadedTitleBlocks)
                TitleBlocks.Add(tb);

            SelectedTitleBlock = TitleBlocks.FirstOrDefault();

            FilteredSections = CollectionViewSource.GetDefaultView(Sections);
            FilteredSections.Filter = FilterPredicate;

            // Commands
            PlaceCommand              = new RelayCommand(ExecutePlacement,   () => CanPlace);
            CancelCommand             = new RelayCommand(ExecuteCancel,      () => ShowCancelButton);
            RefreshCommand            = new RelayCommand(ExecuteRefresh,     () => CanRefresh);
            SelectAllFilteredCommand  = new RelayCommand(ExecuteSelectAll,   () => IsUiEnabled);
            SelectNoneFilteredCommand = new RelayCommand(ExecuteSelectNone,  () => IsUiEnabled);
            ClearLogCommand           = new RelayCommand(ExecuteClearLog);
            ExportLogCommand          = new RelayCommand(ExecuteExportLog);
            CopyLogsCommand           = new RelayCommand(ExecuteCopyLogs);
            CollapseLogCommand        = new RelayCommand(() => IsLogExpanded = false);
            ExpandLogCommand          = new RelayCommand(() => IsLogExpanded = true);

            // Initialise log header
            CopyableLogText  = "=== APUS V330 - Auto Place Sections ===\n";
            CopyableLogText += $"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
            CopyableLogText += "=======================================\n\n";
            CopyableLogText += $"Loaded {Sections.Count} sections, {TitleBlocks.Count} title blocks\n";
            CopyableLogText += $"   Placed:   {Sections.Count(s => s.IsPlaced)}\n";
            CopyableLogText += $"   Unplaced: {Sections.Count(s => !s.IsPlaced)}\n\n";

            LogEntries.CollectionChanged += (s, e) => OnPropertyChanged(nameof(LogEntries));

            LogInfo("UI READY - Data pre-loaded from Revit context");
            LogInfo($"Sections: {Sections.Count} total, {Sections.Count(s => !s.IsPlaced)} unplaced");
            LogInfo($"Title blocks: {TitleBlocks.Count}");
            LogInfo($"Grid default: {GridRows}x{GridColumns} (max {MaxViewsPerSheet} views per sheet)");
            LogInfo($"Multi-sheet mode: {(PlaceToMultipleSheets ? "Enabled" : "Disabled (single sheet)")}");

            CurrentState = PluginState.ReadyToPlace;
        }

        // ---------------- FILTER ----------------
        private bool FilterPredicate(object obj)
        {
            if (obj is not SectionItemViewModel item) return false;

            if (SelectedPlacementState != ALL)
            {
                if (SelectedPlacementState == "Placed Only"   && !item.IsPlaced) return false;
                if (SelectedPlacementState == "Unplaced Only" &&  item.IsPlaced) return false;
            }

            if (SelectedPlacementScope != ALL &&
                !string.Equals(item.PlacementScope, SelectedPlacementScope, StringComparison.OrdinalIgnoreCase))
                return false;

            if (SheetNumberFilter != ALL &&
                !string.Equals(item.SheetNumber, SheetNumberFilter, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        private void OnFilterChanged()
        {
            FilteredSections?.Refresh();
            OnPropertyChanged(nameof(SelectedSectionsCount));
            UpdateReadyState();
        }

        private void UpdateReadyState()
        {
            if (CurrentState == PluginState.Idle && SelectedSectionsCount > 0)
                CurrentState = PluginState.ReadyToPlace;
            else if (CurrentState == PluginState.ReadyToPlace && SelectedSectionsCount == 0)
                CurrentState = PluginState.Idle;

            OnPropertyChanged(nameof(CanPlace));
            CommandManager.InvalidateRequerySuggested();
        }

        private void UpdateStatusMessage()
        {
            StatusMessage = CurrentState switch
            {
                PluginState.Idle         => $"Idle ({SelectedSectionsCount} sections selected)",
                PluginState.ReadyToPlace => $"Ready to place ({SelectedSectionsCount} selected)",
                PluginState.Processing   => $"Placing {Progress.Current}/{Progress.Total} sections...",
                PluginState.Cancelling   => "Cancelling...",
                PluginState.Completed    => $"Completed: {Progress.Current} placed",
                PluginState.Error        => "Error occurred - check logs",
                _                        => "Unknown state"
            };
        }

        // ---------------- COMMAND IMPLEMENTATIONS ----------------

        private void ExecutePlacement()
        {
            LogInfo("================================================");
            LogInfo("STARTING PLACEMENT OPERATION");
            LogInfo("================================================");

            try
            {
                var selectedSections = FilteredSections
                    .Cast<SectionItemViewModel>()
                    .Where(x => x.IsSelected && (!SkipPlacedViews || !x.IsPlaced))
                    .ToList();

                if (!selectedSections.Any())
                {
                    LogWarning("No sections selected for placement.");
                    return;
                }

                if (SelectedTitleBlock == null)
                {
                    LogError("No title block selected. Please select a title block.");
                    return;
                }

                LogConfiguration(selectedSections.Count);

                _placementHandler.SectionsToPlace = selectedSections;
                _placementHandler.ViewModel       = this;

                CurrentState = PluginState.Processing;
                Progress.Reset(selectedSections.Count);
                Progress.CurrentOperation = "Placing sections...";

                var requestResult = _placementEvent.Raise();

                if (requestResult == ExternalEventRequest.Accepted)
                    LogSuccess($"Placement event raised for {selectedSections.Count} sections");
                else
                {
                    LogError($"Failed to raise placement event: {requestResult}");
                    CurrentState = PluginState.Error;
                    OnPlacementComplete(false, "Failed to raise Revit event");
                }
            }
            catch (Exception ex)
            {
                LogError($"Placement failed: {ex.Message}");
                OnPlacementComplete(false, ex.Message);
            }
        }

        private void ExecuteCancel()
        {
            LogWarning("CANCELLATION REQUESTED...");
            CurrentState = PluginState.Cancelling;
            Progress.Cancel();
            Progress.CurrentOperation = "Cancelling...";
        }

        private void ExecuteRefresh()
        {
            LogInfo("REFRESHING DATA...");

            try
            {
                var uiApp    = _uidoc.Application;
                var commandId = RevitCommandId.LookupCommandId("Custom.APUS_V330.AutoPlaceSections");

                if (commandId != null && uiApp.CanPostCommand(commandId))
                {
                    uiApp.PostCommand(commandId);
                    LogInfo("Refresh command posted to Revit via CommandId");
                }
                else
                {
                    LogInfo("Command ID not found, using direct invocation...");
                    var result = AutoPlaceSectionsCommand.Invoke(uiApp);

                    if (result == Autodesk.Revit.UI.Result.Succeeded)
                        LogInfo("Refresh completed via direct command invocation");
                    else
                        LogWarning($"Direct command invocation returned: {result}");
                }

                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    foreach (Window window in Application.Current.Windows)
                    {
                        if (window.DataContext == this)
                        {
                            window.Close();
                            break;
                        }
                    }
                }));
            }
            catch (Exception ex)
            {
                LogError($"Refresh failed: {ex.Message}");

                try
                {
                    LogInfo("Attempting emergency direct execution...");
                    var command  = new AutoPlaceSectionsCommand();
                    string msg   = null;
                    var elements = new Autodesk.Revit.DB.ElementSet();
                    command.Execute(_uidoc.Application, ref msg, elements, out _);
                    LogInfo("Refresh completed via emergency direct execution");
                }
                catch (Exception innerEx)
                {
                    LogError($"All refresh methods failed: {innerEx.Message}");
                    MessageBox.Show(
                        "Could not automatically refresh. Please close this window and restart the command from the Revit ribbon.",
                        "APUS V330 - Refresh Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
        }

        /// <summary>
        /// Checks the IsSelected checkbox for every row currently visible in
        /// the filtered DataGrid. Does not touch WPF row-selection state.
        /// </summary>
        private void ExecuteSelectAll()
        {
            int count = 0;
            foreach (var item in FilteredSections.Cast<SectionItemViewModel>())
            {
                item.IsSelected = true;
                count++;
            }
            LogInfo($"Checked all {count} visible sections");
            OnPropertyChanged(nameof(SelectedSectionsCount));
            UpdateReadyState();
        }

        /// <summary>
        /// Unchecks the IsSelected checkbox for every row currently visible in
        /// the filtered DataGrid. Does not touch WPF row-selection state.
        /// </summary>
        private void ExecuteSelectNone()
        {
            int count = 0;
            foreach (var item in FilteredSections.Cast<SectionItemViewModel>())
            {
                item.IsSelected = false;
                count++;
            }
            LogInfo($"Unchecked all {count} visible sections");
            OnPropertyChanged(nameof(SelectedSectionsCount));
            UpdateReadyState();
        }

        private void ExecuteClearLog()
        {
            LogEntries.Clear();
            CopyableLogText = $"=== Log cleared at {DateTime.Now:HH:mm:ss} ===\n\n";
            LogInfo("Log cleared");
        }

        private void ExecuteExportLog()
        {
            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter   = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                    FileName = $"APUS_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                    Title    = "Export Log"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    System.IO.File.WriteAllText(saveDialog.FileName, CopyableLogText);
                    LogSuccess($"Log exported to: {saveDialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to export log: {ex.Message}");
            }
        }

        private void ExecuteCopyLogs()
        {
            try
            {
                if (!string.IsNullOrEmpty(CopyableLogText))
                {
                    Clipboard.SetText(CopyableLogText);
                    LogSuccess("Logs copied to clipboard");
                }
                else
                {
                    LogWarning("No logs to copy");
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to copy logs: {ex.Message}");
            }
        }

        private void LogConfiguration(int sectionCount)
        {
            var sb = new StringBuilder();
            sb.AppendLine("PLACEMENT CONFIGURATION:");
            sb.AppendLine($"   Algorithm:       Grid");
            sb.AppendLine($"   Grid:            {GridRows}x{GridColumns} (max {MaxViewsPerSheet} per sheet)");
            sb.AppendLine($"   Multi-sheet:     {(PlaceToMultipleSheets ? "Enabled" : "Disabled (single sheet)")}");
            sb.AppendLine($"   Sections:        {sectionCount}");
            sb.AppendLine($"   Title block:     {SelectedTitleBlock?.DisplayName ?? "None selected"}");
            sb.AppendLine($"   Margins:         L={LeftMarginMm}mm  R={RightMarginMm}mm  T={TopMarginMm}mm  B={BottomMarginMm}mm");
            sb.AppendLine($"   Gaps:            H={HorizontalGapMm}mm  V={VerticalGapMm}mm");
            sb.AppendLine($"   Y Tolerance:     {YToleranceMm}mm");
            sb.AppendLine($"   Skip placed:     {SkipPlacedViews}");
            sb.AppendLine($"   Open sheets:     {OpenSheetsAfterPlacement}");
            sb.AppendLine($"   Est. sheets:     {EstimatedSheets}");
            sb.AppendLine($"   Est. time:       {EstimatedTime}");
            LogInfo(sb.ToString());
        }

        // ---------------- LOGGING ----------------
        public void LogInfo(string msg)    => AddLog(LogLevel.Info,    $"ℹ️ {msg}");
        public void LogWarning(string msg) => AddLog(LogLevel.Warning,  $"⚠️ {msg}");
        public void LogError(string msg)   => AddLog(LogLevel.Error,    $"❌ {msg}");
        public void LogSuccess(string msg) => AddLog(LogLevel.Success,  $"✅ {msg}");

        private void AddLog(LogLevel level, string msg)
        {
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                var timestamp = DateTime.Now;
                LogEntries.Add(new LogEntryViewModel(timestamp, level, msg));
                CopyableLogText += $"{timestamp:HH:mm:ss} {msg}\n";

                // Cap in-memory log size
                var lines = CopyableLogText.Split('\n');
                if (lines.Length > 5000)
                    CopyableLogText = string.Join("\n", lines.Skip(lines.Length - 5000));

                if (LogEntries.Count > 1000)
                    LogEntries.RemoveAt(0);
            }));
        }

        // ---------------- COMPLETION ----------------
        public void OnPlacementComplete(bool success, string message = "")
        {
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                if (CurrentState == PluginState.Cancelling)
                {
                    CurrentState = PluginState.ReadyToPlace;
                    LogWarning("Placement cancelled by user");
                    Progress.CurrentOperation = "Cancelled";
                }
                else if (success)
                {
                    CurrentState = PluginState.Completed;
                    LogSuccess($"Placement completed: {message}");
                    Progress.CurrentOperation = "Completed";
                }
                else
                {
                    CurrentState = PluginState.Error;
                    LogError($"Placement failed: {message}");
                    Progress.CurrentOperation = "Failed";
                }

                OnPropertyChanged(nameof(CanPlace));
                CommandManager.InvalidateRequerySuggested();
            }));
        }

        public void LogProgressUpdate(int current, int total, string operation)
        {
            if (!string.IsNullOrEmpty(operation))
                Progress.CurrentOperation = operation;

            Progress.Current = current;
            Progress.Total   = total;
            StatusMessage    = $"Processing: {current}/{total} - {operation}";
            OnPropertyChanged(nameof(StatusMessage));
            OnPropertyChanged(nameof(Progress));
        }

        // ---------------- CLEANUP ----------------
        public void OnWindowClosing()
        {
            LogInfo("Window closing...");

            if (IsProcessing)
            {
                LogWarning("Placement in progress - cancelling...");
                Progress.Cancel();
            }

            LogInfo("APUS V330 session ended");
            LogInfo("================================================");

            _placementEvent?.Dispose();
        }
    }
}
