using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.WorksetManager_06.Models;
using Revit26_Plugin.WorksetManager_06.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.WorksetManager_06.ViewModels
{
    public partial class WorksetsViewModel : ObservableObject
    {
        private readonly UIDocument _uidoc;
        private readonly WorksetService _service;
        private readonly Document _doc;

        public ObservableCollection<WorksetItem> Items { get; } = new();
        public ObservableCollection<LogEntry>    Log   { get; } = new();

        // ── Filtered views ────────────────────────────────────────────────────

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

        // ── Observable properties ─────────────────────────────────────────────

        [ObservableProperty] private string pattern              = "+Link({name})";
        [ObservableProperty] private string patternErrorMessage  = string.Empty;
        [ObservableProperty] private bool   hasPatternError;
        [ObservableProperty] private bool   isCreateEnabled      = true;
        [ObservableProperty] private bool   isResyncEnabled      = true;
        [ObservableProperty] private int    grid1Count;
        [ObservableProperty] private int    grid2Count;
        [ObservableProperty] private int    grid3Count;
        [ObservableProperty] private int    totalSelectedCount;

        // ── Commands ──────────────────────────────────────────────────────────

        public IRelayCommand CreateCommand       { get; }
        public IRelayCommand ResyncCommand       { get; }
        public IRelayCommand CloseCommand        { get; }
        public IRelayCommand SelectAllCommand    { get; }
        public IRelayCommand SelectNoneCommand   { get; }
        public IRelayCommand SelectAllG3Command  { get; }
        public IRelayCommand SelectNoneG3Command { get; }
        public IRelayCommand CopyLogCommand      { get; }

        public event Action RequestClose;

        // ── Constructor ───────────────────────────────────────────────────────

        public WorksetsViewModel(ExternalCommandData commandData)
        {
            _uidoc   = commandData.Application.ActiveUIDocument;
            _doc     = _uidoc.Document;
            _service = new WorksetService(AddLog);

            RecreateFilteredViews();

            CreateCommand       = new RelayCommand(ExecuteCreate,        CanCreate);
            ResyncCommand       = new RelayCommand(ExecuteResync,        CanResync);
            CloseCommand        = new RelayCommand(() => RequestClose?.Invoke());
            SelectAllCommand    = new RelayCommand(() => SetGrid2Selection(true));
            SelectNoneCommand   = new RelayCommand(() => SetGrid2Selection(false));
            SelectAllG3Command  = new RelayCommand(() => SetGrid3Selection(true));
            SelectNoneG3Command = new RelayCommand(() => SetGrid3Selection(false));
            CopyLogCommand      = new RelayCommand(ExecuteCopyLog);

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

        // ── Filtered view management ──────────────────────────────────────────

        private void RecreateFilteredViews()
        {
            Grid1AssignedItems  = CreateFilteredView(i => i.GridCategory == WorksetGridCategory.AlreadyAssigned);
            Grid2ActionableItems = CreateFilteredView(i => i.GridCategory == WorksetGridCategory.NeedsWorkset);
            Grid3NoInstanceItems = CreateFilteredView(i => i.GridCategory == WorksetGridCategory.NoInstances);
        }

        private ICollectionView CreateFilteredView(Predicate<WorksetItem> filter)
        {
            var cvs = new CollectionViewSource { Source = Items };
            cvs.Filter += (s, e) => e.Accepted = filter((WorksetItem)e.Item);
            return cvs.View;
        }

        // ── Data loading ──────────────────────────────────────────────────────

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

            AddLog(new LogEntry(LogLevel.Info,
                $"Loaded {Items.Count} linked file(s): " +
                $"{Grid1Count} assigned, {Grid2Count} actionable, {Grid3Count} no-instance"));
        }

        private void RefreshData()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                AddLog(new LogEntry(LogLevel.Info, "Refreshing UI data..."));
                Items.Clear();
                LoadData();
                RecreateFilteredViews();
                AddLog(new LogEntry(LogLevel.Info, "UI refresh complete."));
            });
        }

        // ── Pattern handling ──────────────────────────────────────────────────

        partial void OnPatternChanged(string value)
        {
            ValidatePattern();
            UpdateProposedNamesSilent();   // silent — no log noise per keystroke
            RefreshCommandStates();
        }

        private void ValidatePattern()
        {
            if (string.IsNullOrEmpty(Pattern) || !Pattern.Contains("{name}"))
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

        // ── Selection helpers ─────────────────────────────────────────────────

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

        // ── Command guards ────────────────────────────────────────────────────

        private bool CanCreate() =>
            IsCreateEnabled &&
            !HasPatternError &&
            Items.Any(i => i.IsSelected &&
                           (i.GridCategory == WorksetGridCategory.NeedsWorkset ||
                            i.GridCategory == WorksetGridCategory.NoInstances));

        private bool CanResync() =>
            IsResyncEnabled &&
            Items.Any(i => i.IsSelected &&
                           (i.GridCategory == WorksetGridCategory.NeedsWorkset ||
                            i.GridCategory == WorksetGridCategory.NoInstances ||
                            i.GridCategory == WorksetGridCategory.AlreadyAssigned));

        // ── Command executions ────────────────────────────────────────────────

        private void ExecuteCreate()
        {
            var toProcess = Items
                .Where(i => i.IsSelected &&
                            (i.GridCategory == WorksetGridCategory.NeedsWorkset ||
                             i.GridCategory == WorksetGridCategory.NoInstances))
                .Select(i => (i.ProposedWorksetName, i.LinkName, CreateNew: !i.IsExistingWorkset))
                .ToList();

            if (!toProcess.Any())
            {
                AddLog(new LogEntry(LogLevel.Warning, "No links selected for creation."));
                return;
            }

            IsCreateEnabled = false;
            IsResyncEnabled = false;
            RefreshCommandStates();

            try
            {
                _service.CreateAndAssign(_doc, toProcess, this);
                AddLog(new LogEntry(LogLevel.Info,
                    $"Done — created and assigned {toProcess.Count} workset(s)."));
            }
            catch (Exception ex)
            {
                AddLog(new LogEntry(LogLevel.Error, $"Error: {ex.Message}"));
            }
            finally
            {
                IsCreateEnabled = true;
                IsResyncEnabled = true;
                RefreshData();
                RefreshCommandStates();
            }
        }

        private void ExecuteResync()
        {
            // Resync scope: Grid 2 & Grid 3 selected items only.
            // Grid 1 items are already correctly assigned — don't touch them.
            var toProcess = Items
                .Where(i => i.IsSelected &&
                            (i.GridCategory == WorksetGridCategory.NeedsWorkset ||
                             i.GridCategory == WorksetGridCategory.NoInstances))
                .Select(i => (
                    i.ProposedWorksetName,
                    i.LinkName,
                    CreateNew: !i.IsExistingWorkset
                ))
                .ToList();

            if (!toProcess.Any())
            {
                AddLog(new LogEntry(LogLevel.Warning, "Nothing to resync."));
                return;
            }

            IsCreateEnabled = false;
            IsResyncEnabled = false;
            RefreshCommandStates();

            AddLog(new LogEntry(LogLevel.Info, $"Resyncing {toProcess.Count} workset(s)..."));

            try
            {
                _service.CreateAndAssign(_doc, toProcess, this);
                AddLog(new LogEntry(LogLevel.Info, "Resync complete."));
            }
            catch (Exception ex)
            {
                AddLog(new LogEntry(LogLevel.Error, $"Resync error: {ex.Message}"));
            }
            finally
            {
                IsCreateEnabled = true;
                IsResyncEnabled = true;
                RefreshData();
                RefreshCommandStates();
            }
        }

        private void ExecuteCopyLog()
        {
            if (!Log.Any()) return;

            // Log is stored newest-first; reverse so oldest is at top of clipboard text
            var sb = new StringBuilder();
            foreach (var entry in Log.Reverse())
                sb.AppendLine(entry.ToString());

            try
            {
                Clipboard.SetText(sb.ToString());
                AddLog(new LogEntry(LogLevel.Info, "Log copied to clipboard."));
            }
            catch (Exception ex)
            {
                AddLog(new LogEntry(LogLevel.Error, $"Copy failed: {ex.Message}"));
            }
        }

        // ── UI helpers ────────────────────────────────────────────────────────

        private void RefreshCounts()
        {
            Grid1Count = Items.Count(i => i.GridCategory == WorksetGridCategory.AlreadyAssigned);
            Grid2Count = Items.Count(i => i.GridCategory == WorksetGridCategory.NeedsWorkset);
            Grid3Count = Items.Count(i => i.GridCategory == WorksetGridCategory.NoInstances);
            TotalSelectedCount = Items.Count(i => i.IsSelected &&
                                                  (i.GridCategory == WorksetGridCategory.NeedsWorkset ||
                                                   i.GridCategory == WorksetGridCategory.NoInstances));
        }

        private void RefreshCommandStates()
        {
            RefreshCounts();
            CreateCommand.NotifyCanExecuteChanged();
            ResyncCommand.NotifyCanExecuteChanged();
        }

        public void AddLog(LogEntry entry)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Log.Insert(0, entry);
            }, DispatcherPriority.Background);
        }

        public void KeepUIResponsive()
        {
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
        }
    }
}
