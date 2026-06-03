using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.WSFL_010.Models;
using Revit26_Plugin.WSFL_010.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

namespace Revit26_Plugin.WSFL_010.ViewModels
{
    public partial class WorksetsViewModel : ObservableObject
    {
        private readonly UIDocument _uidoc;
        private readonly WorksetService _service;
        private readonly Document _doc;

        public ObservableCollection<WorksetItem> Items { get; } = new();

        // These are now full properties so we can raise PropertyChanged when they are recreated
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

        public ObservableCollection<LogEntry> Log { get; } = new();

        [ObservableProperty]
        private string pattern = "+Link({name})";

        [ObservableProperty]
        private string patternErrorMessage = string.Empty;

        [ObservableProperty]
        private bool hasPatternError;

        [ObservableProperty]
        private bool isCreateEnabled = true;

        [ObservableProperty]
        private bool isResyncEnabled = true;

        [ObservableProperty]
        private int grid1Count;

        [ObservableProperty]
        private int grid2Count;

        [ObservableProperty]
        private int grid3Count;

        [ObservableProperty]
        private int grid2SelectedCount;

        [ObservableProperty]
        private int totalSelectedCount;

        public IRelayCommand CreateCommand { get; }
        public IRelayCommand ResyncCommand { get; }
        public IRelayCommand CloseCommand { get; }
        public IRelayCommand SelectAllCommand { get; }
        public IRelayCommand SelectNoneCommand { get; }

        public event Action RequestClose;

