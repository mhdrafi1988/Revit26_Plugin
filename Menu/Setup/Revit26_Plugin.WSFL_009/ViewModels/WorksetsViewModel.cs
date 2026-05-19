using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.WSFL_009.Models;
using Revit26_Plugin.WSFL_009.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace Revit26_Plugin.WSFL_009.ViewModels
{
    public partial class WorksetsViewModel : ObservableObject
    {
        private readonly UIDocument _uidoc;
        private readonly WorksetService _service;
        private readonly Document _doc;

        public ObservableCollection<WorksetItem> Items { get; } = new();
        public ObservableCollection<LogEntry> Log { get; } = new();

        [ObservableProperty]
        private string pattern = "+Link({name})";

        [ObservableProperty]
        private string patternErrorMessage = string.Empty;

        [ObservableProperty]
        private bool hasPatternError;

        [ObservableProperty]
        private bool isCreateEnabled = true;

        public IRelayCommand CreateCommand { get; }
        public IRelayCommand CloseCommand { get; }
        public IRelayCommand SelectAllCommand { get; }
        public IRelayCommand SelectNoneCommand { get; }

        public event Action RequestClose;

        public WorksetsViewModel(ExternalCommandData commandData)
        {
            _uidoc = commandData.Application.ActiveUIDocument;
            _doc = _uidoc.Document;

            _service = new WorksetService(AddLog);

            // Initialize commands first
            CreateCommand = new RelayCommand(CreateWorksets, CanCreateWorksets);
            CloseCommand = new RelayCommand(() => RequestClose?.Invoke());
            SelectAllCommand = new RelayCommand(() => SetAllSelections(true));
            SelectNoneCommand = new RelayCommand(() => SetAllSelections(false));

            // Attach collection changed handler
            Items.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (WorksetItem item in e.NewItems)
                    {
                        AttachItemHandler(item);
                    }
                }
                CreateCommand.NotifyCanExecuteChanged();
            };

            // Load items
            LoadItems();
            ValidatePattern();
        }

        private void AttachItemHandler(WorksetItem item)
        {
            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(WorksetItem.IsSelected))
                {
                    System.Diagnostics.Debug.WriteLine($"PropertyChanged fired for {item.LinkName}, IsSelected = {item.IsSelected}");
                    CreateCommand.NotifyCanExecuteChanged();
                }
            };
        }

        private bool CanCreateWorksets()
        {
            bool canCreate = IsCreateEnabled && Items.Any(i => i.IsSelected && !i.IsExistingWorkset);
            System.Diagnostics.Debug.WriteLine($"CanCreateWorksets: {canCreate} (IsCreateEnabled={IsCreateEnabled}, SelectedCount={Items.Count(i => i.IsSelected && !i.IsExistingWorkset)})");
            return canCreate;
        }

        partial void OnPatternChanged(string value)
        {
            ValidatePattern();
            UpdateProposedNames();
            CreateCommand.NotifyCanExecuteChanged();
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
            CreateCommand.NotifyCanExecuteChanged();
        }

        private void LoadItems()
        {
            var linkNames = _service.GetLinkedFileNames(_doc);
            int serialNumber = 1;

            foreach (string linkName in linkNames)
            {
                var item = new WorksetItem
                {
                    SerialNumber = serialNumber++,
                    LinkName = linkName,
                    HasInstances = _service.HasInstances(_doc, linkName)
                };

                item.CurrentWorksetName = _service.GetCurrentWorksetName(_doc, linkName);
                item.IsMixedWorkset = item.CurrentWorksetName == "MIXED";

                string proposedName = AssembleWorksetName(linkName);
                item.ExistingWorksetName = _service.CheckExistingWorkset(_doc, proposedName);
                item.IsExistingWorkset = !string.IsNullOrEmpty(item.ExistingWorksetName);

                if (item.IsExistingWorkset)
                {
                    item.IsSelected = false;
                    item.ProposedWorksetName = $"{proposedName} (exists)";
                    item.ProposedWorksetTooltip = $"Workset '{item.ExistingWorksetName}' already exists";
                }
                else
                {
                    // Default select new worksets that have instances
                    item.IsSelected = item.HasInstances;
                    item.ProposedWorksetName = proposedName;
                }

                Items.Add(item);
            }

            _service.ResolveDuplicates(Items);

            // Refresh command state after loading
            CreateCommand.NotifyCanExecuteChanged();
        }

        private string AssembleWorksetName(string linkName)
        {
            string cleanedLinkName = _service.CleanName(linkName);
            string assembled = Pattern.Replace("{name}", cleanedLinkName);
            return _service.CleanName(assembled);
        }

        private void UpdateProposedNames()
        {
            foreach (var item in Items.Where(i => !i.IsExistingWorkset))
            {
                string proposed = AssembleWorksetName(item.LinkName);
                item.ProposedWorksetName = proposed;
            }
        }

        private void SetAllSelections(bool selected)
        {
            foreach (var item in Items.Where(i => !i.IsExistingWorkset && i.HasInstances))
            {
                item.IsSelected = selected;
            }
            // Notify is already handled by PropertyChanged event
            CreateCommand.NotifyCanExecuteChanged();
        }

        private void CreateWorksets()
        {
            var selected = Items
                .Where(i => i.IsSelected && !i.IsExistingWorkset)
                .Select(i => (ProposedName: i.ProposedWorksetName, LinkName: i.LinkName))
                .ToList();

            if (!selected.Any())
            {
                AddLog(new LogEntry(LogLevel.Warning, "No links selected."));
                return;
            }

            IsCreateEnabled = false;
            CreateCommand.NotifyCanExecuteChanged();

            try
            {
                _service.CreateAndAssign(_doc, selected, this);
                AddLog(new LogEntry(LogLevel.Info, $"Successfully created {selected.Count} workset(s)."));
            }
            catch (Exception ex)
            {
                AddLog(new LogEntry(LogLevel.Error, $"Error creating worksets: {ex.Message}"));
            }
            finally
            {
                IsCreateEnabled = true;
                CreateCommand.NotifyCanExecuteChanged();
            }
        }

        public void AddLog(LogEntry entry)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Log.Add(entry);
            }, DispatcherPriority.Background);
        }

        public void KeepUIResponsive()
        {
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
        }
    }
}