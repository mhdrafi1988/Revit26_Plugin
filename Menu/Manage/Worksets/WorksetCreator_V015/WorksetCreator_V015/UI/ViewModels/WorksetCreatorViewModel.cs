// ==============================================
// File: WorksetCreatorViewModel.cs
// Layer: UI/ViewModels
// Workset Creator V015 — renamed lineage of Workset Manager V014
// (WorksetsViewModel.cs).
//
// Changes vs WorksetManager V014:
//   CHG  Derives from Shared/ViewModels/ToolViewModelBase: the log collection
//        (LogEntries), Copy All / Copy Selected / Clear commands and the three
//        shell status properties (IsRunning / Progress / SummaryText) now come
//        from the shared base. The log therefore reads oldest-first (the base
//        appends; V014 inserted at index 0).
//   NEW  LastRunSummary — the run report (counts + one row per linked file)
//        shown in the window's "Last run" card and in a TaskDialog right after
//        each run (RunSummaryDialog), with CopySummaryCommand for the card.
//   CHG  Progress / KeepUIResponsive are driven by the service's progress
//        callback instead of the service holding this ViewModel.
// Everything else — the three grids, the pattern engine, duplicate handling,
// every command guard, Create and all three Resync paths, the single
// ExternalEvent — is V014 behaviour.
// ==============================================

using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.Shared.Models;
using Revit26_Plugin.Shared.ViewModels;
using Revit26_Plugin.WorksetCreator.V015.Core.Models;
using Revit26_Plugin.WorksetCreator.V015.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Threading;

namespace Revit26_Plugin.WorksetCreator.V015.UI.ViewModels
{
    public partial class WorksetCreatorViewModel : ToolViewModelBase
    {
        private readonly UIDocument _uidoc;
        private readonly WorksetService _service;
        private readonly Document _doc;
        private readonly WorksetActionHandler _handler = new();
        private readonly ExternalEvent _externalEvent;

        // Captured on the UI thread at construction time. Do NOT use
        // System.Windows.Application.Current.Dispatcher here — Revit doesn't
        // guarantee a System.Windows.Application instance exists in-process,
        // so System.Windows.Application.Current can be null and NullReferenceException.
        private readonly Dispatcher _dispatcher;

        public ObservableCollection<WorksetItem> Items { get; } = new();

        // ── Filtered views ───────────────────────────────────────────────────

        private ICollectionView _grid1AssignedItems;
        public ICollectionView Grid1AssignedItems
        {
            get => _grid1AssignedItems;
            private set => SetProperty(ref _grid1AssignedItems, value);
        }

        private ICollectionView _grid2ActionableItems;
        public ICollectionView Grid2ActionableItems
        {
            get => _grid2ActionableItems;
            private set => SetProperty(ref _grid2ActionableItems, value);
        }

        private ICollectionView _grid3NoInstanceItems;
        public ICollectionView Grid3NoInstanceItems
        {
            get => _grid3NoInstanceItems;
            private set => SetProperty(ref _grid3NoInstanceItems, value);
        }

        // ── Observable properties ────────────────────────────────────────────
        // IsRunning / Progress / SummaryText live on ToolViewModelBase.

        [ObservableProperty] private string pattern              = "+Link({name})";
        [ObservableProperty] private string patternErrorMessage  = string.Empty;
        [ObservableProperty] private bool   hasPatternError;
        [ObservableProperty] private bool   isCreateEnabled      = true;
        [ObservableProperty] private bool   isResyncEnabled      = true;
        [ObservableProperty] private int    grid1Count;
        [ObservableProperty] private int    grid2Count;
        [ObservableProperty] private int    grid3Count;
        [ObservableProperty] private int    totalLinksCount;
        [ObservableProperty] private int    totalSelectedCount;
        [ObservableProperty] private int    grid2SelectedCount;
        [ObservableProperty] private int    grid3SelectedCount;
        [ObservableProperty] private int    grid2ResyncableCount;
        [ObservableProperty] private int    grid3ResyncableCount;
        [ObservableProperty] private string searchText1          = string.Empty;
        [ObservableProperty] private string searchText2          = string.Empty;
        [ObservableProperty] private string searchText3          = string.Empty;
        [ObservableProperty] private bool   isLogExpanded        = false;

        // ── Run summary (NEW in V015) ────────────────────────────────────────

        [ObservableProperty] private RunSummary lastRunSummary;

        /// <summary>True once a run has finished — drives the "Last run" card's visibility.</summary>
        public bool HasLastRun => LastRunSummary != null;

