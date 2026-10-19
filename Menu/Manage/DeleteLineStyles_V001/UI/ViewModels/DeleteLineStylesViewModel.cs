using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.DeleteLineStyles.V001.Core.Models;
using Revit26_Plugin.DeleteLineStyles.V001.Core.Services;
using Revit26_Plugin.Shared.Models;
using Revit26_Plugin.Shared.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

namespace Revit26_Plugin.DeleteLineStyles.V001.UI.ViewModels
{
    /// <summary>
    /// View model of the Delete Line Styles window: lists custom line styles, lets the user pick
    /// "unused only" (default) or "all custom", and deletes the ticked ones through an external event.
    /// </summary>
    public partial class DeleteLineStylesViewModel : ObservableObject
    {
        private readonly Document _doc;
        private readonly DeleteLineStylesHandler _handler = new();
        private readonly ExternalEvent _externalEvent;
        private readonly Dispatcher _dispatcher;

        /// <summary>Custom line styles.</summary>
        public ObservableCollection<LineStyleRow> Rows { get; } = new();

        /// <summary>Styles lines can be moved to (all line styles not ticked for deletion).</summary>
        public ObservableCollection<ReplacementStyle> Replacements { get; } = new();

        /// <summary>Run log, newest first.</summary>
        public ObservableCollection<LogEntry> Log { get; } = new();

        private List<ReplacementStyle> _allStyles = new();

        private ICollectionView _rowsView;

        /// <summary>Filtered view of <see cref="Rows"/>.</summary>
        public ICollectionView RowsView
        {
            get => _rowsView;
            private set => SetProperty(ref _rowsView, value);
        }

        /// <summary>Name filter.</summary>
        [ObservableProperty] private string filterText = string.Empty;

        /// <summary>Number of custom line styles.</summary>
        [ObservableProperty] private int totalCount;

        /// <summary>Number of custom line styles no line uses.</summary>
        [ObservableProperty] private int unusedCount;

        /// <summary>Number of custom line styles in use.</summary>
        [ObservableProperty] private int usedCount;

        /// <summary>Number of rows ticked.</summary>
        [ObservableProperty] private int selectedCount;

        /// <summary>
        /// False (default): only unused styles can be deleted. True: used styles can be deleted
        /// too; their lines move to <see cref="Replacement"/> first.
        /// </summary>
        [ObservableProperty] private bool includeUsed;

        /// <summary>Style that lines of a deleted, used style move to.</summary>
        [ObservableProperty] private ReplacementStyle replacement;

        /// <summary>True while a deletion is running.</summary>
        [ObservableProperty] private bool isRunning;

        /// <summary>Log panel expanded.</summary>
        [ObservableProperty] private bool isLogExpanded = true;

        /// <summary>Creates the view model and loads the active document's line styles.</summary>
        public DeleteLineStylesViewModel(ExternalCommandData commandData)
        {
            _doc = commandData.Application.ActiveUIDocument.Document;
            _dispatcher = Dispatcher.CurrentDispatcher;
            _externalEvent = ExternalEvent.Create(_handler);

            Refresh();
        }

        /// <summary>True when "unused only" is chosen; bound to the first radio button.</summary>
        public bool UnusedOnly
        {
            get => !IncludeUsed;
            set { if (value) IncludeUsed = false; }
        }

        partial void OnIncludeUsedChanged(bool value)
        {
            OnPropertyChanged(nameof(UnusedOnly));
            foreach (var r in Rows) r.ApplyMode(value);
            UpdateCounts();
            DeleteCommand.NotifyCanExecuteChanged();
        }

        partial void OnFilterTextChanged(string value) => RowsView?.Refresh();

        partial void OnReplacementChanged(ReplacementStyle value) => DeleteCommand.NotifyCanExecuteChanged();

        partial void OnIsRunningChanged(bool value) => DeleteCommand.NotifyCanExecuteChanged();

        /// <summary>Reloads line styles and usage from the document.</summary>
        [RelayCommand]
        private void Refresh()
        {
            if (!_doc.IsValidObject)
            {
                AddLog(new LogEntry(LogLevel.Error, "The document was closed. Close this window."));
                return;
            }

            var svc = new LineStyleService(AddLog);
            var rows = svc.LoadLineStyles(_doc);
            _allStyles = svc.LoadReplacementStyles(_doc);

            foreach (var r in Rows) r.PropertyChanged -= OnRowPropertyChanged;
            Rows.Clear();
            foreach (var r in rows)
            {
                r.ApplyMode(IncludeUsed);
                r.IsSelected = r.IsDeletable && !r.IsUsed;
                r.PropertyChanged += OnRowPropertyChanged;
                Rows.Add(r);
            }

            var cv = CollectionViewSource.GetDefaultView(Rows);
            cv.Filter = obj => obj is LineStyleRow row
                && (string.IsNullOrWhiteSpace(FilterText)
                    || row.Name.IndexOf(FilterText, StringComparison.OrdinalIgnoreCase) >= 0);
            RowsView = cv;

            RebuildReplacements();
            UpdateCounts();
            DeleteCommand.NotifyCanExecuteChanged();
        }

