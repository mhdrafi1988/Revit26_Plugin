// File: AutoPlaceSectionsViewModel.cs
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.APUS_V321_01.Commands;
using Revit26_Plugin.APUS_V321_01.ExternalEvents;
using Revit26_Plugin.APUS_V321_01.Models;
using Revit26_Plugin.APUS_V321_01.Services;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace Revit26_Plugin.APUS_V321_01.ViewModels
{
    /// <summary>
    /// V321 changes vs V320:
    ///  - Grid rows/columns/MaxViewsPerSheet removed — no per-sheet cap;
    ///    sheets fill by real footprint and overflow to the next sheet.
    ///  - PlacementScopes / SheetNumberOptions / SelectedPlacementScope /
    ///    SheetNumberFilter removed — Whole Model is the only scope, and
    ///    filtering narrows to name search + placed/unplaced only.
    ///  - NameSearch + PlacementFilterState added for the new filter row.
    ///  - Reading Order card inputs added: ReadingDirection, YToleranceMm
    ///    (now actually exposed — V320 had the field but no UI for it),
    ///    RowTiebreak, MarkerSource, SortFallback.
    ///  - SheetNumberFormat / SheetPrefix drive SheetNumberService's
    ///    "{prefix}-{3-digit}" numbering.
    ///  - RunMetrics (Placed/Skipped/Failed) added for the metric bar.
    ///  - Sortable grid: SortColumn/SortAscending + ApplySort().
    ///  - Confirm-before-placing hook: PendingConfirmation raised before the
    ///    external event fires, showing sections/sheets estimate; handler
    ///    proceeds only after explicit confirmation.
    /// </summary>
    public class AutoPlaceSectionsViewModel : BaseViewModel
    {
        private readonly UIDocument _uidoc;
        private readonly SectionPlacementHandler _placementHandler;
        private readonly ExternalEvent _placementEvent;

        private PluginState _currentState = PluginState.ReadyToPlace;
        private string _statusMessage = "Ready";
        private string _copyableLogText = string.Empty;
        private bool _isLogExpanded = true;

        // ===================== DATA COLLECTIONS (Pre-loaded) =====================
        public ObservableCollection<SectionItemViewModel> Sections { get; } = new();
        public ICollectionView FilteredSections { get; }
        public ObservableCollection<TitleBlockItemViewModel> TitleBlocks { get; } = new();
        public ObservableCollection<LogEntryViewModel> LogEntries { get; } = new();
        public PlacementProgressViewModel Progress { get; } = new PlacementProgressViewModel();
        public PlacementRunMetrics RunMetrics { get; } = new PlacementRunMetrics();

        // ===================== SECTIONS FILTER (name search + 3-way toggle) =====================
        private string _nameSearch = string.Empty;
        public string NameSearch
        {
            get => _nameSearch;
            set { SetField(ref _nameSearch, value); OnFilterChanged(); }
        }

        private PlacementFilterState _placementFilter = PlacementFilterState.All;
        public PlacementFilterState PlacementFilter
        {
            get => _placementFilter;
            set { SetField(ref _placementFilter, value); OnFilterChanged(); }
        }

        // ===================== SORTABLE GRID =====================
        private SectionGridSortColumn _sortColumn = SectionGridSortColumn.Name;
        public SectionGridSortColumn SortColumn
        {
            get => _sortColumn;
            private set => SetField(ref _sortColumn, value);
        }

        private bool _sortAscending = true;
        public bool SortAscending
        {
            get => _sortAscending;
            private set => SetField(ref _sortAscending, value);
        }

        /// <summary>Toggles sort direction if the same column is clicked again, otherwise sorts ascending on the new column.</summary>
        public void SetSortColumn(SectionGridSortColumn column)
        {
            if (SortColumn == column)
                SortAscending = !SortAscending;
            else
            {
                SortColumn = column;
                SortAscending = true;
            }
            ApplySort();
        }

        private void ApplySort()
        {
            if (FilteredSections is ListCollectionView lcv)
            {
                lcv.SortDescriptions.Clear();
                string propertyName = SortColumn switch
                {
                    SectionGridSortColumn.Name => nameof(SectionItemViewModel.ViewName),
                    SectionGridSortColumn.Size => nameof(SectionItemViewModel.SizeWidthM),
                    SectionGridSortColumn.Placed => nameof(SectionItemViewModel.IsPlaced),
                    SectionGridSortColumn.DetailNumber => nameof(SectionItemViewModel.DetailNumber),
                    _ => nameof(SectionItemViewModel.ViewName)
                };
                lcv.SortDescriptions.Add(new SortDescription(
                    propertyName,
                    SortAscending ? ListSortDirection.Ascending : ListSortDirection.Descending));
            }
        }

        // ===================== UI OPTIONS =====================
        private bool _openSheetsAfterPlacement = true;
        public bool OpenSheetsAfterPlacement
        {
            get => _openSheetsAfterPlacement;
            set => SetField(ref _openSheetsAfterPlacement, value);
        }

        private bool _skipPlacedViews = true;
        public bool SkipPlacedViews
        {
            get => _skipPlacedViews;
            set => SetField(ref _skipPlacedViews, value);
        }

        private bool _placeToMultipleSheets = true;
        public bool PlaceToMultipleSheets
        {
            get => _placeToMultipleSheets;
            set => SetField(ref _placeToMultipleSheets, value);
        }

        // ===================== TITLE BLOCK / SHEET NUMBERING =====================
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

        private string _sheetPrefix = "SEC";
        public string SheetPrefix
        {
            get => _sheetPrefix;
            set
            {
                var trimmed = string.IsNullOrWhiteSpace(value) ? "SEC" : value.Trim();
                if (SetField(ref _sheetPrefix, trimmed))
                    OnPropertyChanged(nameof(SheetNumberFormatPreview));
            }
        }

        /// <summary>Read-only preview shown in the UI, e.g. "SEC-001".</summary>
        public string SheetNumberFormatPreview => $"{SheetPrefix}-001";

        // ===================== PLACEMENT ALGORITHM =====================
        // Only one algorithm remains in V321 — no selector shown in the UI.
        public PlacementAlgorithm SelectedAlgorithm => PlacementAlgorithm.Grid;

        // ===================== LAYOUT SETTINGS (mm) =====================
        private double _leftMarginMm = 40;
        public double LeftMarginMm
        {
            get => _leftMarginMm;
            set => SetField(ref _leftMarginMm, Math.Max(0, value));
        }

        private double _rightMarginMm = 150;
        public double RightMarginMm
        {
            get => _rightMarginMm;
            set => SetField(ref _rightMarginMm, Math.Max(0, value));
        }

        private double _topMarginMm = 40;
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
        /// <summary>Minimum horizontal gap — rows spread edge-to-edge, this is the floor, not a fixed value.</summary>
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

        // ===================== READING ORDER CARD =====================
        private ReadingDirection _readingDirection = ReadingDirection.TopToBottom_LeftToRight;
        public ReadingDirection ReadingDirection
        {
            get => _readingDirection;
            set => SetField(ref _readingDirection, value);
        }

        public ObservableCollection<ReadingDirection> AvailableReadingDirections { get; } =
            new(Enum.GetValues(typeof(ReadingDirection)).Cast<ReadingDirection>());

        private double _yToleranceMm = 50;
        /// <summary>Row band width — points within this Y distance of the band average are treated as the same reading-order row. Default 50mm; V320 had a same-named field defaulting to 10mm but never surfaced it in the UI.</summary>
        public double YToleranceMm
        {
            get => _yToleranceMm;
            set => SetField(ref _yToleranceMm, Math.Max(1, value));
        }

        private RowTiebreak _rowTiebreak = RowTiebreak.XPosition;
        public RowTiebreak RowTiebreak
        {
            get => _rowTiebreak;
            set => SetField(ref _rowTiebreak, value);
        }

        public ObservableCollection<RowTiebreak> AvailableRowTiebreaks { get; } =
            new(Enum.GetValues(typeof(RowTiebreak)).Cast<RowTiebreak>());

        private MarkerSource _markerSource = MarkerSource.SectionCurveMidpoint;
        public MarkerSource MarkerSource
        {
            get => _markerSource;
            set => SetField(ref _markerSource, value);
        }

        public ObservableCollection<MarkerSource> AvailableMarkerSources { get; } =
            new(Enum.GetValues(typeof(MarkerSource)).Cast<MarkerSource>());

        private SortFallback _sortFallback = SortFallback.PushToEnd;
        public SortFallback SortFallback
        {
            get => _sortFallback;
            set => SetField(ref _sortFallback, value);
        }

        public ObservableCollection<SortFallback> AvailableSortFallbacks { get; } =
            new(Enum.GetValues(typeof(SortFallback)).Cast<SortFallback>());

        // ---------------- STATE PROPERTIES ----------------
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

        public bool IsUiEnabled => CurrentState is PluginState.Idle or PluginState.ReadyToPlace
                                                 or PluginState.Completed or PluginState.Error;

        public bool IsProcessing => CurrentState is PluginState.Processing or PluginState.Cancelling;

        public bool ShowProgress => IsProcessing;
        public bool ShowCancelButton => IsProcessing;

        public bool CanPlace => !IsProcessing && SelectedSectionsCount > 0 && SelectedTitleBlock != null;
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
        public int SelectedSectionsCount =>
            FilteredSections?.Cast<SectionItemViewModel>().Count(x => x.IsSelected && !x.IsPlaced) ?? 0;

        // ---------------- COMMANDS ----------------
        public IRelayCommand PlaceCommand { get; }
        public IRelayCommand CancelCommand { get; }
        public IRelayCommand RefreshCommand { get; }
        public IRelayCommand SelectAllCommand { get; }
        public IRelayCommand ClearSelectionCommand { get; }
        public IRelayCommand InvertSelectionCommand { get; }
        public IRelayCommand ClearLogCommand { get; }
        public IRelayCommand ExportLogCommand { get; }
        public IRelayCommand CopyLogsCommand { get; }
        public IRelayCommand CollapseLogCommand { get; }
        public IRelayCommand ExpandLogCommand { get; }

        /// <summary>
        /// Raised right before the placement event fires, so the View can show
        /// a Yes/No confirmation ("Place N sections across ~M sheets?").
        /// The handler passed in the args must be invoked with the user's
        /// choice via args.Respond(true/false) — placement only proceeds on true.
        /// </summary>
        public event EventHandler<PlacementConfirmationEventArgs> PendingConfirmation;

        // ---------------- CONSTRUCTOR (No Revit API calls!) ----------------
        public AutoPlaceSectionsViewModel(
            UIDocument uidoc,
            System.Collections.Generic.List<SectionItemViewModel> preloadedSections,
            System.Collections.Generic.IList<TitleBlockItemViewModel> preloadedTitleBlocks)
        {
            _uidoc = uidoc ?? throw new ArgumentNullException(nameof(uidoc));

            _placementHandler = new SectionPlacementHandler { ViewModel = this };
            _placementEvent = ExternalEvent.Create(_placementHandler);

            foreach (var section in preloadedSections)
                Sections.Add(section);

            foreach (var tb in preloadedTitleBlocks)
                TitleBlocks.Add(tb);

            SelectedTitleBlock = TitleBlocks.FirstOrDefault();

            FilteredSections = CollectionViewSource.GetDefaultView(Sections);
            FilteredSections.Filter = FilterPredicate;
            ApplySort();

            PlaceCommand = new RelayCommand(ExecutePlacement, () => CanPlace);
            CancelCommand = new RelayCommand(ExecuteCancel, () => ShowCancelButton);
            RefreshCommand = new RelayCommand(ExecuteRefresh, () => CanRefresh);
            SelectAllCommand = new RelayCommand(ExecuteSelectAll, () => IsUiEnabled);
            ClearSelectionCommand = new RelayCommand(ExecuteClearSelection, () => IsUiEnabled);
            InvertSelectionCommand = new RelayCommand(ExecuteInvertSelection, () => IsUiEnabled);
            ClearLogCommand = new RelayCommand(ExecuteClearLog);
            ExportLogCommand = new RelayCommand(ExecuteExportLog);
            CopyLogsCommand = new RelayCommand(ExecuteCopyLogs);
            CollapseLogCommand = new RelayCommand(() => IsLogExpanded = false);
            ExpandLogCommand = new RelayCommand(() => IsLogExpanded = true);

            CopyableLogText = "=== APUS V321 - Auto Place Sections ===\n";
            CopyableLogText += $"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
            CopyableLogText += "====================================\n\n";
            CopyableLogText += $"Loaded {Sections.Count} sections, {TitleBlocks.Count} title blocks\n";
            CopyableLogText += $"    Placed: {Sections.Count(s => s.IsPlaced)}\n";
            CopyableLogText += $"    Unplaced: {Sections.Count(s => !s.IsPlaced)}\n\n";

            LogEntries.CollectionChanged += (s, e) => OnPropertyChanged(nameof(LogEntries));

            LogInfo("UI ready — data pre-loaded from Revit context.");
            LogInfo($"Sections: {Sections.Count} total, {Sections.Count(s => !s.IsPlaced)} unplaced.");
            LogInfo($"Title blocks: {TitleBlocks.Count}.");

            CurrentState = PluginState.ReadyToPlace;
        }

        private bool FilterPredicate(object obj)
        {
            if (obj is not SectionItemViewModel item) return false;

            if (PlacementFilter == PlacementFilterState.PlacedOnly && !item.IsPlaced) return false;
            if (PlacementFilter == PlacementFilterState.UnplacedOnly && item.IsPlaced) return false;

            if (!string.IsNullOrWhiteSpace(NameSearch) &&
                item.ViewName.IndexOf(NameSearch, StringComparison.OrdinalIgnoreCase) < 0)
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
                PluginState.Idle => $"Idle ({SelectedSectionsCount} sections selected)",
                PluginState.ReadyToPlace => $"Ready to place ({SelectedSectionsCount} selected)",
                PluginState.Processing => $"Placing {Progress.Current}/{Progress.Total} sections...",
                PluginState.Cancelling => "Cancelling...",
                PluginState.Completed => $"Completed: {Progress.Current} placed",
                PluginState.Error => "Error occurred - check logs",
                _ => "Unknown state"
            };
        }

        // ---------------- COMMAND EXECUTION ----------------
        private void ExecutePlacement()
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

            // Rough sheet estimate for the confirmation dialog only — the
            // real count comes from EvenGapPlacementService.BuildPlan, which
            // the handler runs before actually creating anything. This
            // estimate exists purely so the dialog can show a number without
            // requiring a full dry run before the user has even said yes.
            int roughEstimate = Math.Max(1, selectedSections.Count / 6);

            var args = new PlacementConfirmationEventArgs(selectedSections.Count, roughEstimate);
            PendingConfirmation?.Invoke(this, args);

            if (!args.Confirmed)
            {
                LogInfo("Placement cancelled by user at confirmation prompt.");
                return;
            }

            LogInfo("");
            LogInfo("STARTING PLACEMENT OPERATION");
            LogInfo("");

            try
            {
                LogConfiguration(selectedSections.Count);

                _placementHandler.SectionsToPlace = selectedSections;
                _placementHandler.ViewModel = this;

                CurrentState = PluginState.Processing;
                Progress.Reset(selectedSections.Count);
                Progress.CurrentOperation = "Placing sections...";
                RunMetrics.Reset();

                ExternalEventRequest result = _placementEvent.Raise();

                if (result == ExternalEventRequest.Accepted)
                {
                    LogSuccess($"Placement event raised for {selectedSections.Count} sections.");
                }
                else
                {
                    LogError($"Failed to raise placement event: {result}");
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
            LogWarning("Cancellation requested...");
            CurrentState = PluginState.Cancelling;
            Progress.Cancel();
            Progress.CurrentOperation = "Cancelling...";
        }

        private void ExecuteRefresh()
        {
            LogInfo("Refreshing data...");

            try
            {
                var uiApp = _uidoc.Application;
                var commandId = RevitCommandId.LookupCommandId("Custom.APUS_V321.AutoPlaceSections");

                if (commandId != null && uiApp.CanPostCommand(commandId))
                {
                    uiApp.PostCommand(commandId);
                    LogInfo("Refresh command posted to Revit.");
                }
                else
                {
                    LogInfo("Command ID not found, using direct invocation...");
                    var result = AutoPlaceSectionsCommand.Invoke(uiApp);
                    if (result == Result.Succeeded)
                        LogInfo("Refresh completed via direct command invocation.");
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
                    var command = new AutoPlaceSectionsCommand();
                    string message = null;
                    var elements = new Autodesk.Revit.DB.ElementSet();
                    command.Execute(_uidoc.Application, ref message, elements, out _);
                    LogInfo("Refresh completed via emergency direct execution.");
                }
                catch (Exception innerEx)
                {
                    LogError($"All refresh methods failed: {innerEx.Message}");

                    System.Windows.MessageBox.Show(
                        "Could not automatically refresh. Please close this window and restart the command from the Revit ribbon.",
                        "APUS V321 - Refresh Failed",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }
            }
        }

        private void ExecuteSelectAll()
        {
            // Respects the active filter — only currently-visible, unplaced
            // rows are selected. Placed rows stay locked out regardless.
            int count = 0;
            foreach (var item in FilteredSections.Cast<SectionItemViewModel>())
            {
                if (item.IsPlaced) continue;
                item.IsSelected = true;
                count++;
            }
            LogInfo($"Selected all {count} filtered, unplaced sections.");
            OnPropertyChanged(nameof(SelectedSectionsCount));
            UpdateReadyState();
        }

        private void ExecuteClearSelection()
        {
            int count = 0;
            foreach (var item in FilteredSections.Cast<SectionItemViewModel>())
            {
                item.IsSelected = false;
                count++;
            }
            LogInfo($"Cleared selection of {count} sections.");
            OnPropertyChanged(nameof(SelectedSectionsCount));
            UpdateReadyState();
        }

        private void ExecuteInvertSelection()
        {
            int selected = 0, deselected = 0;
            foreach (var item in FilteredSections.Cast<SectionItemViewModel>())
            {
                if (item.IsPlaced) continue;
                item.IsSelected = !item.IsSelected;
                if (item.IsSelected) selected++; else deselected++;
            }
            LogInfo($"Inverted selection: {selected} selected, {deselected} deselected.");
            OnPropertyChanged(nameof(SelectedSectionsCount));
            UpdateReadyState();
        }

        public void NotifySelectionCountChanged()
        {
            OnPropertyChanged(nameof(SelectedSectionsCount));
            UpdateReadyState();
        }

        private void ExecuteClearLog()
        {
            LogEntries.Clear();
            CopyableLogText = $"=== Log cleared at {DateTime.Now:HH:mm:ss} ===\n\n";
            LogInfo("Log cleared.");
        }

        private void ExecuteExportLog()
        {
            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                    FileName = $"APUS_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                    Title = "Export Log"
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
                    LogSuccess("Logs copied to clipboard.");
                }
                else
                {
                    LogWarning("No logs to copy.");
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
            sb.AppendLine($"    Sections to place: {sectionCount}");
            sb.AppendLine($"    Title block: {SelectedTitleBlock?.DisplayName ?? "None selected"}");
            sb.AppendLine($"    Sheet prefix / format: {SheetPrefix} -> {SheetNumberFormatPreview}");
            sb.AppendLine($"    Margins: L={LeftMarginMm}mm, R={RightMarginMm}mm, T={TopMarginMm}mm, B={BottomMarginMm}mm");
            sb.AppendLine($"    Gaps: H(min)={HorizontalGapMm}mm, V={VerticalGapMm}mm");
            sb.AppendLine($"    Reading direction: {ReadingDirection}");
            sb.AppendLine($"    Row Y-tolerance: {YToleranceMm}mm");
            sb.AppendLine($"    Within-row tiebreak: {RowTiebreak}");
            sb.AppendLine($"    Marker source: {MarkerSource}");
            sb.AppendLine($"    Sort fallback: {SortFallback}");
            sb.AppendLine($"    Skip placed views: {SkipPlacedViews}");
            sb.AppendLine($"    Multi-sheet mode: {(PlaceToMultipleSheets ? "Enabled" : "Disabled")}");
            sb.AppendLine($"    Open sheets after: {OpenSheetsAfterPlacement}");

            LogInfo(sb.ToString());
        }

        // ---------------- LOGGING (shared LogLevel) ----------------
        public void LogInfo(string msg) => AddLog(LogLevel.Info, msg);
        public void LogWarning(string msg) => AddLog(LogLevel.Warning, msg);
        public void LogError(string msg) => AddLog(LogLevel.Error, msg);
        public void LogSuccess(string msg) => AddLog(LogLevel.Success, msg);

        private void AddLog(LogLevel level, string msg)
        {
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                var timestamp = DateTime.Now;
                var entry = new LogEntryViewModel(timestamp, level, msg);
                LogEntries.Add(entry);

                CopyableLogText += $"{timestamp:HH:mm:ss} {level,-7} {msg}\n";

                var lines = CopyableLogText.Split('\n');
                if (lines.Length > 5000)
                    CopyableLogText = string.Join("\n", lines.Skip(lines.Length - 5000));

                if (LogEntries.Count > 1000)
                    LogEntries.RemoveAt(0);
            }));
        }

        // ---------------- PLACEMENT COMPLETION ----------------
        public void OnPlacementComplete(bool success, string message = "")
        {
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                if (CurrentState == PluginState.Cancelling)
                {
                    CurrentState = PluginState.ReadyToPlace;
                    LogWarning("Placement cancelled by user.");
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
            Progress.Total = total;
            StatusMessage = $"Processing: {current}/{total} - {operation}";
            OnPropertyChanged(nameof(StatusMessage));
            OnPropertyChanged(nameof(Progress));
        }

        // ---------------- CLEANUP ----------------
        public void OnWindowClosing()
        {
            LogInfo("Window closing...");

            if (IsProcessing)
            {
                LogWarning("Placement in progress — cancelling...");
                Progress.Cancel();
            }

            LogInfo("APUS V321 session ended.");
            LogInfo("");

            _placementEvent?.Dispose();
        }
    }

    /// <summary>
    /// Args for the pre-placement confirmation dialog: "Place N sections
    /// across ~M sheets?" — the View sets Confirmed via a synchronous
    /// MessageBox call before returning control to ExecutePlacement.
    /// </summary>
    public class PlacementConfirmationEventArgs : EventArgs
    {
        public int SectionCount { get; }
        public int EstimatedSheetCount { get; }
        public bool Confirmed { get; set; }

        public PlacementConfirmationEventArgs(int sectionCount, int estimatedSheetCount)
        {
            SectionCount = sectionCount;
            EstimatedSheetCount = estimatedSheetCount;
        }
    }
}
