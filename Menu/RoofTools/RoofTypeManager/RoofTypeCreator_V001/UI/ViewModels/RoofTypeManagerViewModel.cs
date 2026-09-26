using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Revit26_Plugin.RoofTypeCreator.V001.Core.Models;
using Revit26_Plugin.RoofTypeCreator.V001.Infrastructure.ExternalEvents;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoofTypeCreator.V001.UI.ViewModels
{
    public partial class RoofTypeManagerViewModel : ObservableObject, IDisposable
    {
        private readonly UIApplication _uiApp;
        private readonly RoofTypeExternalEventHandler _handler;
        private readonly ExternalEvent _externalEvent;
        private readonly Dispatcher _dispatcher;

        // ── Observable Collections ──────────────────────────────
        public ObservableCollection<RoofTypeItem>     AllRoofTypes        { get; } = new();
        public ObservableCollection<RoofTypeItem>     FilteredRoofTypes   { get; } = new();
        public ObservableCollection<ImportPreviewItem> ImportPreviewItems  { get; } = new();
        public ObservableCollection<LogEntry>          LogEntries          { get; } = new();

        // ── Metrics ─────────────────────────────────────────────
        [ObservableProperty] private int    totalRoofTypes;
        [ObservableProperty] private string lastExportDate = "—";
        [ObservableProperty] private string lastImportFile = "—";

        // ── State ───────────────────────────────────────────────
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ExportSelectedCommand))]
        [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
        [NotifyCanExecuteChangedFor(nameof(ImportCommand))]
        private bool isBusy;

        [ObservableProperty] private string filterText          = "";
        [ObservableProperty] private string exportFilePath      = "";
        [ObservableProperty] private string importFilePath      = "";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ExportSelectedCommand))]
        private int selectedExportCount;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ImportCommand))]
        private int newImportCount;

        // ── Header checkbox for export grid ─────────────────────
        [ObservableProperty] private bool allExportItemsSelected;
        partial void OnAllExportItemsSelectedChanged(bool value)
        {
            foreach (var item in FilteredRoofTypes) item.IsSelected = value;
            UpdateSelectedCount();
        }

        // ── Completion summary ───────────────────────────────────
        [ObservableProperty] private int importedCount;
        [ObservableProperty] private int duplicateCount;
        [ObservableProperty] private int failedCount;
        [ObservableProperty] private string lastRunTime = "";

        // ── Ctor ────────────────────────────────────────────────
        public RoofTypeManagerViewModel(UIApplication uiApp)
        {
            _uiApp      = uiApp;
            _dispatcher = Dispatcher.CurrentDispatcher;

            _handler = new RoofTypeExternalEventHandler
            {
                OnTypesLoaded   = items    => _dispatcher.Invoke(() => ApplyLoadedTypes(items)),
                OnExportDone    = path     => _dispatcher.Invoke(() => ApplyExportDone(path)),
                OnPreviewLoaded = items    => _dispatcher.Invoke(() => ApplyPreview(items)),
                OnImportDone    = result   => _dispatcher.Invoke(() => ApplyImportDone(result)),
                OnLog           = (lv, msg)=> _dispatcher.Invoke(() => AppendLog(lv, msg))
            };

            _externalEvent = ExternalEvent.Create(_handler);
        }

        // ── Property callbacks ──────────────────────────────────
        partial void OnFilterTextChanged(string value) => RebuildFilter();

        // ── Commands ────────────────────────────────────────────

        [RelayCommand]
        private void WindowLoaded() => RaiseRequest(RoofTypeRequest.LoadTypes);

        [RelayCommand(CanExecute = nameof(CanRefresh))]
        private void Refresh() => RaiseRequest(RoofTypeRequest.LoadTypes);
        private bool CanRefresh() => !IsBusy;

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var item in FilteredRoofTypes) item.IsSelected = true;
            UpdateSelectedCount();
        }

        [RelayCommand]
        private void ClearSelection()
        {
            foreach (var item in FilteredRoofTypes) item.IsSelected = false;
            UpdateSelectedCount();
        }

        [RelayCommand]
        private void BrowseExport()
        {
            var dlg = new SaveFileDialog
            {
                Filter   = "Excel Files (*.xlsx)|*.xlsx",
                FileName = $"RoofTypes_Export_{DateTime.Now:yyyy-MM-dd}.xlsx"
            };
            if (dlg.ShowDialog() == true) ExportFilePath = dlg.FileName;
        }

        [RelayCommand(CanExecute = nameof(CanExport))]
        private void ExportSelected()
        {
            if (string.IsNullOrWhiteSpace(ExportFilePath)) { BrowseExport(); }
            if (string.IsNullOrWhiteSpace(ExportFilePath)) return;

            _handler.ItemsToExport  = FilteredRoofTypes.Where(i => i.IsSelected).ToList();
            _handler.ExportFilePath = ExportFilePath;
            RaiseRequest(RoofTypeRequest.ExportSelected);
        }
        private bool CanExport() => !IsBusy && SelectedExportCount > 0;

        [RelayCommand]
        private void BrowseImport()
        {
            var dlg = new OpenFileDialog { Filter = "Excel Files (*.xlsx)|*.xlsx" };
            if (dlg.ShowDialog() != true) return;

            ImportFilePath       = dlg.FileName;
            _handler.ImportFilePath = ImportFilePath;
            LastImportFile       = Path.GetFileName(ImportFilePath);
            RaiseRequest(RoofTypeRequest.PreviewImport);
        }

        [RelayCommand(CanExecute = nameof(CanImport))]
        private void Import()
        {
            _handler.ItemsToImport = ImportPreviewItems.ToList();
            RaiseRequest(RoofTypeRequest.ImportNew);
        }
        private bool CanImport() => !IsBusy && NewImportCount > 0;

        [RelayCommand]
        private void CopyAllLogs()
        {
            var text = string.Join(Environment.NewLine, LogEntries.Select(l => l.ToString()));
            try { Clipboard.SetText(text); } catch { /* clipboard may be locked */ }
        }

        // ── Helpers ─────────────────────────────────────────────
        private void RaiseRequest(RoofTypeRequest req)
        {
            IsBusy = true;
            _handler.PendingRequest = req;
            _externalEvent.Raise();
        }

        private void RebuildFilter()
        {
            foreach (var item in FilteredRoofTypes)
                item.PropertyChanged -= OnItemPropertyChanged;

            FilteredRoofTypes.Clear();

            var query = string.IsNullOrWhiteSpace(FilterText)
                ? AllRoofTypes.AsEnumerable()
                : AllRoofTypes.Where(i =>
                    i.TypeName.IndexOf(FilterText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    i.TypeMark.IndexOf(FilterText, StringComparison.OrdinalIgnoreCase) >= 0);

            foreach (var item in query)
            {
                item.PropertyChanged += OnItemPropertyChanged;
                FilteredRoofTypes.Add(item);
            }

            UpdateSelectedCount();
        }

        private void OnItemPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RoofTypeItem.IsSelected))
                UpdateSelectedCount();
        }

        private void UpdateSelectedCount()
        {
            SelectedExportCount = FilteredRoofTypes.Count(i => i.IsSelected);
        }

        private void AppendLog(string levelName, string message)
        {
            if (!Enum.TryParse<LogLevel>(levelName, ignoreCase: true, out var level))
                level = LogLevel.Info;
            LogEntries.Add(new LogEntry(level, message));
        }

        // ── External event callbacks ─────────────────────────────
        private void ApplyLoadedTypes(List<RoofTypeItem> items)
        {
            AllRoofTypes.Clear();
            foreach (var item in items) AllRoofTypes.Add(item);
            TotalRoofTypes = items.Count;
            RebuildFilter();
            IsBusy = false;
        }

        private void ApplyExportDone(string path)
        {
            if (path != null) LastExportDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            IsBusy = false;
        }

        private void ApplyPreview(List<ImportPreviewItem> items)
        {
            ImportPreviewItems.Clear();
            foreach (var item in items) ImportPreviewItems.Add(item);
            NewImportCount = items.Count(i => i.Status == ImportStatus.New);
            IsBusy = false;
        }

        private void ApplyImportDone(ImportResult result)
        {
            ImportedCount   = result.Created;
            DuplicateCount  = result.Duplicates;
            FailedCount     = result.Failed;
            LastRunTime     = $"Last run: {DateTime.Now:HH:mm:ss}";
            IsBusy = false;

            if (result.Created > 0) RaiseRequest(RoofTypeRequest.LoadTypes);
        }

        // ── IDisposable ──────────────────────────────────────────
        public void Dispose()
        {
            foreach (var item in FilteredRoofTypes)
                item.PropertyChanged -= OnItemPropertyChanged;
            _externalEvent?.Dispose();
        }
    }
}