        /// <summary>Ticks every row that can be deleted in the current mode.</summary>
        [RelayCommand]
        private void SelectAll()
        {
            foreach (var r in Rows.Where(r => r.IsDeletable)) r.IsSelected = true;
        }

        /// <summary>Unticks every row.</summary>
        [RelayCommand]
        private void ClearSelection()
        {
            foreach (var r in Rows) r.IsSelected = false;
        }

        /// <summary>Confirms and deletes the ticked line styles.</summary>
        [RelayCommand(CanExecute = nameof(CanDelete))]
        private void Delete()
        {
            var selected = Rows.Where(r => r.IsSelected && r.IsDeletable).ToList();
            if (selected.Count == 0) return;

            bool includeUsed = IncludeUsed;
            var replacement = Replacement;
            int usedLines = selected.Sum(r => r.LineCount);

            if (includeUsed && usedLines > 0 && replacement == null)
            {
                MessageBox.Show("Choose a replacement style for the lines that use the selected styles.",
                    ToolCatalog.DeleteLineStyles.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Delete {selected.Count} line style(s)?\n");
            foreach (var r in selected.Take(20))
                sb.AppendLine(r.IsUsed ? $"  • {r.Name}  ({r.LineCount} line(s))" : $"  • {r.Name}");
            if (selected.Count > 20) sb.AppendLine($"  … and {selected.Count - 20} more");
            if (usedLines > 0)
                sb.AppendLine($"\n{usedLines} line(s) will be changed to '{replacement.Name}'.");
            sb.AppendLine("\nYou can undo this with Ctrl+Z in Revit.");

            if (MessageBox.Show(sb.ToString(), ToolCatalog.DeleteLineStyles.Title,
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            IsRunning = true;
            _handler.Queue(app => RunDelete(app, selected, includeUsed, replacement));
            _externalEvent.Raise();
        }

        private bool CanDelete() => !IsRunning && Rows.Any(r => r.IsSelected && r.IsDeletable);

        // Runs in the Revit API context.
        private void RunDelete(UIApplication app, List<LineStyleRow> selected, bool includeUsed, ReplacementStyle replacement)
        {
            try
            {
                var doc = app.ActiveUIDocument?.Document;
                if (doc == null || !doc.Equals(_doc))
                {
                    AddLog(new LogEntry(LogLevel.Error,
                        "The active document changed. Switch back to the document this window was opened for."));
                    return;
                }

                AddLog(new LogEntry(LogLevel.Info, $"Deleting {selected.Count} line style(s)…"));
                var result = new LineStyleService(AddLog).DeleteLineStyles(doc, selected, includeUsed, replacement);
                AddLog(new LogEntry(result.Deleted > 0 ? LogLevel.Success : LogLevel.Warning, "— " + result));
            }
            catch (Exception ex)
            {
                AddLog(new LogEntry(LogLevel.Error, $"Run aborted, nothing was changed: {ex.Message}"));
                ToolGuard.Report(GetType(), ex);
            }
            finally
            {
                _dispatcher.Invoke(() =>
                {
                    IsRunning = false;
                    Refresh();
                });
            }
        }

        /// <summary>Copies the log to the clipboard.</summary>
        [RelayCommand]
        private void CopyLog()
        {
            if (Log.Count == 0) return;
            var sb = new StringBuilder();
            foreach (var e in Log.Reverse()) sb.AppendLine(e.ToString());
            try { Clipboard.SetText(sb.ToString()); }
            catch (System.Runtime.InteropServices.ExternalException) { /* clipboard busy */ }
        }

        private void OnRowPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(LineStyleRow.IsSelected)) return;
            RebuildReplacements();
            UpdateCounts();
            DeleteCommand.NotifyCanExecuteChanged();
        }

        private void UpdateCounts()
        {
            TotalCount = Rows.Count;
            UnusedCount = Rows.Count(r => !r.IsUsed);
            UsedCount = Rows.Count(r => r.IsUsed);
            SelectedCount = Rows.Count(r => r.IsSelected);
        }

        // Replacement candidates are every line style not ticked for deletion. Keeps the current
        // choice if still valid, otherwise picks <Thin Lines> or the first built-in style.
        private void RebuildReplacements()
        {
            var ticked = Rows.Where(r => r.IsSelected).Select(r => r.CategoryId).ToHashSet();
            var keep = Replacement?.CategoryId;

            Replacements.Clear();
            foreach (var s in _allStyles.Where(s => !ticked.Contains(s.CategoryId)))
                Replacements.Add(s);

            Replacement = Replacements.FirstOrDefault(s => s.CategoryId == keep)
                ?? Replacements.FirstOrDefault(s => s.IsBuiltIn && s.Name == "<Thin Lines>")
                ?? Replacements.FirstOrDefault(s => s.IsBuiltIn)
                ?? Replacements.FirstOrDefault();
        }

        private void AddLog(LogEntry entry)
            => _dispatcher.Invoke(() => Log.Insert(0, entry), DispatcherPriority.Background);
    }
}
