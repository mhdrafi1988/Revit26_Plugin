using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Revit26_Plugin.ParaManager.V003.Models;
using Revit26_Plugin.ParaManager.V003.Services;
using Revit26_Plugin.Shared.Models;
using Revit26_Plugin.Shared.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace Revit26_Plugin.ParaManager.V003.ViewModels
{
    public partial class ParaManagerViewModel : ObservableObject
    {
        private const string ToolFolderName = "ParaManager";

        private readonly ParaManagerExternalEventHandler _eventHandler;
        private readonly ExternalEvent _externalEvent;
        private readonly ParaManagerSettings _settings;

        // ── Shared Parameter File ────────────────────────────────────────
        [ObservableProperty]
        private string _sharedParameterFilePath = string.Empty;

        private List<SharedParameterInfo> _allParameters = new();

        // ── Categories ────────────────────────────────────────────────────
        public ObservableCollection<CategoryInfo> AvailableCategories { get; } = new();
        public ObservableCollection<CategoryInfo> SelectedCategories { get; } = new();

        [ObservableProperty]
        private bool _isCategoryPopupOpen;

        // ── Parameter popover ─────────────────────────────────────────────
        public ObservableCollection<ParameterGroupNode> ParameterGroups { get; } = new();

        [ObservableProperty]
        private bool _isParameterPopoverOpen;

        [ObservableProperty]
        private string _parameterFilterText = string.Empty;

        // ── Assignment Queue (Advanced DataGrid) ─────────────────────────
        public ObservableCollection<ParameterAssignmentRow> QueueRows { get; } = new();
        public ICollectionView QueueView { get; }

        [ObservableProperty]
        private string _queueFilterText = string.Empty;

        // ── Log ───────────────────────────────────────────────────────────
        public ObservableCollection<LogEntry> LogEntries { get; } = new();
        public ObservableCollection<LogEntry> SelectedLogEntries { get; } = new();

        [ObservableProperty]
        private bool _isLogExpanded;

        [ObservableProperty]
        private int _unreadLogCount;

        // ── Wizard step ───────────────────────────────────────────────────
        [ObservableProperty]
        private int _currentStep = 1;

        public bool IsStep1 => CurrentStep == 1;
        public bool IsStep2 => CurrentStep == 2;
        public bool IsStep3 => CurrentStep == 3;
        public bool IsStep4 => CurrentStep == 4;

        partial void OnCurrentStepChanged(int value)
        {
            OnPropertyChanged(nameof(IsStep1));
            OnPropertyChanged(nameof(IsStep2));
            OnPropertyChanged(nameof(IsStep3));
            OnPropertyChanged(nameof(IsStep4));
            NextStepCommand.NotifyCanExecuteChanged();
            PreviousStepCommand.NotifyCanExecuteChanged();
        }

        partial void OnMetricParamsFileCountChanged(int value) => NextStepCommand.NotifyCanExecuteChanged();
        partial void OnMetricCategoriesSelectedChanged(int value) => NextStepCommand.NotifyCanExecuteChanged();
        partial void OnMetricParametersSelectedChanged(int value) => NextStepCommand.NotifyCanExecuteChanged();

        private bool CanGoNext() => CurrentStep switch
        {
            1 => !string.IsNullOrWhiteSpace(SharedParameterFilePath),
            2 => SelectedCategories.Count > 0,
            3 => QueueRows.Count > 0,
            _ => false
        };

        [RelayCommand(CanExecute = nameof(CanGoNext))]
        private void NextStep()
        {
            if (CurrentStep >= 4) return;
            CurrentStep++;
        }

        private bool CanGoBack() => CurrentStep > 1;

        [RelayCommand(CanExecute = nameof(CanGoBack))]
        private void PreviousStep()
        {
            if (CurrentStep <= 1) return;
            CurrentStep--;
        }

        [RelayCommand]
        private void GoToStep(string stepNumber)
        {
            if (int.TryParse(stepNumber, out var step) && step >= 1 && step <= 4)
                CurrentStep = step;
        }

        [RelayCommand]
        private void ToggleLogExpanded()
        {
            IsLogExpanded = !IsLogExpanded;
            if (IsLogExpanded) UnreadLogCount = 0;
        }

        // ── Run state ─────────────────────────────────────────────────────
        [ObservableProperty]
        private bool _isRunning;

        [ObservableProperty]
        private string _summaryLine = "0 queued | 0 assigned | 0 failed";

        // ── Metrics (top cards) ─────────────────────────────────────────
        [ObservableProperty] private int _metricParamsFileCount;
        [ObservableProperty] private int _metricCategoriesSelected;
        [ObservableProperty] private int _metricParametersSelected;
        [ObservableProperty] private int _metricAssignedThisSession;

        public ParaManagerViewModel(ParaManagerExternalEventHandler eventHandler, ExternalEvent externalEvent)
        {
            _eventHandler = eventHandler;
            _externalEvent = externalEvent;
            _eventHandler.Completed += OnRunCompleted;

            QueueView = CollectionViewSource.GetDefaultView(QueueRows);
            QueueView.Filter = FilterQueueRow;

            foreach (var cat in CategoryProvider.GetAvailableCategories())
                AvailableCategories.Add(cat);

            _settings = SettingsService<ParaManagerSettings>.Load(ToolFolderName);
            RestoreLastSession();

            Log(LogLevel.Info, "ParaManager ready.");
        }

        // ─────────────────────────────────────────────────────────────────
        // Shared Parameter File
        // ─────────────────────────────────────────────────────────────────

        [RelayCommand]
        private void BrowseSharedParameterFile()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Shared Parameter File",
                Filter = "Shared Parameter Files (*.txt)|*.txt|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() != true) return;

            LoadSharedParameterFile(dlg.FileName);
        }

        [RelayCommand]
        private void ReloadSharedParameterFile()
        {
            if (string.IsNullOrWhiteSpace(SharedParameterFilePath))
            {
                Log(LogLevel.Warning, "No shared parameter file selected yet.");
                return;
            }
            LoadSharedParameterFile(SharedParameterFilePath);
        }

        private void LoadSharedParameterFile(string path)
        {
            try
            {
                _allParameters = SharedParameterFileParser.Parse(path);
                SharedParameterFilePath = path;
                MetricParamsFileCount = 1;

                RebuildParameterGroups();
                Log(LogLevel.Info, $"Shared parameter file loaded — {_allParameters.Count} parameters found.");

                _settings.LastSharedParameterFilePath = path;
                SettingsService<ParaManagerSettings>.Save(ToolFolderName, _settings);
            }
            catch (Exception ex)
            {
                MetricParamsFileCount = 0;
                Log(LogLevel.Error, $"Failed to load shared parameter file — {ex.Message}");
                MessageBox.Show($"Could not load shared parameter file:\n{ex.Message}",
                    "ParaManager", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RebuildParameterGroups()
        {
            ParameterGroups.Clear();
            foreach (var group in SharedParameterFileParser.GroupByParameterGroup(_allParameters))
            {
                var node = new ParameterGroupNode(group.Key);
                foreach (var p in group)
                    node.AddParameter(new ParameterLeafNode(p));
                ParameterGroups.Add(node);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Categories (multi-select)
        // ─────────────────────────────────────────────────────────────────

        [RelayCommand]
        private void ToggleCategoryPopup() => IsCategoryPopupOpen = !IsCategoryPopupOpen;

        [RelayCommand]
        private void ToggleCategory(CategoryInfo category)
        {
            if (SelectedCategories.Contains(category))
                SelectedCategories.Remove(category);
            else
                SelectedCategories.Add(category);

            MetricCategoriesSelected = SelectedCategories.Count;
        }

        [RelayCommand]
        private void RemoveCategory(CategoryInfo category)
        {
            SelectedCategories.Remove(category);
            MetricCategoriesSelected = SelectedCategories.Count;
        }

        [RelayCommand]
        private void ClearCategories()
        {
            SelectedCategories.Clear();
            MetricCategoriesSelected = 0;
        }

        // ─────────────────────────────────────────────────────────────────
        // Parameter popover
        // ─────────────────────────────────────────────────────────────────

        [RelayCommand]
        private void OpenParameterPopover()
        {
            if (_allParameters.Count == 0)
            {
                Log(LogLevel.Warning, "Load a shared parameter file before selecting parameters.");
                return;
            }
            IsParameterPopoverOpen = true;
        }

        [RelayCommand]
        private void SelectAllParameters()
        {
            foreach (var group in ParameterGroups)
                group.IsChecked = true;
        }

        [RelayCommand]
        private void ClearParameterSelection()
        {
            foreach (var group in ParameterGroups)
                group.IsChecked = false;
        }

        [RelayCommand]
        private void CancelParameterPopover() => IsParameterPopoverOpen = false;

        [RelayCommand]
        private void ApplyParameterPopover()
        {
            if (SelectedCategories.Count == 0)
            {
                Log(LogLevel.Warning, "Select at least one category before applying parameters.");
                return;
            }

            var checkedParams = ParameterGroups
                .SelectMany(g => g.Parameters)
                .Where(p => p.IsChecked)
                .Select(p => p.Parameter)
                .ToList();

            if (checkedParams.Count == 0)
            {
                Log(LogLevel.Warning, "No parameters selected.");
                return;
            }

            var categoriesSnapshot = SelectedCategories.ToList();

            foreach (var param in checkedParams)
            {
                // Avoid queueing an exact duplicate (same parameter, same category set) twice.
                bool alreadyQueued = QueueRows.Any(r =>
                    r.Parameter.Guid == param.Guid &&
                    r.Categories.Select(c => c.Name).OrderBy(n => n)
                        .SequenceEqual(categoriesSnapshot.Select(c => c.Name).OrderBy(n => n)));

                if (alreadyQueued) continue;

                QueueRows.Add(new ParameterAssignmentRow(param, categoriesSnapshot));
            }

            RecalculateParametersSelectedMetric();
            UpdateSummaryLine();
            IsParameterPopoverOpen = false;

            Log(LogLevel.Info, $"{checkedParams.Count} parameter(s) staged for assignment across {categoriesSnapshot.Count} categor{(categoriesSnapshot.Count == 1 ? "y" : "ies")}.");
        }

        // ─────────────────────────────────────────────────────────────────
        // Queue grid (filter / select all / clear / refresh)
        // ─────────────────────────────────────────────────────────────────

        partial void OnQueueFilterTextChanged(string value) => QueueView.Refresh();

        private bool FilterQueueRow(object obj)
        {
            if (obj is not ParameterAssignmentRow row) return false;
            if (string.IsNullOrWhiteSpace(QueueFilterText)) return true;

            var term = QueueFilterText.Trim();
            return row.ParameterName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || row.ParameterGroup.Contains(term, StringComparison.OrdinalIgnoreCase)
                || row.CategoryDisplay.Contains(term, StringComparison.OrdinalIgnoreCase);
        }

        [RelayCommand]
        private void SelectAllQueueRows()
        {
            // Scoped to visible/filtered rows only, per Advanced DataGrid spec.
            foreach (var item in QueueView.Cast<ParameterAssignmentRow>())
                item.IsSelected = true;
        }

        [RelayCommand]
        private void ClearQueueSelection()
        {
            foreach (var item in QueueView.Cast<ParameterAssignmentRow>())
                item.IsSelected = false;
        }

        [RelayCommand]
        private void RefreshQueue() => QueueView.Refresh();

        [RelayCommand]
        private void RemoveQueueRow(ParameterAssignmentRow row)
        {
            QueueRows.Remove(row);
            RecalculateParametersSelectedMetric();
            UpdateSummaryLine();
        }

        /// <summary>Keeps the "Parameters Selected" metric card in sync with the live
        /// queue (distinct parameters across all rows), rather than just the size of
        /// the last-applied batch.</summary>
        private void RecalculateParametersSelectedMetric()
            => MetricParametersSelected = QueueRows.Select(r => r.Parameter.Guid).Distinct().Count();

        // ─────────────────────────────────────────────────────────────────
        // Run
        // ─────────────────────────────────────────────────────────────────

        private bool CanRun() => !IsRunning && QueueRows.Any(r => r.IsSelected);

        [RelayCommand(CanExecute = nameof(CanRun))]
        private void Run()
        {
            var selectedRows = QueueRows.Where(r => r.IsSelected).ToList();
            if (selectedRows.Count == 0)
            {
                Log(LogLevel.Warning, "No rows selected to run.");
                return;
            }

            // One binding-type prompt per Run click, applied to the whole batch.
            var choice = PromptBindingChoice();
            if (choice == null)
            {
                Log(LogLevel.Info, "Run cancelled — no binding type chosen.");
                return;
            }

            IsRunning = true;
            RunCommand.NotifyCanExecuteChanged();
            Log(LogLevel.Info, $"Run started — {selectedRows.Count} parameter(s), binding: {choice}.");

            _eventHandler.SharedParameterFilePath = SharedParameterFilePath;
            _eventHandler.RowsToAssign = selectedRows;
            _eventHandler.Binding = choice.Value;
            _externalEvent.Raise();
        }

        private BindingChoice? PromptBindingChoice()
        {
            var result = MessageBox.Show(
                "Choose binding type for this batch:\n\nYes = Instance binding\nNo = Type binding\nCancel = abort run",
                "ParaManager — Binding Type",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            return result switch
            {
                MessageBoxResult.Yes => BindingChoice.Instance,
                MessageBoxResult.No => BindingChoice.Type,
                _ => null
            };
        }

        private void OnRunCompleted(List<RowResult> results, Exception failure)
        {
            // Marshal back if needed — Completed is invoked from the API thread's
            // synchronization context set up by ExternalEvent, safe to touch UI-bound
            // collections directly here per standard IExternalEventHandler pattern.
            IsRunning = false;
            RunCommand.NotifyCanExecuteChanged();

            if (failure != null)
            {
                Log(LogLevel.Error, $"Run failed — {failure.Message}");
                MessageBox.Show($"ParaManager run failed:\n{failure.Message}",
                    "ParaManager", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int assigned = 0, skipped = 0, failedCount = 0;

            foreach (var r in results)
            {
                switch (r.Outcome)
                {
                    case RowOutcome.Assigned:
                        assigned++;
                        Log(LogLevel.Success, r.Message);
                        break;
                    case RowOutcome.SkippedDuplicate:
                        skipped++;
                        Log(LogLevel.Warning, r.Message);
                        break;
                    case RowOutcome.Failed:
                        failedCount++;
                        Log(LogLevel.Error, r.Message);
                        break;
                }
            }

            MetricAssignedThisSession += assigned;
            SummaryLine = $"{QueueRows.Count} queued | {assigned} assigned | {skipped} skipped | {failedCount} failed";
            Log(LogLevel.Success, $"Run complete — {assigned} assigned | {skipped} skipped | {failedCount} failed.");

            AutoSaveLog();
        }

        private void UpdateSummaryLine()
            => SummaryLine = $"{QueueRows.Count} queued | 0 assigned | 0 skipped | 0 failed";

        // ─────────────────────────────────────────────────────────────────
        // Log
        // ─────────────────────────────────────────────────────────────────

        private void Log(LogLevel level, string message)
        {
            LogEntries.Add(new LogEntry(level, message));
            if (!IsLogExpanded) UnreadLogCount++;
        }

        [RelayCommand]
        private void CopyAllLog()
        {
            var text = string.Join(Environment.NewLine, LogEntries.Select(e => e.ToString()));
            SafeSetClipboard(text);
        }

        [RelayCommand]
        private void CopySelectedLog()
        {
            var text = string.Join(Environment.NewLine, SelectedLogEntries.Select(e => e.ToString()));
            SafeSetClipboard(text);
        }

        private void SafeSetClipboard(string text)
        {
            try { Clipboard.SetText(text ?? string.Empty); }
            catch { /* clipboard can be locked by another process — non-fatal */ }
        }

        [RelayCommand]
        private void ExportLogs()
        {
            try
            {
                string folder = _settings.LastLogExportFolder;

                if (string.IsNullOrWhiteSpace(folder) || !System.IO.Directory.Exists(folder))
                {
                    var dlg = new OpenFolderDialog { Title = "Select folder to save ParaManager logs" };
                    if (dlg.ShowDialog() != true) return;
                    folder = dlg.FolderName;

                    _settings.LastLogExportFolder = folder;
                    SettingsService<ParaManagerSettings>.Save(ToolFolderName, _settings);
                }

                var path = LogExportService.Export(ToolFolderName, LogEntries, folder);
                Log(LogLevel.Success, $"Log exported to {path}");
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Log export failed — {ex.Message}");
            }
        }

        private void AutoSaveLog()
        {
            try
            {
                string folder = string.IsNullOrWhiteSpace(_settings.LastLogExportFolder)
                    ? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Revit26_Plugin", "ParaManager", "Logs")
                    : _settings.LastLogExportFolder;

                LogExportService.Export(ToolFolderName, LogEntries, folder);
            }
            catch
            {
                // Auto-save is best-effort; manual Export Logs remains available if this fails.
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Session restore
        // ─────────────────────────────────────────────────────────────────

        private void RestoreLastSession()
        {
            if (!string.IsNullOrWhiteSpace(_settings.LastSharedParameterFilePath)
                && System.IO.File.Exists(_settings.LastSharedParameterFilePath))
            {
                LoadSharedParameterFile(_settings.LastSharedParameterFilePath);
            }

            if (_settings.LastSelectedCategoryNames?.Length > 0)
            {
                foreach (var name in _settings.LastSelectedCategoryNames)
                {
                    var match = AvailableCategories.FirstOrDefault(c => c.Name == name);
                    if (match != null) SelectedCategories.Add(match);
                }
                MetricCategoriesSelected = SelectedCategories.Count;
            }
        }

        /// <summary>Call from the Window's Closing event to persist category selection.</summary>
        public void OnWindowClosing()
        {
            _settings.LastSelectedCategoryNames = SelectedCategories.Select(c => c.Name).ToArray();
            SettingsService<ParaManagerSettings>.Save(ToolFolderName, _settings);
        }
    }
}
