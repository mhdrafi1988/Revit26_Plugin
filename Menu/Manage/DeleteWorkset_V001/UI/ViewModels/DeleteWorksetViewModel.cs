using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.DeleteWorkset.V001.Core.Models;
using Revit26_Plugin.DeleteWorkset.V001.Core.Services;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

namespace Revit26_Plugin.DeleteWorkset.V001.UI.ViewModels
{
    public partial class DeleteWorksetViewModel : ObservableObject
    {
        // ── Revit context ─────────────────────────────────────────────────────

        private readonly UIDocument   _uidoc;
        private readonly Document     _doc;
        private readonly DeleteWorksetHandler  _handler = new();
        private readonly ExternalEvent _externalEvent;

        // Captured on the UI thread; see WorksetsViewModel.cs comment on why
        // System.Windows.Application.Current is not used here.
        private readonly Dispatcher _dispatcher;

        // ── Collections ───────────────────────────────────────────────────────

        public ObservableCollection<WorksetRow>   Rows            { get; } = new();
        public ObservableCollection<WorksetRow>   MigrationTargets { get; } = new();
        public ObservableCollection<LogEntry>     Log             { get; } = new();

        private ICollectionView _rowsView;
        public  ICollectionView RowsView
        {
            get => _rowsView;
            private set => SetProperty(ref _rowsView, value);
        }

        // ── Observable properties ─────────────────────────────────────────────

        [ObservableProperty] private string filterText = string.Empty;
        [ObservableProperty] private int    totalCount;
        [ObservableProperty] private int    deletableCount;
        [ObservableProperty] private int    nonDeletableCount;

        [ObservableProperty] private WorksetRow migrationTarget;
        [ObservableProperty] private bool   hardDeleteUnmigratable;
        [ObservableProperty] private bool   confirmEachDeletion = true;

        [ObservableProperty] private bool   isRunning;
        [ObservableProperty] private bool   isLogExpanded = true;

        // ── Constructor ───────────────────────────────────────────────────────

        public DeleteWorksetViewModel(ExternalCommandData commandData)
        {
            _uidoc        = commandData.Application.ActiveUIDocument;
            _doc          = _uidoc.Document;
            _dispatcher   = Dispatcher.CurrentDispatcher;
            _externalEvent = ExternalEvent.Create(_handler);

            Refresh();
        }

        // ── Property change hooks ─────────────────────────────────────────────

        partial void OnFilterTextChanged(string value)
            => RowsView?.Refresh();

        partial void OnMigrationTargetChanged(WorksetRow value)
        {
            // Keep MigrationTargets in sync with what is selected for deletion.
            RebuildMigrationTargets();
            DeleteCommand.NotifyCanExecuteChanged();
        }

        // ── Commands ──────────────────────────────────────────────────────────

        [RelayCommand]
        private void Refresh()
        {
            var svc = new DeleteWorksetService(AddLog);
            var rows = svc.LoadWorksets(_doc);

            Rows.Clear();
            foreach (var r in rows)
            {
                r.PropertyChanged += OnRowPropertyChanged;
                Rows.Add(r);
            }

            // Filtered view
            var cv = CollectionViewSource.GetDefaultView(Rows);
            cv.Filter = obj =>
            {
                if (obj is not WorksetRow row) return false;
                if (string.IsNullOrWhiteSpace(FilterText)) return true;
                return row.Name.IndexOf(FilterText, StringComparison.OrdinalIgnoreCase) >= 0;
            };
            RowsView = cv;

            UpdateMetrics();
            RebuildMigrationTargets();
            DeleteCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var r in Rows.Where(r => r.IsDeletable))
                r.IsSelected = true;
        }

        [RelayCommand]
        private void ClearSelection()
        {
            foreach (var r in Rows)
                r.IsSelected = false;
        }

        [RelayCommand]
        private void Reset()
        {
            foreach (var r in Rows)
                r.IsSelected = false;
            MigrationTarget      = null;
            HardDeleteUnmigratable = false;
            ConfirmEachDeletion  = true;
            FilterText           = string.Empty;
            Log.Clear();
        }

