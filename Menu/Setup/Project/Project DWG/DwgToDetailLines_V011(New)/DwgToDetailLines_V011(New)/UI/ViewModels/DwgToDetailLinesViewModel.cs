// ==============================================
// File: DwgToDetailLinesViewModel.cs
// Layer: UI/ViewModels
// Changes vs V010:
//   FIX  AvailableLineStyles / AvailableFillPatterns were hardcoded
//        placeholder strings, not read from the project. Now populated
//        from ProjectCatalogService (real OST_Lines subcategories /
//        FilledRegionType names) on construction and refresh.
//   FIX  Per-tool LogEntryViewModel(Message,Color) replaced with the
//        shared LogEntry(Level,Message) model, matching every other tool
//        in the suite.
//   ADDED Close/Copy Selected/Export log commands (log panel convention
//        from MasterGuide.md section 10) — this window previously had no
//        Close button at all (modeless, closed only via the window chrome).
// ==============================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using Revit26_Plugin.DwgToDetailLines.V011.Core.Models;
using Revit26_Plugin.DwgToDetailLines.V011.Core.Services;
using Revit26_Plugin.DwgToDetailLines.V011.Infrastructure.ExternalEvents;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.DwgToDetailLines.V011.UI.ViewModels
{
    public partial class DwgToDetailLinesViewModel : ObservableObject
    {
        private readonly UIApplication _uiApp;
        private readonly ExternalEvent _convertEvent;
        private readonly ConvertExternalEventHandler _convertHandler;

        public ObservableCollection<CadImportItem> AvailableCads { get; } = new();
        public ObservableCollection<LogEntry> LogEntries { get; } = new();
        public ObservableCollection<LayerRow> LayerRows { get; } = new();

        /// <summary>Grouped view of LayerRows for the grid — groups by EntityType (Lines / Hatches).</summary>
        public ICollectionView LayerRowsView { get; }

        public List<string> AvailableLineStyles { get; private set; } = new();
        public List<string> AvailableFillPatterns { get; private set; } = new();

        [ObservableProperty] private CadImportItem selectedCad;
        [ObservableProperty] private SplineHandlingMode splineHandlingMode;
        [ObservableProperty] private TransformMethod transformMethod = TransformMethod.None;
        [ObservableProperty] private string contextBannerText;
        [ObservableProperty] private ConversionMetrics metrics = ConversionMetrics.Empty;
        [ObservableProperty] private bool isRunning;
        [ObservableProperty] private string layerFilterText = string.Empty;
        [ObservableProperty] private string defaultLineStyle;
        [ObservableProperty] private string defaultFillPattern;
        [ObservableProperty] private LayerRow selectedLayerRow;

        public RelayCommand ConvertCommand { get; }
        public RelayCommand SelectAllCommand { get; }
        public RelayCommand ClearCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand CloseCommand { get; }
        public RelayCommand CopyAllLogCommand { get; }
        public RelayCommand CopySelectedLogCommand { get; }
        public RelayCommand ExportLogCommand { get; }

        /// <summary>Set by the View's SelectionChanged handler for "Copy Selected".</summary>
        public IList SelectedLogEntries { get; set; }

        public event Action RequestClose;

        public DwgToDetailLinesViewModel(UIApplication app)
        {
            _uiApp = app;

            // Must be created while on the main Revit API thread (here, during
            // window construction inside Command.Execute's valid API context).
            _convertHandler = new ConvertExternalEventHandler();
            _convertEvent = ExternalEvent.Create(_convertHandler);

            ConvertCommand = new RelayCommand(Convert, CanConvert);
            SelectAllCommand = new RelayCommand(() => SetAllVisible(true));
            ClearCommand = new RelayCommand(() => SetAllVisible(false));
            RefreshCommand = new RelayCommand(RefreshLayers);
            CloseCommand = new RelayCommand(() => RequestClose?.Invoke());
            CopyAllLogCommand = new RelayCommand(CopyAllLog, () => LogEntries.Count > 0);
            CopySelectedLogCommand = new RelayCommand(CopySelectedLog);
            ExportLogCommand = new RelayCommand(ExportLog);
            LogEntries.CollectionChanged += (_, _) => CopyAllLogCommand.NotifyCanExecuteChanged();

            RefreshCatalog();

            LayerRowsView = CollectionViewSource.GetDefaultView(LayerRows);
            LayerRowsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(LayerRow.EntityType)));
            LayerRowsView.Filter = FilterLayerRow;

            var activeView = _uiApp.ActiveUIDocument.ActiveView;
            ContextBannerText =
                $"Context: Drafting View \"{activeView.Name}\"\n" +
                "Detail Lines and Filled Regions will be created in the active view.";

            LoadCadImports();
        }

        private void RefreshCatalog()
        {
            Document doc = _uiApp.ActiveUIDocument.Document;

            AvailableLineStyles = ProjectCatalogService.GetLineStyleNames(doc);
            AvailableFillPatterns = ProjectCatalogService.GetFilledRegionTypeNames(doc);

            DefaultLineStyle = AvailableLineStyles.FirstOrDefault();
            DefaultFillPattern = AvailableFillPatterns.FirstOrDefault();
        }

        private bool CanConvert() =>
            SelectedCad != null && !IsRunning && LayerRows.Any(r => r.IsSelected);

        private bool FilterLayerRow(object obj)
        {
            if (string.IsNullOrWhiteSpace(LayerFilterText))
                return true;

            return obj is LayerRow row &&
                   row.LayerName.Contains(LayerFilterText, System.StringComparison.OrdinalIgnoreCase);
        }

        partial void OnLayerFilterTextChanged(string value) => LayerRowsView.Refresh();

        private void LoadCadImports()
        {
            var items = new CadImportCollectorService(_uiApp).GetAllCadImports();
            foreach (var item in items)
                AvailableCads.Add(item);

            // Default selected item = first CAD import in the list, per spec.
            if (AvailableCads.Count > 0)
                SelectedCad = AvailableCads[0];
        }

        partial void OnSelectedCadChanged(CadImportItem value)
        {
            ConvertCommand.NotifyCanExecuteChanged();
            RefreshLayers();
        }

        private void RefreshLayers()
        {
            foreach (var row in LayerRows)
                row.PropertyChanged -= OnLayerRowPropertyChanged;

            LayerRows.Clear();

            if (SelectedCad == null)
            {
                Metrics = ConversionMetrics.Empty;
                ConvertCommand.NotifyCanExecuteChanged();
                return;
            }

            var activeView = _uiApp.ActiveUIDocument.ActiveView;
            var doc = _uiApp.ActiveUIDocument.Document;

            var (entityCount, layerCount) = CadGeometryExtractor.PreScan(
                SelectedCad.ImportInstance, doc, activeView);

            Metrics = new ConversionMetrics
            {
                LayersFound = layerCount,
                Entities = entityCount,
                Placed = null,
                Skipped = null,
                Failed = null
            };

            var rows = CadGeometryExtractor.ScanLayers(SelectedCad.ImportInstance, doc, activeView);

            foreach (var row in rows)
            {
                row.ResolvedStyleName = row.EntityType == CadEntityType.Line
                    ? DefaultLineStyle
                    : DefaultFillPattern;
                row.PropertyChanged += OnLayerRowPropertyChanged;
                LayerRows.Add(row);
            }

            // Default selected item = first row (current screen's first object), per spec.
            SelectedLayerRow = LayerRows.FirstOrDefault();

            ConvertCommand.NotifyCanExecuteChanged();
        }

        private void OnLayerRowPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LayerRow.IsSelected))
                ConvertCommand.NotifyCanExecuteChanged();
        }

        private void SetAllVisible(bool value)
        {
            // Scoped to visible/filtered rows only, per DataGrid spec.
            foreach (var row in LayerRowsView.Cast<LayerRow>())
                row.IsSelected = value;

            ConvertCommand.NotifyCanExecuteChanged();
        }

        partial void OnDefaultLineStyleChanged(string value)
        {
            foreach (var row in LayerRows.Where(r => r.EntityType == CadEntityType.Line))
                row.ResolvedStyleName = value;
        }

        partial void OnDefaultFillPatternChanged(string value)
        {
            foreach (var row in LayerRows.Where(r => r.EntityType == CadEntityType.Hatch))
                row.ResolvedStyleName = value;
        }

        private void AddLog(LogLevel level, string message)
            => LogEntries.Add(new LogEntry(level, message));

        private void CopyAllLog()
        {
            if (LogEntries.Count == 0)
                return;

            try
            {
                Clipboard.SetText(string.Join(Environment.NewLine, LogEntries.Select(e => e.ToString())));
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // Clipboard can transiently fail if another process holds it
                // (common on Windows). Not worth surfacing as a hard error —
                // log it so the attempt isn't silently swallowed.
                AddLog(LogLevel.Warning, "Copy to clipboard failed (clipboard busy) — try again.");
            }
        }

        private void CopySelectedLog()
        {
            if (SelectedLogEntries == null || SelectedLogEntries.Count == 0)
            {
                AddLog(LogLevel.Warning, "No log rows selected.");
                return;
            }

            try
            {
                Clipboard.SetText(string.Join(
                    Environment.NewLine,
                    SelectedLogEntries.Cast<LogEntry>().Select(e => e.ToString())));
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                AddLog(LogLevel.Warning, "Copy to clipboard failed (clipboard busy) — try again.");
            }
        }

        private void ExportLog()
        {
            if (LogEntries.Count == 0)
            {
                AddLog(LogLevel.Warning, "Nothing to export.");
                return;
            }

            var dialog = new SaveFileDialog
            {
                FileName = $"DwgToDetailLines_Logs_{DateTime.Now:yyyy-MM-dd_HH-mm}.txt",
                Filter = "Text file (*.txt)|*.txt"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                File.WriteAllLines(dialog.FileName, LogEntries.Select(e => e.ToString()));
                AddLog(LogLevel.Info, $"Log exported to '{dialog.FileName}'");
            }
            catch (Exception ex)
            {
                AddLog(LogLevel.Error, $"Export failed: {ex.Message}");
            }
        }

        private void Convert()
        {
            IsRunning = true;
            ConvertCommand.NotifyCanExecuteChanged();

            var cad = SelectedCad.ImportInstance;
            var spline = SplineHandlingMode;
            var transform = TransformMethod;
            int entityCount = Metrics.Entities;
            int layerCount = Metrics.LayersFound;

            var selectedLineLayers = LayerRows
                .Where(r => r.IsSelected && r.EntityType == CadEntityType.Line)
                .Select(r => r.LayerName)
                .ToList();

            var selectedHatchLayers = LayerRows
                .Where(r => r.IsSelected && r.EntityType == CadEntityType.Hatch)
                .Select(r => r.LayerName)
                .ToList();

            string lineStyle = DefaultLineStyle;
            string fillPattern = DefaultFillPattern;

            _convertHandler.Raise(_convertEvent, uiApp =>
            {
                try
                {
                    var service = new DetailLineConversionService(uiApp, e => LogEntries.Add(e));

                    var updatedMetrics = service.Execute(
                        cad, spline, transform, entityCount, layerCount,
                        selectedLineLayers, selectedHatchLayers,
                        lineStyle, fillPattern);

                    Metrics = updatedMetrics;
                }
                catch (Exception ex)
                {
                    AddLog(LogLevel.Error, ex.Message);
                    TaskDialog.Show("DWG to Detail Lines", $"Conversion failed:\n{ex.Message}");
                }
                finally
                {
                    IsRunning = false;
                    ConvertCommand.NotifyCanExecuteChanged();
                }
            });
        }
    }
}
