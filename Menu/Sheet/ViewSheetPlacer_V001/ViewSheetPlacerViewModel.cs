using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.Tools.ViewSheetPlacer
{
    public partial class ViewSheetPlacerViewModel : ObservableObject
    {
        private const string ToolName = "ViewSheetPlacer";

        private readonly Document _doc;
        private readonly ExternalEvent _event;
        private readonly ViewSheetPlacerHandler _handler;
        private readonly Dispatcher _dispatcher;
        private readonly ViewSheetPlacerSettings _settings;

        public ObservableCollection<ViewInfo> Views { get; } = new();
        public ICollectionView ViewsView { get; }
        public ObservableCollection<LogEntry> Logs { get; } = new();
        public ObservableCollection<TitleblockOption> Titleblocks { get; } = new();
        public ObservableCollection<string> ParameterNames { get; } = new();

        public string[] PlacedFilterOptions { get; } = { "All", "Placed", "Unplaced" };

        [ObservableProperty] private string _textFilter = string.Empty;
        [ObservableProperty] private string _placedFilter = "All";
        [ObservableProperty] private string? _selectedParameter;
        [ObservableProperty] private string _parameterValueFilter = string.Empty;
        [ObservableProperty] private bool _parameterFilterActive;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RunCommand))]
        [NotifyCanExecuteChangedFor(nameof(DryRunCommand))]
        private TitleblockOption? _selectedTitleblock;
        [ObservableProperty] private string _sheetNamePrefix = string.Empty;
        [ObservableProperty] private string _grouping = "Discipline";
        [ObservableProperty] private bool _skipAlreadyPlaced = true;
        [ObservableProperty] private bool _showViewportTitles = true;

        [ObservableProperty] private int _placedCount;
        [ObservableProperty] private int _skippedCount;
        [ObservableProperty] private int _failedCount;
        [ObservableProperty] private int _selectedCount;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RunCommand))]
        [NotifyCanExecuteChangedFor(nameof(DryRunCommand))]
        private bool _isRunning;

        [ObservableProperty] private bool _noTitleblocks;

        public IList<LogEntry> SelectedLogs { get; } = new List<LogEntry>();

        public ViewSheetPlacerViewModel(
            Document doc, ViewScan scan, ExternalEvent evt, ViewSheetPlacerHandler handler)
        {
            _doc = doc;
            _event = evt;
            _handler = handler;
            _dispatcher = Dispatcher.CurrentDispatcher;
            _settings = ViewSheetPlacerSettings.Load();

            LoadScan(scan);
            ApplySettings();

            ViewsView = CollectionViewSource.GetDefaultView(Views);
            ViewsView.Filter = FilterRow;
        }

        private void LoadScan(ViewScan scan)
        {
            // Detach old row handlers before replacing the collection.
            foreach (var old in Views) old.PropertyChanged -= ViewRow_PropertyChanged;

            Views.Clear();
            foreach (var v in scan.Views)
            {
                v.PropertyChanged += ViewRow_PropertyChanged;
                Views.Add(v);
            }
            RecomputeSelectedCount();

            Titleblocks.Clear();
            foreach (var t in scan.Titleblocks) Titleblocks.Add(t);
            NoTitleblocks = Titleblocks.Count == 0;

            ParameterNames.Clear();
            foreach (var n in scan.ParameterNames) ParameterNames.Add(n);
        }

        private bool _suppressCountRecompute;

        private void ViewRow_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!_suppressCountRecompute && e.PropertyName == nameof(ViewInfo.IsSelected))
                RecomputeSelectedCount();
        }

        private void RecomputeSelectedCount() =>
            SelectedCount = Views.Count(v => v.IsSelected);

        private void ApplySettings()
        {
            SheetNamePrefix = _settings.SheetNamePrefix;
            Grouping = _settings.Grouping;
            SkipAlreadyPlaced = _settings.SkipAlreadyPlaced;
            ShowViewportTitles = _settings.ShowViewportTitles;

            SelectedTitleblock =
                Titleblocks.FirstOrDefault(t => t.UniqueId == _settings.TitleblockUniqueId)
                ?? Titleblocks.FirstOrDefault();
        }

        // ---- filtering --------------------------------------------------------

        partial void OnTextFilterChanged(string value) => ViewsView?.Refresh();
        partial void OnPlacedFilterChanged(string value) => ViewsView?.Refresh();

        private bool FilterRow(object obj)
        {
            if (obj is not ViewInfo v) return false;

            if (!string.IsNullOrWhiteSpace(TextFilter) &&
                v.ViewName.IndexOf(TextFilter, StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            if (PlacedFilter == "Placed" && !v.IsPlaced) return false;
            if (PlacedFilter == "Unplaced" && v.IsPlaced) return false;

            if (ParameterFilterActive &&
                !string.IsNullOrEmpty(SelectedParameter))
            {
                v.ParamValues.TryGetValue(SelectedParameter, out var pv);
                pv ??= string.Empty;
                if (pv.IndexOf(ParameterValueFilter ?? string.Empty,
                        StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }

            return true;
        }

        [RelayCommand]
        private void ApplyParameterFilter()
        {
            ParameterFilterActive = !string.IsNullOrEmpty(SelectedParameter);
            ViewsView?.Refresh();
        }

        [RelayCommand]
        private void ClearParameterFilter()
        {
            SelectedParameter = null;
            ParameterValueFilter = string.Empty;
            ParameterFilterActive = false;
            ViewsView?.Refresh();
        }

        [RelayCommand]
        private void SelectAll() => SetSelectionForVisible(true);

        [RelayCommand]
        private void ClearSelection() => SetSelectionForVisible(false);

        private void SetSelectionForVisible(bool value)
        {
            _suppressCountRecompute = true;
            foreach (var v in ViewsView.Cast<ViewInfo>()) v.IsSelected = value;
            _suppressCountRecompute = false;
            RecomputeSelectedCount();
        }

        [RelayCommand]
        private void Refresh()
        {
            string? currentTb = SelectedTitleblock?.UniqueId;

            var scan = ViewCollector.Scan(_doc);
            LoadScan(scan);

            // Preserve the user's form; only re-resolve the titleblock if it vanished.
            SelectedTitleblock =
                Titleblocks.FirstOrDefault(t => t.UniqueId == currentTb)
                ?? Titleblocks.FirstOrDefault();

            ViewsView?.Refresh();
            AddLog(new LogEntry(LogLevel.Info, "View list refreshed."));
        }

        // ---- run --------------------------------------------------------------

        private bool CanRun() => !IsRunning && SelectedTitleblock != null;

        [RelayCommand(CanExecute = nameof(CanRun))]
        private void Run() => Launch(dryRun: false);

        [RelayCommand(CanExecute = nameof(CanRun))]
        private void DryRun() => Launch(dryRun: true);

        private void Launch(bool dryRun)
        {
            var selected = Views.Where(v => v.IsSelected).ToList();
            if (selected.Count == 0)
            {
                AddLog(new LogEntry(LogLevel.Warning, "No views selected."));
                return;
            }
            if (SelectedTitleblock == null)
            {
                AddLog(new LogEntry(LogLevel.Warning, "No titleblock selected."));
                return;
            }

            PersistSettings();

            PlacedCount = SkippedCount = FailedCount = 0;
            IsRunning = true;
            AddLog(new LogEntry(LogLevel.Info,
                dryRun ? "Starting dry run..." : "Starting placement..."));

            _handler.Request = new PlacementRequest
            {
                SelectedViews = selected,
                TitleblockTypeId = SelectedTitleblock.TypeId,
                SheetNamePrefix = SheetNamePrefix ?? string.Empty,
                Grouping = Grouping == "ViewType" ? GroupMode.ViewType : GroupMode.Discipline,
                SkipAlreadyPlaced = SkipAlreadyPlaced,
                ShowViewportTitles = ShowViewportTitles,
                SheetMarginMm = _settings.SheetMarginMm,
                ViewportGapMm = _settings.ViewportGapMm,
                TitleStripMm = _settings.TitleStripMm,
                DryRun = dryRun,
                Log = entry => UiInvoke(() => AddLog(entry)),
                OnComplete = (p, s, f) => UiInvoke(() =>
                {
                    PlacedCount = p; SkippedCount = s; FailedCount = f;
                    IsRunning = false;
                    AutoSaveLog();
                })
            };

            _event.Raise();
        }

        /// <summary>Marshal to the UI thread, tolerating a window closed mid-run.</summary>
        private void UiInvoke(Action action)
        {
            if (_dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished) return;
            try { _dispatcher.Invoke(action); }
            catch (Exception) { /* dispatcher torn down between the check and the call */ }
        }

        /// <summary>Called from the window on close: persist state and release the event.</summary>
        public void OnClosing()
        {
            PersistSettings();
            foreach (var v in Views) v.PropertyChanged -= ViewRow_PropertyChanged;
            try { _event?.Dispose(); } catch { /* already disposed */ }
        }

        // ---- logs -------------------------------------------------------------

        private void AddLog(LogEntry entry) => Logs.Add(entry);

        [RelayCommand]
        private void CopyAllLogs()
        {
            if (Logs.Count == 0) return;
            TrySetClipboard(string.Join(Environment.NewLine, Logs.Select(e => e.ToString())));
        }

        [RelayCommand]
        private void CopySelectedLogs()
        {
            if (SelectedLogs.Count == 0) return;
            TrySetClipboard(string.Join(Environment.NewLine, SelectedLogs.Select(e => e.ToString())));
        }

        private void TrySetClipboard(string text)
        {
            try
            {
                Clipboard.SetText(text);
            }
            catch (Exception ex)
            {
                AddLog(new LogEntry(LogLevel.Warning, $"Copy to clipboard failed: {ex.Message}"));
            }
        }

        [RelayCommand]
        private void ExportLogs() => AutoSaveLog(manual: true);

        private void AutoSaveLog(bool manual = false)
        {
            if (Logs.Count == 0) return;
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Revit26_Plugin", ToolName, "Logs");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir,
                    $"{ToolName}_Logs_{DateTime.Now:yyyy-MM-dd}_{DateTime.Now:HH-mm}.txt");

                var sb = new StringBuilder();
                foreach (var e in Logs) sb.AppendLine(e.ToString());
                File.WriteAllText(file, sb.ToString());

                if (manual)
                    AddLog(new LogEntry(LogLevel.Success, $"Logs exported: {file}"));
            }
            catch (Exception ex)
            {
                AddLog(new LogEntry(LogLevel.Warning, $"Log export failed: {ex.Message}"));
            }
        }

        // LogEntry.ToString() already yields "HH:mm:ss  Level  Message" — the
        // shared canonical format used for Copy/Export across all tools.

        private void PersistSettings()
        {
            _settings.TitleblockUniqueId = SelectedTitleblock?.UniqueId ?? string.Empty;
            _settings.SheetNamePrefix = SheetNamePrefix ?? string.Empty;
            _settings.Grouping = Grouping;
            _settings.SkipAlreadyPlaced = SkipAlreadyPlaced;
            _settings.ShowViewportTitles = ShowViewportTitles;
            _settings.Save();
        }
    }
}
