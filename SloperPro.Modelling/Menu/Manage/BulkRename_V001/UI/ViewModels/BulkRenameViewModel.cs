using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.BulkRename.V001.Core.Models;
using Revit26_Plugin.BulkRename.V001.Core.Services;
using Revit26_Plugin.BulkRename.V001.Core.Sources;
using Revit26_Plugin.Shared.Models;
using Revit26_Plugin.Shared.Services;
using Revit26_Plugin.Shared.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

namespace Revit26_Plugin.BulkRename.V001.UI.ViewModels
{
    /// <summary>
    /// View model of the Bulk Rename window. The user picks what to rename (line styles, line patterns,
    /// arrowheads or fill patterns), builds new names with the rules or by typing, and applies them.
    /// All reads and writes of the model go through an external event, so they run in a Revit API context.
    /// </summary>
    public partial class BulkRenameViewModel : ToolViewModelBase
    {
        private readonly Document _doc;
        private readonly BulkRenameHandler _handler = new();
        private readonly ExternalEvent _externalEvent;
        private readonly Dispatcher _dispatcher;
        private readonly Dictionary<string, List<RenameItem>> _cache = new();

        // True while this view model changes rows itself, so one rule over 500 rows revalidates once.
        private bool _suspend;

        // Queued actions that have not finished; the window shows "running" while above zero.
        private int _busy;

        /// <summary>The kinds of item that can be renamed.</summary>
        public IReadOnlyList<RenameSource> Sources { get; }

        /// <summary>Rows of the selected kind of item.</summary>
        public ObservableCollection<RenameItem> Rows { get; } = new();

        /// <summary>Rows that match the filter, in grid order.</summary>
        public ICollectionView RowsView { get; }

        /// <summary>What is being renamed.</summary>
        [ObservableProperty] private RenameSource selectedSource;

        /// <summary>Shows only rows whose current name contains this text.</summary>
        [ObservableProperty] private string filterText = string.Empty;

        /// <summary>Find text of the find/replace rule.</summary>
        [ObservableProperty] private string findText = string.Empty;

        /// <summary>Replacement text of the find/replace rule.</summary>
        [ObservableProperty] private string replaceText = string.Empty;

        /// <summary>Find/replace is case sensitive.</summary>
        [ObservableProperty] private bool matchCase;

        /// <summary>The find text is a regular expression.</summary>
        [ObservableProperty] private bool useRegex;

        /// <summary>Text added to or removed from the start of names.</summary>
        [ObservableProperty] private string prefix = string.Empty;

        /// <summary>Text added to or removed from the end of names.</summary>
        [ObservableProperty] private string suffix = string.Empty;

        /// <summary>Numbering pattern; {n} is the number, {name} the row's current new name.</summary>
        [ObservableProperty] private string numberPattern = "{name}_{n}";

        /// <summary>First number.</summary>
        [ObservableProperty] private int numberStart = 1;

        /// <summary>Difference between one number and the next.</summary>
        [ObservableProperty] private int numberStep = 1;

        /// <summary>Digits the number is padded to with zeros.</summary>
        [ObservableProperty] private int numberDigits = 2;

        /// <summary>Items listed.</summary>
        [ObservableProperty] private int totalCount;

        /// <summary>Rows that are ticked and can be renamed.</summary>
        [ObservableProperty] private int tickedCount;

        /// <summary>Rows that will be renamed on Apply.</summary>
        [ObservableProperty] private int changeCount;

        /// <summary>Ticked rows with an invalid or duplicate new name; Apply waits until there are none.</summary>
        [ObservableProperty] private int problemCount;

        /// <summary>One-line explanation of the selected kind of item.</summary>
        public string SourceDescription => SelectedSource?.Description ?? string.Empty;

        /// <summary>Reminder of the numbering tokens.</summary>
        public string NumberingHint => "{n} = number, {name} = the row's current new name";

        /// <summary>Creates the view model and starts listing the first kind of item.</summary>
        public BulkRenameViewModel(ExternalCommandData commandData)
        {
            _doc = commandData.Application.ActiveUIDocument.Document;
            _dispatcher = Dispatcher.CurrentDispatcher;
            _externalEvent = ExternalEvent.Create(_handler);

            Sources = new RenameSource[]
            {
                new LineStylesSource(),
                new LinePatternsSource(),
                new ArrowheadsSource(),
                new FillPatternsSource()
            };

            // Everything the callbacks below touch is ready before the first source is selected.
            var view = CollectionViewSource.GetDefaultView(Rows);
            view.Filter = obj => obj is RenameItem row && PassesFilter(row);
            RowsView = view;

            SelectedSource = Sources[0];
        }

        partial void OnSelectedSourceChanged(RenameSource value)
        {
            OnPropertyChanged(nameof(SourceDescription));
            LoadSelected(force: false);
        }