        [RelayCommand(CanExecute = nameof(CanDelete))]
        private void Delete()
        {
            var selected = Rows.Where(r => r.IsSelected && r.IsDeletable).ToList();
            if (!selected.Any()) return;

            // Pre-flight validation
            // Closed worksets may hold elements that are not counted, so they need a target too.
            bool anyHaveElements = selected.Any(r => r.ElementCount > 0 || !r.IsOpen);
            if (anyHaveElements && MigrationTarget == null)
            {
                MessageBox.Show(
                    "One or more selected worksets have elements or are closed.\nPlease choose a migration target workset.",
                    "Delete Workset",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Count un-migratable: warn if any exist and hard-delete is off.
            // (We approximate at the view model level; exact counts come from the service.)
            if (!HardDeleteUnmigratable)
            {
                // Quick advisory check — annotation-heavy projects may have some.
                // The service will skip them and log warnings.
            }

            // Confirmation summary
            string summary = BuildConfirmationSummary(selected);
            var confirm = MessageBox.Show(
                summary,
                "Confirm Deletion",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            IsRunning = true;
            DeleteCommand.NotifyCanExecuteChanged();

            var targetId  = MigrationTarget?.WorksetId;
            bool hardDel  = HardDeleteUnmigratable;
            bool perStep  = ConfirmEachDeletion;

            _handler.Queue(app =>
            {
                var doc = app.ActiveUIDocument.Document;
                var svc = new DeleteWorksetService(AddLog);

                using var tg = new TransactionGroup(doc, "Delete Workset(s)");
                try
                {
                    // Checkout talks to central and is not allowed inside a transaction.
                    var owned = svc.CheckoutWorksets(doc, selected);
                    var toDelete = selected.Where(r => owned.Contains(r.WorksetId.IntegerValue)).ToList();

                    tg.Start();
                    Func<WorksetRow, bool> confirm2 = perStep
                        ? row =>
                        {
                            bool proceed = false;
                            _dispatcher.Invoke(() =>
                            {
                                var r = MessageBox.Show(
                                    $"Delete workset '{row.Name}' ({row.ElementCount} elements)?",
                                    "Confirm",
                                    MessageBoxButton.YesNo,
                                    MessageBoxImage.Question);
                                proceed = r == MessageBoxResult.Yes;
                            });
                            return proceed;
                        }
                        : (Func<WorksetRow, bool>)null;

                    var result = svc.DeleteWorksets(doc, toDelete, targetId, hardDel, confirm2);
                    tg.Assimilate();

                    AddLog(new LogEntry(LogLevel.Success,
                        $"— {result}"));

                    _dispatcher.Invoke(() =>
                    {
                        Refresh();
                        IsRunning = false;
                        DeleteCommand.NotifyCanExecuteChanged();
                    });
                }
                catch (OperationCanceledException ex)
                {
                    if (tg.HasStarted()) tg.RollBack();
                    AddLog(new LogEntry(LogLevel.Warning, ex.Message));
                    _dispatcher.Invoke(() =>
                    {
                        Refresh();
                        IsRunning = false;
                        DeleteCommand.NotifyCanExecuteChanged();
                    });
                }
                catch (Exception ex)
                {
                    if (tg.HasStarted()) tg.RollBack();
                    AddLog(new LogEntry(LogLevel.Error, $"Run aborted — all changes rolled back: {ex.Message}"));
                    _dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(
                            $"The operation failed and all changes were rolled back.\n\n{ex.Message}",
                            "Delete Workset — Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        Refresh();
                        IsRunning = false;
                        DeleteCommand.NotifyCanExecuteChanged();
                    });
                }
            });

            _externalEvent.Raise();
        }

        private bool CanDelete()
        {
            if (IsRunning) return false;
            return Rows.Any(r => r.IsSelected && r.IsDeletable);
        }

        [RelayCommand]
        private void CopyAllLog()
        {
            if (!Log.Any()) return;
            var sb = new StringBuilder();
            foreach (var e in Log) sb.AppendLine(e.ToString());
            try { Clipboard.SetText(sb.ToString()); }
            catch { /* clipboard busy */ }
        }

        [RelayCommand]
        private void CopySelectedLog() => CopyAllLog(); // selection handled in view

        [RelayCommand]
        private void ToggleLog() => IsLogExpanded = !IsLogExpanded;

        // ── Helpers ───────────────────────────────────────────────────────────

        private void OnRowPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WorksetRow.IsSelected))
            {
                RebuildMigrationTargets();
                DeleteCommand.NotifyCanExecuteChanged();
            }
        }

        private void UpdateMetrics()
        {
            TotalCount       = Rows.Count;
            DeletableCount   = Rows.Count(r => r.IsDeletable);
            NonDeletableCount = Rows.Count(r => !r.IsDeletable);
        }

        private void RebuildMigrationTargets()
        {
            var selectedIds = Rows
                .Where(r => r.IsSelected && r.IsDeletable)
                .Select(r => r.WorksetId.IntegerValue)
                .ToHashSet();

            // Any user workset not being deleted can receive the elements.
            MigrationTargets.Clear();
            foreach (var r in Rows.Where(r => !selectedIds.Contains(r.WorksetId.IntegerValue)))
                MigrationTargets.Add(r);

            // If current target was removed, clear it.
            if (MigrationTarget != null && !MigrationTargets.Contains(MigrationTarget))
                MigrationTarget = null;
        }

        private void AddLog(LogEntry entry)
            => _dispatcher.Invoke(
                () => Log.Insert(0, entry),
                DispatcherPriority.Background);

        private string BuildConfirmationSummary(System.Collections.Generic.List<WorksetRow> selected)
        {
            var sb = new StringBuilder();
            sb.AppendLine("The following worksets will be permanently deleted:\n");
            foreach (var r in selected)
                sb.AppendLine($"  • {r.Name}  ({r.ElementCount} elements)");

            if (MigrationTarget != null)
                sb.AppendLine($"\nMigratable elements → '{MigrationTarget.Name}'");

            if (HardDeleteUnmigratable)
                sb.AppendLine("Un-migratable elements will be hard-deleted.");
            else
                sb.AppendLine("Un-migratable elements will be skipped (left in place).");

            sb.AppendLine("\nThis cannot be undone. Continue?");
            return sb.ToString();
        }
    }
}