        partial void OnLastRunSummaryChanged(RunSummary value) => OnPropertyChanged(nameof(HasLastRun));

        public string LogChevron => IsLogExpanded ? "▾" : "▸";

        // ── Commands ─────────────────────────────────────────────────────────
        // Log commands (CopyAllLog / CopySelectedLog / ClearLog) come from the base.

        public IRelayCommand CreateGrid2Command  { get; }
        public IRelayCommand CreateGrid3Command  { get; }
        public IRelayCommand ResyncCommand       { get; }
        public IRelayCommand ResyncGrid2Command  { get; }
        public IRelayCommand ResyncGrid3Command  { get; }
        public IRelayCommand CloseCommand        { get; }
        public IRelayCommand SelectAllCommand    { get; }
        public IRelayCommand SelectNoneCommand   { get; }
        public IRelayCommand SelectAllG3Command  { get; }
        public IRelayCommand SelectNoneG3Command { get; }
        public IRelayCommand ToggleLogCommand    { get; }
        public IRelayCommand CopySummaryCommand  { get; }
        public IRelayCommand ShowSummaryCommand  { get; }

        public event Action RequestClose;

        // ── Constructor ──────────────────────────────────────────────────────

        public WorksetCreatorViewModel(ExternalCommandData commandData)
        {
            _uidoc      = commandData.Application.ActiveUIDocument;
            _doc        = _uidoc.Document;
            _dispatcher = Dispatcher.CurrentDispatcher;
            _service    = new WorksetService(LogFromService);
            _externalEvent = ExternalEvent.Create(_handler);

            RecreateFilteredViews();

            CreateGrid2Command   = new RelayCommand(ExecuteCreateGrid2,    CanCreateGrid2);
            CreateGrid3Command   = new RelayCommand(ExecuteCreateGrid3,    CanCreateGrid3);
            ResyncCommand        = new RelayCommand(ExecuteResync,         CanResync);
            ResyncGrid2Command   = new RelayCommand(
                () => ExecuteResyncFor(WorksetGridCategory.NeedsWorkset, "Grid 2"),
                () => CanResyncGrid(WorksetGridCategory.NeedsWorkset));
            ResyncGrid3Command   = new RelayCommand(
                () => ExecuteResyncFor(WorksetGridCategory.NoInstances, "Grid 3"),
                () => CanResyncGrid(WorksetGridCategory.NoInstances));
            CloseCommand         = new RelayCommand(() => RequestClose?.Invoke());
            SelectAllCommand     = new RelayCommand(() => SetGrid2Selection(true));
            SelectNoneCommand    = new RelayCommand(() => SetGrid2Selection(false));
            SelectAllG3Command   = new RelayCommand(() => SetGrid3Selection(true));
            SelectNoneG3Command  = new RelayCommand(() => SetGrid3Selection(false));
            ToggleLogCommand     = new RelayCommand(() =>
            {
                IsLogExpanded = !IsLogExpanded;
                OnPropertyChanged(nameof(LogChevron));
            });
            CopySummaryCommand   = new RelayCommand(
                () => RunSummaryDialog.CopyReport(LastRunSummary),
                () => LastRunSummary != null);
            ShowSummaryCommand   = new RelayCommand(
                () => RunSummaryDialog.Show(LastRunSummary),
                () => LastRunSummary != null);

            Items.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                    foreach (WorksetItem item in e.NewItems)
                        AttachItemHandler(item);
                RefreshCommandStates();
            };

            LoadData();
            ValidatePattern();
        }