        partial void OnFilterTextChanged(string value) => RowsView?.Refresh();

        /// <summary>Releases the external event; called when the window closes.</summary>
        public void Shutdown()
        {
            try { _externalEvent.Dispose(); }
            catch (Autodesk.Revit.Exceptions.ApplicationException) { /* Revit already released it */ }
        }

        // ── Loading ───────────────────────────────────────────────────────────

        private bool PassesFilter(RenameItem row)
            => string.IsNullOrWhiteSpace(FilterText)
               || row.CurrentName.IndexOf(FilterText, StringComparison.OrdinalIgnoreCase) >= 0;

        private void LoadSelected(bool force)
        {
            var source = SelectedSource;
            if (source == null) return;

            if (!force && _cache.TryGetValue(source.Key, out var cached))
            {
                ShowRows(cached);
                return;
            }

            ShowRows(new List<RenameItem>());
            RunInRevit($"Loading {source.DisplayName.ToLowerInvariant()}…", app =>
            {
                if (!TryGetDocument(app, out var doc)) return;
                LoadInto(doc, source);
            });
        }

        // Runs in the Revit API context: lists the items, then shows them if the user is still on this kind.
        private void LoadInto(Document doc, RenameSource source)
        {
            var items = source.Load(doc, LogFromRevit);
            foreach (var item in items) item.IsSelected = !item.IsLocked;

            OnUi(() =>
            {
                _cache[source.Key] = items;
                if (ReferenceEquals(SelectedSource, source)) ShowRows(items);
            });
        }

        private void ShowRows(List<RenameItem> items)
        {
            _suspend = true;
            try
            {
                foreach (var row in Rows) row.PropertyChanged -= OnRowPropertyChanged;
                Rows.Clear();
                foreach (var item in items)
                {
                    item.PropertyChanged -= OnRowPropertyChanged;
                    item.PropertyChanged += OnRowPropertyChanged;
                    Rows.Add(item);
                }
            }
            finally
            {
                _suspend = false;
            }

            Revalidate();
            SummaryText = items.Count == 0 ? string.Empty : $"{items.Count} item(s) listed.";
        }

