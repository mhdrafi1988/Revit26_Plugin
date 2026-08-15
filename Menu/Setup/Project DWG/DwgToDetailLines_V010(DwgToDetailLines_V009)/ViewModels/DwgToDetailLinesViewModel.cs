using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media;
using System.ComponentModel;
using Revit26_Plugin.DwgToDetailLines.V010.Helpers;
using Revit26_Plugin.DwgToDetailLines.V010.Models;
using Revit26_Plugin.DwgToDetailLines.V010.Services;

namespace Revit26_Plugin.DwgToDetailLines.V010.ViewModels
{
    public partial class DwgToDetailLinesViewModel : ObservableObject
    {
        private readonly UIApplication _uiApp;
        private readonly ExternalEvent _convertEvent;
        private readonly ConvertExternalEventHandler _convertHandler;

        public ObservableCollection<CadImportItem> AvailableCads { get; } = new();
        public ObservableCollection<LogEntryViewModel> LogEntries { get; } = new();
        public ObservableCollection<LayerRow> LayerRows { get; } = new();

        /// <summary>Grouped view of LayerRows for the grid — groups by EntityType (Lines / Hatches).</summary>
        public ICollectionView LayerRowsView { get; }

        // Fixed catalog offered in the two default dropdowns. ASSUMPTION (flagged):
        // these are placeholder display names, not read from live Revit project
        // categories/patterns — needs confirm on whether the combo should instead
        // be populated from actual existing OST_Lines subcategories / FillPatternElements.
        public List<string> AvailableLineStyles { get; } = new() { "Thin Lines", "Medium Lines", "Wide Lines" };
        public List<string> AvailableFillPatterns { get; } = new() { "Diagonal Crosshatch", "Solid Fill", "Sand" };

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
        public RelayCommand CopyAllLogCommand { get; }

        public DwgToDetailLinesViewModel(UIApplication app)
        {
            _uiApp = app;

            // Must be created while on the main Revit API thread (here, during
            // window construction inside LaunchCommand.Execute's valid API context).
            _convertHandler = new ConvertExternalEventHandler();
            _convertEvent = ExternalEvent.Create(_convertHandler);

            ConvertCommand = new RelayCommand(Convert, CanConvert);
            SelectAllCommand = new RelayCommand(() => SetAllVisible(true));
            ClearCommand = new RelayCommand(() => SetAllVisible(false));
            RefreshCommand = new RelayCommand(RefreshLayers);
            CopyAllLogCommand = new RelayCommand(CopyAllLog, () => LogEntries.Count > 0);
            LogEntries.CollectionChanged += (_, _) => CopyAllLogCommand.NotifyCanExecuteChanged();

            DefaultLineStyle = AvailableLineStyles.FirstOrDefault();
            DefaultFillPattern = AvailableFillPatterns.FirstOrDefault();

            LayerRowsView = CollectionViewSource.GetDefaultView(LayerRows);
            LayerRowsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(LayerRow.EntityType)));
            LayerRowsView.Filter = FilterLayerRow;

            var activeView = _uiApp.ActiveUIDocument.ActiveView;
            ContextBannerText =
                $"Context: Drafting View \"{activeView.Name}\"\n" +
                "Detail Lines and Filled Regions will be created in the active view.";

            LoadCadImports();
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

        private void OnLayerRowPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
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

        private void AppendLog(string message, Brush color)
        {
            LogEntries.Add(new LogEntryViewModel(message, color));
        }

        /// <summary>
        /// Copies the full log (all entries, in order) to the clipboard as
        /// plain text, one line per entry — for pasting into a bug report,
        /// email, or the Export .txt file's contents.
        /// </summary>
        private void CopyAllLog()
        {
            if (LogEntries.Count == 0)
                return;

            string text = string.Join(
                System.Environment.NewLine,
                LogEntries.Select(e => e.Message));

            try
            {
                System.Windows.Clipboard.SetText(text);
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // Clipboard can transiently fail if another process holds it
                // (common on Windows). Not worth surfacing as a hard error —
                // log it so the attempt isn't silently swallowed.
                AppendLog("[WARN] Copy to clipboard failed (clipboard busy) — try again.", Brushes.Orange);
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
                    var service = new DetailLineConversionService(uiApp, AppendLog);

                    var updatedMetrics = service.Execute(
                        cad, spline, transform, entityCount, layerCount,
                        selectedLineLayers, selectedHatchLayers,
                        lineStyle, fillPattern);

                    Metrics = updatedMetrics;
                }
                catch (System.Exception ex)
                {
                    AppendLog($"[ERROR] {ex.Message}", Brushes.Red);
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