        private void AttachItemHandler(WorksetItem item)
        {
            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(WorksetItem.IsSelected))
                    RefreshCommandStates();
            };
        }

        // ── Logging ──────────────────────────────────────────────────────────

        /// <summary>
        /// Service-side logger. The service runs inside the external event (Revit's
        /// UI thread), but the marshal is kept from V014 so a log line can never
        /// touch LogEntries from the wrong thread.
        /// </summary>
        private void LogFromService(LogEntry entry)
        {
            if (entry == null) return;
            _dispatcher.Invoke(() => AddLog(entry.Level, entry.Message), DispatcherPriority.Background);
        }

        private void Log(LogLevel level, string message) => LogFromService(new LogEntry(level, message));

        // ── Filtered view management ─────────────────────────────────────────

        private void RecreateFilteredViews()
        {
            Grid1AssignedItems   = CreateFilteredView(i => i.GridCategory == WorksetGridCategory.AlreadyAssigned,  1);
            Grid2ActionableItems = CreateFilteredView(i => i.GridCategory == WorksetGridCategory.NeedsWorkset,     2);
            Grid3NoInstanceItems = CreateFilteredView(i => i.GridCategory == WorksetGridCategory.NoInstances,      3);
        }

        private ICollectionView CreateFilteredView(Predicate<WorksetItem> categoryFilter, int grid)
        {
            var cvs = new CollectionViewSource { Source = Items };
            cvs.Filter += (s, e) =>
            {
                var item = (WorksetItem)e.Item;
                if (!categoryFilter(item)) { e.Accepted = false; return; }
                string q = grid == 1 ? SearchText1 : grid == 2 ? SearchText2 : SearchText3;
                e.Accepted = string.IsNullOrWhiteSpace(q) ||
                             item.LinkName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
            };
            return cvs.View;
        }

        partial void OnSearchText1Changed(string value) => Grid1AssignedItems?.Refresh();
        partial void OnSearchText2Changed(string value) => Grid2ActionableItems?.Refresh();
        partial void OnSearchText3Changed(string value) => Grid3NoInstanceItems?.Refresh();

        // ── Data loading ─────────────────────────────────────────────────────

        private void LoadData()
        {
            var linkNames = _service.GetLinkedFileNames(_doc);
            int serial    = 1;

            foreach (string linkName in linkNames)
            {
                var item = new WorksetItem
                {
                    SerialNumber  = serial++,
                    LinkName      = linkName,
                    HasInstances  = _service.HasInstances(_doc, linkName),
                    InstanceCount = _service.GetInstanceCount(_doc, linkName)
                };

                string proposedName = AssembleWorksetName(linkName);
                string existingWs   = _service.CheckExistingWorkset(_doc, proposedName);
                item.ExistingWorksetName = existingWs;
                item.IsExistingWorkset   = !string.IsNullOrEmpty(existingWs);

                if (!item.HasInstances)
                {
                    item.GridCategory        = WorksetGridCategory.NoInstances;
                    item.ProposedWorksetName = proposedName;
                    item.IsSelected          = false;
                }
                else
                {
                    item.CurrentWorksetName = _service.GetCurrentWorksetName(_doc, linkName);
                    item.IsMixedWorkset     = item.CurrentWorksetName == "MIXED";

                    if (item.IsExistingWorkset &&
                        _service.IsFullyAssigned(_doc, linkName, existingWs))
                    {
                        item.GridCategory          = WorksetGridCategory.AlreadyAssigned;
                        item.ProposedWorksetName   = existingWs;
                        item.IsSelected            = false;
                        item.IsExactMatchAssigned  = true;
                        item.ProposedWorksetTooltip = $"All instances already assigned to '{existingWs}'";
                    }
                    else
                    {
                        item.GridCategory        = WorksetGridCategory.NeedsWorkset;
                        item.ProposedWorksetName = proposedName;
                        item.IsSelected          = true;
                        if (item.IsExistingWorkset)
                            item.ProposedWorksetTooltip =
                                $"Workset '{existingWs}' exists but assignment is incomplete";
                    }
                }

                Items.Add(item);
            }

            _service.ResolveDuplicates(Items);
            RefreshCounts();
            RefreshCommandStates();

            Log(LogLevel.Info,
                $"Loaded {Items.Count} linked file(s): " +
                $"{Grid1Count} assigned, {Grid2Count} actionable, {Grid3Count} no-instance");
        }

        private void RefreshData()
        {
            _dispatcher.Invoke(() =>
            {
                Log(LogLevel.Info, "Refreshing UI data...");
                Items.Clear();
                LoadData();
                RecreateFilteredViews();
                Log(LogLevel.Info, "UI refresh complete.");
            });
        }

        private void ValidatePattern()
        {
            if (string.IsNullOrWhiteSpace(Pattern) || !Pattern.Contains("{name}"))
            {
                HasPatternError      = true;
                PatternErrorMessage  = "Pattern must contain {name}";
                IsCreateEnabled      = false;
            }
            else
            {
                HasPatternError      = false;
                PatternErrorMessage  = string.Empty;
                IsCreateEnabled      = true;
            }
        }

        partial void OnPatternChanged(string value)
        {
            ValidatePattern();
            UpdateProposedNamesSilent();
            RefreshCommandStates();
        }

        private string AssembleWorksetName(string linkName)
        {
            string cleaned   = _service.CleanName(linkName);
            string assembled = Pattern.Replace("{name}", cleaned);
            return _service.CleanName(assembled);
        }

        private string AssembleWorksetNameSilent(string linkName)
        {
            string cleaned   = _service.CleanNameSilent(linkName);
            string assembled = Pattern.Replace("{name}", cleaned);
            return _service.CleanNameSilent(assembled);
        }

        /// <summary>Updates proposed names without writing to the log (live typing).</summary>
        private void UpdateProposedNamesSilent()
        {
            foreach (var item in Items.Where(i =>
                i.GridCategory != WorksetGridCategory.AlreadyAssigned))
            {
                item.ProposedWorksetName = AssembleWorksetNameSilent(item.LinkName);
            }
        }

        // ── Selection helpers ────────────────────────────────────────────────

        private void SetGrid2Selection(bool selected)
        {
            foreach (var item in Items.Where(i =>
                i.GridCategory == WorksetGridCategory.NeedsWorkset))
                item.IsSelected = selected;
            RefreshCommandStates();
        }

        private void SetGrid3Selection(bool selected)
        {
            foreach (var item in Items.Where(i =>
                i.GridCategory == WorksetGridCategory.NoInstances))
                item.IsSelected = selected;
            RefreshCommandStates();
        }

        // ── Command guards ───────────────────────────────────────────────────

        private bool CanResync() =>
            IsResyncEnabled &&
            Items.Any(i => i.IsSelected && i.IsExistingWorkset &&
                           (i.GridCategory == WorksetGridCategory.NeedsWorkset ||
                            i.GridCategory == WorksetGridCategory.NoInstances));

        private bool CanResyncGrid(WorksetGridCategory category) =>
            IsResyncEnabled && Items.Any(i => i.IsSelected && i.IsExistingWorkset && i.GridCategory == category);

        private bool CanCreateGrid2() =>
            IsCreateEnabled && !HasPatternError &&
            Items.Any(i => i.IsSelected && i.GridCategory == WorksetGridCategory.NeedsWorkset);

        private bool CanCreateGrid3() =>
            IsCreateEnabled && !HasPatternError &&
            Items.Any(i => i.IsSelected && i.GridCategory == WorksetGridCategory.NoInstances);

        // ── Command executions ───────────────────────────────────────────────

        private void RunInRevit(
            List<(string, string, bool)> toProcess,
            RunSummary summary,
            string doneMessage,
            string errorPrefix)
        {
            IsCreateEnabled = false;
            IsResyncEnabled = false;
            IsRunning       = true;
            Progress        = 0;
            SummaryText     = $"{summary.Mode} running — {toProcess.Count} link(s)...";
            RefreshCommandStates();

            _handler.Queue(_ =>
            {
                RunSummary result = summary;
                try
                {
                    result = _service.CreateAndAssign(_doc, toProcess, summary, ReportProgress);
                    Log(LogLevel.Info, doneMessage);
                }
                catch (Exception ex)
                {
                    Log(LogLevel.Error, $"{errorPrefix}: {ex.Message}");
                }
                finally
                {
                    IsCreateEnabled = !HasPatternError;
                    IsResyncEnabled = true;
                    IsRunning       = false;
                    Progress        = 100;
                    RefreshData();
                    PublishSummary(result);
                    RefreshCommandStates();
                }
            });

            ExternalEventRequest request = _externalEvent.Raise();
            if (request != ExternalEventRequest.Accepted)
            {
                Log(LogLevel.Error, $"Revit rejected the request ({request}). Try again.");
                IsCreateEnabled = !HasPatternError;
                IsResyncEnabled = true;
                IsRunning       = false;
                SummaryText     = "Revit rejected the request — nothing was changed.";
                RefreshCommandStates();
            }
        }

        /// <summary>Progress callback handed to the service — (processed, total).</summary>
        private void ReportProgress(int processed, int total)
        {
            if (total > 0) Progress = processed * 100.0 / total;
            if (processed % 5 == 0) KeepUIResponsive();
        }

        /// <summary>
        /// The run report: status strip, the window's "Last run" card, then the
        /// TaskDialog — on the UI thread, after RefreshData, never mid-transaction.
        /// </summary>
        private void PublishSummary(RunSummary summary)
        {
            if (summary == null) return;

            _dispatcher.Invoke(() =>
            {
                LastRunSummary = summary;
                SummaryText    = summary.HeadlineText();
                Log(summary.Errors > 0 ? LogLevel.Error
                        : summary.HasProblems ? LogLevel.Warning
                        : LogLevel.Success,
                    summary.HeadlineText());
                CopySummaryCommand.NotifyCanExecuteChanged();
                ShowSummaryCommand.NotifyCanExecuteChanged();
                RunSummaryDialog.Show(summary);
            });
        }

        private RunSummary NewSummary(string mode, string label, int selectedCount) => new()
        {
            Mode          = mode,
            Label         = label,
            LinksInModel  = Items.Count,
            LinksSelected = selectedCount
        };

        private void ExecuteResync() =>
            ResyncExisting(i => i.GridCategory == WorksetGridCategory.NeedsWorkset ||
                                i.GridCategory == WorksetGridCategory.NoInstances, "Grid 2 & 3");

        private void ExecuteResyncFor(WorksetGridCategory category, string label) =>
            ResyncExisting(i => i.GridCategory == category, label);

        // Resync never creates worksets — it only reassigns links to worksets that already exist.
        private void ResyncExisting(Func<WorksetItem, bool> scope, string label)
        {
            var selected = Items.Where(i => i.IsSelected && scope(i)).ToList();
            var toProcess = selected
                .Where(i => i.IsExistingWorkset)
                .Select(i => (i.ProposedWorksetName, i.LinkName, false))
                .ToList();

            var summary = NewSummary("Resync", label, selected.Count);

            foreach (var skipped in selected.Where(i => !i.IsExistingWorkset))
            {
                Log(LogLevel.Warning,
                    $"Skipped '{skipped.LinkName}' — workset '{skipped.ProposedWorksetName}' does not exist (use Create).");

                summary.Outcomes.Add(new LinkOutcome
                {
                    LinkName       = skipped.LinkName,
                    WorksetName    = skipped.ProposedWorksetName,
                    Outcome        = RunOutcome.Skipped,
                    InstancesTotal = skipped.InstanceCount,
                    Note           = "Workset does not exist — use Create"
                });
            }

            if (!toProcess.Any())
            {
                Log(LogLevel.Warning, $"Nothing to resync in {label} — no checked link has an existing workset.");
                if (summary.Outcomes.Count > 0) PublishSummary(summary);
                return;
            }

            Log(LogLevel.Info, $"Resyncing {toProcess.Count} link(s) from {label}...");
            RunInRevit(toProcess, summary, $"Resync complete for {label}.", "Resync error");
        }

        private void ExecuteCreateGrid2() => ExecuteCreateFor(WorksetGridCategory.NeedsWorkset, "Grid 2");
        private void ExecuteCreateGrid3() => ExecuteCreateFor(WorksetGridCategory.NoInstances,  "Grid 3");

        private void ExecuteCreateFor(WorksetGridCategory category, string label)
        {
            var toProcess = Items
                .Where(i => i.IsSelected && i.GridCategory == category)
                .Select(i => (i.ProposedWorksetName, i.LinkName, CreateNew: !i.IsExistingWorkset))
                .ToList();

            if (!toProcess.Any())
            {
                Log(LogLevel.Warning, $"No {label} links selected for creation.");
                return;
            }

            RunInRevit(toProcess,
                NewSummary("Create", label, toProcess.Count),
                $"Done — created and assigned {toProcess.Count} workset(s) from {label}.", "Error");
        }

        // ── UI helpers ───────────────────────────────────────────────────────

        private void RefreshCounts()
        {
            Grid1Count = Items.Count(i => i.GridCategory == WorksetGridCategory.AlreadyAssigned);
            Grid2Count = Items.Count(i => i.GridCategory == WorksetGridCategory.NeedsWorkset);
            Grid3Count = Items.Count(i => i.GridCategory == WorksetGridCategory.NoInstances);
            TotalLinksCount    = Grid1Count + Grid2Count + Grid3Count;
            Grid2SelectedCount = Items.Count(i => i.IsSelected && i.GridCategory == WorksetGridCategory.NeedsWorkset);
            Grid3SelectedCount = Items.Count(i => i.IsSelected && i.GridCategory == WorksetGridCategory.NoInstances);
            TotalSelectedCount = Grid2SelectedCount + Grid3SelectedCount;
            Grid2ResyncableCount = Items.Count(i => i.IsSelected && i.IsExistingWorkset && i.GridCategory == WorksetGridCategory.NeedsWorkset);
            Grid3ResyncableCount = Items.Count(i => i.IsSelected && i.IsExistingWorkset && i.GridCategory == WorksetGridCategory.NoInstances);
        }

        private void RefreshCommandStates()
        {
            RefreshCounts();
            CreateGrid2Command.NotifyCanExecuteChanged();
            CreateGrid3Command.NotifyCanExecuteChanged();
            ResyncCommand.NotifyCanExecuteChanged();
            ResyncGrid2Command.NotifyCanExecuteChanged();
            ResyncGrid3Command.NotifyCanExecuteChanged();
        }

        public void KeepUIResponsive()
        {
            _dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
        }
    }
}