        /// <summary>Lists the selected kind of item again, discarding names that were not applied.</summary>
        [RelayCommand]
        private void Reload()
        {
            if (SelectedSource == null) return;

            bool hasEdits = Rows.Any(r => !string.Equals(r.NewName, r.CurrentName, StringComparison.Ordinal));
            if (hasEdits && MessageBox.Show("Reloading discards the new names you have not applied. Continue?",
                    ToolCatalog.BulkRename.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            _cache.Remove(SelectedSource.Key);
            LoadSelected(force: true);
        }

        // ── Rules ─────────────────────────────────────────────────────────────

        /// <summary>Runs the find/replace rule on the ticked, visible rows.</summary>
        [RelayCommand]
        private void ApplyFindReplace()
        {
            if (string.IsNullOrEmpty(FindText))
            {
                SummaryText = "Type the text to find first.";
                return;
            }

            if (UseRegex && !NameRules.IsValidRegex(FindText, out string error))
            {
                SummaryText = "Not a valid regular expression: " + error;
                AddLog(LogLevel.Warning, SummaryText);
                return;
            }

            string find = FindText, replace = ReplaceText ?? string.Empty;
            bool matchCase = MatchCase, regex = UseRegex;
            ApplyToTargets("Find & replace", (name, _) => NameRules.Replace(name, find, replace, matchCase, regex));
        }

        /// <summary>Adds the prefix to the ticked, visible rows.</summary>
        [RelayCommand]
        private void AddPrefix()
        {
            string text = Prefix;
            if (RequireText(text, "prefix")) ApplyToTargets("Add prefix", (name, _) => NameRules.AddPrefix(name, text));
        }

        /// <summary>Removes the prefix from the ticked, visible rows that start with it.</summary>
        [RelayCommand]
        private void RemovePrefix()
        {
            string text = Prefix;
            if (RequireText(text, "prefix")) ApplyToTargets("Remove prefix", (name, _) => NameRules.RemovePrefix(name, text));
        }

        /// <summary>Adds the suffix to the ticked, visible rows.</summary>
        [RelayCommand]
        private void AddSuffix()
        {
            string text = Suffix;
            if (RequireText(text, "suffix")) ApplyToTargets("Add suffix", (name, _) => NameRules.AddSuffix(name, text));
        }

        /// <summary>Removes the suffix from the ticked, visible rows that end with it.</summary>
        [RelayCommand]
        private void RemoveSuffix()
        {
            string text = Suffix;
            if (RequireText(text, "suffix")) ApplyToTargets("Remove suffix", (name, _) => NameRules.RemoveSuffix(name, text));
        }

        /// <summary>Trims names and collapses runs of spaces.</summary>
        [RelayCommand]
        private void CleanSpaces() => ApplyToTargets("Clean spaces", (name, _) => NameRules.CleanSpaces(name));

        /// <summary>Turns spaces into underscores.</summary>
        [RelayCommand]
        private void SpacesToUnderscores() => ApplyToTargets("Spaces to underscores", (name, _) => NameRules.SpacesToUnderscores(name));

        /// <summary>Upper-cases names.</summary>
        [RelayCommand]
        private void UpperCase() => ApplyToTargets("UPPER CASE", (name, _) => NameRules.ToUpper(name));

        /// <summary>Lower-cases names.</summary>
        [RelayCommand]
        private void LowerCase() => ApplyToTargets("lower case", (name, _) => NameRules.ToLower(name));

        /// <summary>Title-cases names.</summary>
        [RelayCommand]
        private void TitleCase() => ApplyToTargets("Title Case", (name, _) => NameRules.ToTitle(name));

        /// <summary>Numbers the ticked, visible rows in grid order using the numbering pattern.</summary>
        [RelayCommand]
        private void ApplyNumbering()
        {
            if (!NameRules.PatternHasNumber(NumberPattern))
            {
                SummaryText = "The numbering pattern needs {n}, for example {name}_{n}.";
                return;
            }

            string pattern = NumberPattern;
            int start = NumberStart, step = NumberStep, digits = NumberDigits;
            ApplyToTargets("Numbering", (name, index) => NameRules.Number(pattern, name, start + index * step, digits));
        }

        private bool RequireText(string text, string what)
        {
            if (!string.IsNullOrEmpty(text)) return true;
            SummaryText = $"Type the {what} first.";
            return false;
        }

        // Computes every new name first and only then assigns them, so a rule that fails
        // (for example a regex that times out) changes nothing.
        private void ApplyToTargets(string label, Func<string, int, string> transform)
        {
            var targets = RowsView.Cast<RenameItem>().Where(r => r.IsSelected && !r.IsLocked).ToList();
            if (targets.Count == 0)
            {
                SummaryText = "Tick at least one visible row first.";
                return;
            }

            List<string> updated;
            try
            {
                updated = targets.Select((row, index) => transform(row.NewName, index)).ToList();
            }
            catch (Exception ex) when (ex is ArgumentException || ex is RegexMatchTimeoutException)
            {
                SummaryText = $"{label} failed: {ex.Message}";
                AddLog(LogLevel.Warning, SummaryText);
                return;
            }

            int changed = 0;
            _suspend = true;
            try
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    if (string.Equals(targets[i].NewName, updated[i], StringComparison.Ordinal)) continue;
                    targets[i].NewName = updated[i];
                    changed++;
                }
            }
            finally
            {
                _suspend = false;
            }

            Revalidate();
            SummaryText = $"{label}: {changed} of {targets.Count} name(s) changed.";
        }

        // ── Selection ─────────────────────────────────────────────────────────

        /// <summary>Ticks every visible row that can be renamed.</summary>
        [RelayCommand]
        private void SelectAll() => SetTicks(true);

        /// <summary>Unticks every visible row.</summary>
        [RelayCommand]
        private void SelectNone() => SetTicks(false);

        private void SetTicks(bool on)
        {
            _suspend = true;
            try
            {
                foreach (var row in RowsView.Cast<RenameItem>().Where(r => !r.IsLocked)) row.IsSelected = on;
            }
            finally
            {
                _suspend = false;
            }
            Revalidate();
        }

        /// <summary>Puts every visible row's new name back to its current name.</summary>
        [RelayCommand]
        private void RevertNames()
        {
            int reverted = 0;
            _suspend = true;
            try
            {
                foreach (var row in RowsView.Cast<RenameItem>().Where(r => !r.IsLocked))
                {
                    if (string.Equals(row.NewName, row.CurrentName, StringComparison.Ordinal)) continue;
                    row.NewName = row.CurrentName;
                    reverted++;
                }
            }
            finally
            {
                _suspend = false;
            }
            Revalidate();
            SummaryText = $"Reverted {reverted} name(s).";
        }

        // ── Validation ────────────────────────────────────────────────────────

        private void OnRowPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_suspend || sender is not RenameItem row) return;