        public WorksetsViewModel(ExternalCommandData commandData)
        {
            _uidoc = commandData.Application.ActiveUIDocument;
            _doc = _uidoc.Document;
            _service = new WorksetService(AddLog);

            // Create initial filtered views
            RecreateFilteredViews();

            CreateCommand = new RelayCommand(ExecuteCreate, CanCreate);
            ResyncCommand = new RelayCommand(ExecuteResync, CanResync);
            CloseCommand = new RelayCommand(() => RequestClose?.Invoke());
            SelectAllCommand = new RelayCommand(() => SetGrid2Selection(true));
            SelectNoneCommand = new RelayCommand(() => SetGrid2Selection(false));

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

        // ---------- Filtered view management ----------
        private void RecreateFilteredViews()
        {
            Grid1AssignedItems = CreateFilteredView(i => i.GridCategory == WorksetGridCategory.AlreadyAssigned);
            Grid2ActionableItems = CreateFilteredView(i => i.GridCategory == WorksetGridCategory.NeedsWorkset);
            Grid3NoInstanceItems = CreateFilteredView(i => i.GridCategory == WorksetGridCategory.NoInstances);
        }

        private ICollectionView CreateFilteredView(Predicate<WorksetItem> filter)
        {
            var cvs = new CollectionViewSource { Source = Items };
            cvs.Filter += (s, e) => e.Accepted = filter((WorksetItem)e.Item);
            return cvs.View;
        }

        // ---------- Data loading / refresh ----------
        private void LoadData()
        {
            var linkNames = _service.GetLinkedFileNames(_doc);
            int serial = 1;

            foreach (string linkName in linkNames)
            {
                var item = new WorksetItem
                {
                    SerialNumber = serial++,
                    LinkName = linkName,
                    HasInstances = _service.HasInstances(_doc, linkName),
                    InstanceCount = _service.GetInstanceCount(_doc, linkName)
                };

                string proposedName = AssembleWorksetName(linkName);
                string existingWs = _service.CheckExistingWorkset(_doc, proposedName);
                item.ExistingWorksetName = existingWs;
                item.IsExistingWorkset = !string.IsNullOrEmpty(existingWs);

                if (!item.HasInstances)
                {
                    item.GridCategory = WorksetGridCategory.NoInstances;
                    item.ProposedWorksetName = proposedName;
                    item.IsSelected = false;
                }
                else
                {
                    item.CurrentWorksetName = _service.GetCurrentWorksetName(_doc, linkName);
                    item.IsMixedWorkset = item.CurrentWorksetName == "MIXED";

                    if (item.IsExistingWorkset &&
                        _service.IsFullyAssigned(_doc, linkName, existingWs))
                    {
                        item.GridCategory = WorksetGridCategory.AlreadyAssigned;
                        item.ProposedWorksetName = existingWs;
                        item.IsSelected = false;
                        item.IsExactMatchAssigned = true;
                        item.ProposedWorksetTooltip = $"All instances already assigned to '{existingWs}'";
                    }
                    else
                    {
                        item.GridCategory = WorksetGridCategory.NeedsWorkset;
                        item.ProposedWorksetName = proposedName;
                        item.IsSelected = true;
                        if (item.IsExistingWorkset)
                            item.ProposedWorksetTooltip = $"Workset '{existingWs}' exists but assignment is incomplete";
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
                // Recreate the filtered views to ensure they are correctly bound
                RecreateFilteredViews();
                AddLog(new LogEntry(LogLevel.Info, "UI refresh complete."));
            });
        }

        // ---------- Pattern handling ----------
        partial void OnPatternChanged(string value)
        {
            ValidatePattern();
            UpdateProposedNames();
            RefreshCommandStates();
        }

        private void ValidatePattern()
        {
            if (string.IsNullOrEmpty(Pattern) || !Pattern.Contains("{name}"))
            {
                HasPatternError = true;
                PatternErrorMessage = "Pattern must contain {name}";
                IsCreateEnabled = false;
            }
            else
            {
                HasPatternError = false;
                PatternErrorMessage = string.Empty;
                IsCreateEnabled = true;
            }
        }

        private string AssembleWorksetName(string linkName)
        {
            string cleaned = _service.CleanName(linkName);
            string assembled = Pattern.Replace("{name}", cleaned);
            return _service.CleanName(assembled);
        }

        private void UpdateProposedNames()
        {
            foreach (var item in Items.Where(i =>
                i.GridCategory != WorksetGridCategory.AlreadyAssigned))
            {
                item.ProposedWorksetName = AssembleWorksetName(item.LinkName);
            }
        }

        private void SetGrid2Selection(bool selected)
        {
            foreach (var item in Items.Where(i =>
                i.GridCategory == WorksetGridCategory.NeedsWorkset))
            {
                item.IsSelected = selected;
            }
            RefreshCommandStates();
        }

        // ---------- Command availability ----------
        private bool CanCreate() =>
            IsCreateEnabled &&
            !HasPatternError &&
            Items.Any(i => i.IsSelected &&
                           (i.GridCategory == WorksetGridCategory.NeedsWorkset ||
                            i.GridCategory == WorksetGridCategory.NoInstances));

        private bool CanResync() =>
            IsResyncEnabled &&
            Items.Any(i => i.GridCategory == WorksetGridCategory.AlreadyAssigned ||
                           (i.IsSelected && i.GridCategory == WorksetGridCategory.NeedsWorkset));

        // ---------- Command executions ----------
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
                RefreshData();          // reload UI after operation
                RefreshCommandStates();
            }
        }

        private void ExecuteResync()
        {
            var toProcess = Items
                .Where(i =>
                    i.GridCategory == WorksetGridCategory.AlreadyAssigned ||
                    (i.IsSelected && i.GridCategory == WorksetGridCategory.NeedsWorkset) ||
                    (i.IsSelected && i.GridCategory == WorksetGridCategory.NoInstances))
                .Select(i => (
                    i.ProposedWorksetName,
                    i.LinkName,
                    CreateNew: i.GridCategory != WorksetGridCategory.AlreadyAssigned && !i.IsExistingWorkset
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

            AddLog(new LogEntry(LogLevel.Info,
                $"Resyncing {toProcess.Count} workset(s)..."));

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
                RefreshData();          // reload UI after operation
                RefreshCommandStates();
            }
        }

        // ---------- UI helpers ----------
        private void RefreshCounts()
        {
            Grid1Count = Items.Count(i => i.GridCategory == WorksetGridCategory.AlreadyAssigned);
            Grid2Count = Items.Count(i => i.GridCategory == WorksetGridCategory.NeedsWorkset);
            Grid3Count = Items.Count(i => i.GridCategory == WorksetGridCategory.NoInstances);
            Grid2SelectedCount = Items.Count(i =>
                i.IsSelected && i.GridCategory == WorksetGridCategory.NeedsWorkset);
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
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background,
                new Action(() => { }));
        }
    }
}