            if (e.PropertyName == nameof(RenameItem.NewName))
            {
                // Typing a new name into a row is a request to rename it, so tick it.
                if (!row.IsSelected && !row.IsLocked
                    && !string.Equals(row.NewName, row.CurrentName, StringComparison.Ordinal))
                {
                    _suspend = true;
                    try { row.IsSelected = true; }
                    finally { _suspend = false; }
                }
                Revalidate();
            }
            else if (e.PropertyName == nameof(RenameItem.IsSelected))
            {
                Revalidate();
            }
        }

        // Checks all rows together (duplicates depend on the whole list) and refreshes the counters.
        private void Revalidate()
        {
            var entries = Rows.Select(r => new NameEntry(r.CurrentName, r.NewName, r.IsSelected, r.IsLocked)).ToList();
            var checks = NameValidator.Check(entries);

            _suspend = true;
            try
            {
                for (int i = 0; i < Rows.Count; i++)
                {
                    Rows[i].Status = checks[i].Status;
                    Rows[i].StatusMessage = Rows[i].IsLocked ? Rows[i].LockReason : checks[i].Message ?? string.Empty;
                }
            }
            finally
            {
                _suspend = false;
            }

            TotalCount = Rows.Count;
            TickedCount = Rows.Count(r => r.IsSelected && !r.IsLocked);
            ChangeCount = Rows.Count(r => r.Status == RenameStatus.Ready);
            ProblemCount = Rows.Count(r => r.Status == RenameStatus.Invalid || r.Status == RenameStatus.Duplicate);
            ApplyCommand.NotifyCanExecuteChanged();
        }

        // ── Apply ─────────────────────────────────────────────────────────────

        /// <summary>Confirms and renames the ready rows in one transaction.</summary>
        [RelayCommand(CanExecute = nameof(CanApply))]
        private void Apply()
        {
            var source = SelectedSource;
            var targets = Rows.Where(r => r.Status == RenameStatus.Ready).ToList();
            if (source == null || targets.Count == 0) return;

            string kind = source.DisplayName.ToLowerInvariant();
            var sb = new StringBuilder();
            sb.AppendLine($"Rename {targets.Count} {kind}?\n");
            foreach (var row in targets.Take(15))
                sb.AppendLine($"  • {row.CurrentName}  →  {row.NewName.Trim()}");
            if (targets.Count > 15) sb.AppendLine($"  … and {targets.Count - 15} more");
            sb.AppendLine("\nYou can undo this with Ctrl+Z in Revit.");

            if (MessageBox.Show(sb.ToString(), ToolCatalog.BulkRename.Title,
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            RunInRevit($"Renaming {targets.Count} {kind}…", app =>
            {
                if (!TryGetDocument(app, out var doc)) return;

                AddLog(LogLevel.Info, $"Renaming {targets.Count} {kind}…");
                var result = new RenameService(LogFromRevit).Apply(doc, targets, $"Bulk Rename: {source.DisplayName}");
                AddLog(result.Renamed > 0 ? LogLevel.Success : LogLevel.Warning, "— " + result);

                LoadInto(doc, source);
                SummaryText = result.ToString();
            });
        }

        private bool CanApply() => !IsRunning && ChangeCount > 0 && ProblemCount == 0;

        // ── Revit plumbing ────────────────────────────────────────────────────

        private sealed class Job
        {
            public bool Cancelled;
        }

        // Queues work for the Revit API context. A failure is logged and reported; the run counter always
        // goes back down so the window never stays stuck on "running".
        private void RunInRevit(string summary, Action<UIApplication> body)
        {
            var job = new Job();
            SetBusy(+1, summary);

            _handler.Queue(app =>
            {
                if (job.Cancelled) return;

                try
                {
                    body(app);
                }
                catch (Exception ex)
                {
                    AddLog(LogLevel.Error, $"Stopped, nothing was changed: {ex.Message}");
                    ToolGuard.Report(GetType(), ex);
                }
                finally
                {
                    SetBusy(-1, null);
                }
            });

            if (_externalEvent.Raise() == ExternalEventRequest.Denied)
            {
                job.Cancelled = true;
                SetBusy(-1, null);
                SummaryText = "Revit is busy. Close any open dialog and try again.";
                AddLog(LogLevel.Warning, SummaryText);
            }
        }

        private void SetBusy(int delta, string summary)
        {
            _busy = Math.Max(0, _busy + delta);
            IsRunning = _busy > 0;
            if (summary != null) SummaryText = summary;
            ApplyCommand.NotifyCanExecuteChanged();
        }

        private bool TryGetDocument(UIApplication app, out Document doc)
        {
            doc = app.ActiveUIDocument?.Document;
            if (doc != null && _doc.IsValidObject && doc.Equals(_doc)) return true;

            AddLog(LogLevel.Error, "The active document changed. Switch back to the document this window was opened for.");
            doc = null;
            return false;
        }

        private void LogFromRevit(LogEntry entry) => OnUi(() => AddLog(entry.Level, entry.Message));

        private void OnUi(Action action)
        {
            if (_dispatcher.CheckAccess()) action();
            else _dispatcher.Invoke(action);
        }
    }
}
