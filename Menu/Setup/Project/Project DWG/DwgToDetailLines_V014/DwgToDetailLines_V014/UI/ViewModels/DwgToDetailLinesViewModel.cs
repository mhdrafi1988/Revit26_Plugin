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
//        from MasterGuide.md section 10) â€” this window previously had no
//        Close button at all (modeless, closed only via the window chrome).
// Changes vs V013:
//   ADDED Line style shortlist + auto-assign (LineStyleMappingMode.Shortlist):
//         line layers map onto a few existing styles by colour / lineweight /
//         pattern, editable per row. No per-layer prompts, no new styles.
//   ADDED "Place beside CAD": created elements are offset right by one CAD
//         bounding-box width.
//   ADDED Settings (mode, placement, shortlist, layer mappings) persisted to
//         %AppData%\Revit26_Plugin\DwgToDetailLines\settings.json.
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
using Revit26_Plugin.DwgToDetailLines.V014.Core.Models;
using Revit26_Plugin.DwgToDetailLines.V014.Core.Services;
using Revit26_Plugin.DwgToDetailLines.V014.Infrastructure.ExternalEvents;
using Revit26_Plugin.Shared.Models;
using Revit26_Plugin.Shared.Services;

namespace Revit26_Plugin.DwgToDetailLines.V014.UI.ViewModels
{
    /// <summary>View model for the DWG To Detail Lines window.</summary>
    public partial class DwgToDetailLinesViewModel : ObservableObject
    {
        private const string SettingsKey = "DwgToDetailLines";
        private static readonly string[] DefaultShortlist = { "Thin Lines", "Medium Lines", "Wide Lines" };

        private readonly UIApplication _uiApp;
        private readonly ExternalEvent _convertEvent;
        private readonly ConvertExternalEventHandler _convertHandler;
        private readonly DwgToDetailLinesSettings _settings;
        private bool _suppressShortlistRebuild;

        public ObservableCollection<CadImportItem> AvailableCads { get; } = new();
        public ObservableCollection<LogEntry> LogEntries { get; } = new();
        public ObservableCollection<LayerRow> LayerRows { get; } = new();

        /// <summary>Grouped view of LayerRows for the grid â€” groups by EntityType (Lines / Hatches).</summary>
        public ICollectionView LayerRowsView { get; }

        public List<string> AvailableLineStyles { get; private set; } = new();
        public List<string> AvailableFillPatterns { get; private set; } = new();

        /// <summary>Every project line style, with a shortlist checkbox.</summary>
        public ObservableCollection<LineStyleOption> LineStyleOptions { get; } = new();

        /// <summary>Filtered view of <see cref="LineStyleOptions"/> for the shortlist list.</summary>
        public ICollectionView LineStyleOptionsView { get; }

        /// <summary>Names of the shortlisted styles — the choices in each line row's dropdown.</summary>
        public ObservableCollection<string> ShortlistNames { get; } = new();

        [ObservableProperty] private LineStyleMappingMode mappingMode;
        [ObservableProperty] private bool placeBesideCad;
        [ObservableProperty] private string lineStyleFilterText = string.Empty;
        [ObservableProperty] private string shortlistSummary = string.Empty;
        [ObservableProperty] private string cadSizeText = string.Empty;

        [ObservableProperty] private CadImportItem selectedCad;
        [ObservableProperty] private SplineHandlingMode splineHandlingMode;
        [ObservableProperty] private TransformMethod transformMethod = TransformMethod.None;
        [ObservableProperty] private string contextBannerText;
        [ObservableProperty] private ConversionMetrics metrics = ConversionMetrics.Empty;
        [ObservableProperty] private bool isRunning;
        [ObservableProperty] private double progress;                       // ToolWindowShell status strip (CLAUDE.md v1.1)
        [ObservableProperty] private string summaryText = string.Empty;    // ToolWindowShell status strip (CLAUDE.md v1.1)
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
        public RelayCommand AutoAssignCommand { get; }

        /// <summary>Set by the View's SelectionChanged handler for "Copy Selected".</summary>
        public IList SelectedLogEntries { get; set; }

        public event Action RequestClose;

        /// <summary>Builds the view model; must run on the Revit API thread.</summary>
        public DwgToDetailLinesViewModel(UIApplication app)
        {
            _uiApp = app;
            _settings = SettingsService<DwgToDetailLinesSettings>.Load(SettingsKey);
            _settings.Shortlist ??= new List<string>();
            _settings.LayerMappings ??= new Dictionary<string, string>();
            mappingMode = _settings.MappingMode;
            placeBesideCad = _settings.PlaceBesideCad;

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
            AutoAssignCommand = new RelayCommand(() => ApplyMappingToRows(useSaved: false), () => IsShortlistMode);
            LogEntries.CollectionChanged += (_, _) => CopyAllLogCommand.NotifyCanExecuteChanged();

            LineStyleOptionsView = CollectionViewSource.GetDefaultView(LineStyleOptions);
            LineStyleOptionsView.Filter = o => string.IsNullOrWhiteSpace(LineStyleFilterText)
                || (o is LineStyleOption opt && opt.Name.Contains(LineStyleFilterText, StringComparison.OrdinalIgnoreCase));

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

            LoadLineStyleOptions(doc);
        }

        /// <summary>True in Shortlist mapping mode.</summary>
        public bool IsShortlistMode => MappingMode == LineStyleMappingMode.Shortlist;

        private void LoadLineStyleOptions(Document doc)
        {
            foreach (var o in LineStyleOptions)
                o.PropertyChanged -= OnLineStyleOptionPropertyChanged;
            LineStyleOptions.Clear();

            var styles = ProjectCatalogService.GetLineStyles(doc);

            // First run (nothing saved): start from Revit's standard thin /
            // medium / wide styles, or the first three user styles.
            var wanted = new HashSet<string>(_settings.Shortlist, StringComparer.Ordinal);
            if (wanted.Count == 0)
            {
                foreach (string n in DefaultShortlist.Where(n => styles.Any(s => s.Name == n)))
                    wanted.Add(n);
                if (wanted.Count == 0)
                    foreach (var s in styles.Where(s => !s.Name.StartsWith("<")).Take(3))
                        wanted.Add(s.Name);
            }

            _suppressShortlistRebuild = true;
            foreach (var opt in styles)
            {
                opt.IsShortlisted = wanted.Contains(opt.Name);
                opt.PropertyChanged += OnLineStyleOptionPropertyChanged;
                LineStyleOptions.Add(opt);
            }
            _suppressShortlistRebuild = false;

            RebuildShortlist();
        }

        private void OnLineStyleOptionPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LineStyleOption.IsShortlisted) && !_suppressShortlistRebuild)
                RebuildShortlist();
        }

        /// <summary>
        /// Syncs <see cref="ShortlistNames"/> with the checked styles (incrementally, so
        /// row dropdowns keep their selection), then re-matches line rows whose style
        /// left the shortlist.
        /// </summary>
        private void RebuildShortlist()
        {
            var checkedNames = LineStyleOptions.Where(o => o.IsShortlisted).Select(o => o.Name).ToList();

            for (int i = ShortlistNames.Count - 1; i >= 0; i--)
                if (!checkedNames.Contains(ShortlistNames[i]))
                    ShortlistNames.RemoveAt(i);

            for (int i = 0; i < checkedNames.Count; i++)
                if (i >= ShortlistNames.Count || ShortlistNames[i] != checkedNames[i])
                    ShortlistNames.Insert(i, checkedNames[i]);

            ShortlistSummary = $"{checkedNames.Count} of {LineStyleOptions.Count} line styles shortlisted";

            if (IsShortlistMode)
            {
                var shortlist = Shortlist();
                foreach (var row in LayerRows.Where(r => r.EntityType == CadEntityType.Line))
                    if (row.ResolvedStyleName == null || !ShortlistNames.Contains(row.ResolvedStyleName))
                        row.ResolvedStyleName = LineStyleMatcher.BestMatch(row.LayerName, row.Appearance, shortlist);
            }

            ConvertCommand?.NotifyCanExecuteChanged();
        }

        private List<LineStyleOption> Shortlist() =>
            LineStyleOptions.Where(o => o.IsShortlisted).ToList();

        /// <summary>
        /// Sets each row's style column for the current mode. Shortlist mode: the
        /// remembered mapping when <paramref name="useSaved"/> and still shortlisted,
        /// else the best auto-match. Layer Name mode: the layer name itself.
        /// </summary>
        private void ApplyMappingToRows(bool useSaved)
        {
            var shortlist = Shortlist();

            foreach (var row in LayerRows)
            {
                if (row.EntityType != CadEntityType.Line)
                {
                    row.IsStyleEditable = false;
                    row.ResolvedStyleName = DefaultFillPattern;
                    continue;
                }

                if (!IsShortlistMode)
                {
                    row.IsStyleEditable = false;
                    row.ResolvedStyleName = row.LayerName;
                    continue;
                }

                row.IsStyleEditable = true;
                if (useSaved
                    && _settings.LayerMappings.TryGetValue(row.LayerName, out string saved)
                    && ShortlistNames.Contains(saved))
                {
                    row.ResolvedStyleName = saved;
                }
                else
                {
                    row.ResolvedStyleName = LineStyleMatcher.BestMatch(row.LayerName, row.Appearance, shortlist);
                }
            }
        }

        partial void OnMappingModeChanged(LineStyleMappingMode value)
        {
            OnPropertyChanged(nameof(IsShortlistMode));
            AutoAssignCommand?.NotifyCanExecuteChanged();
            ApplyMappingToRows(useSaved: true);
            ConvertCommand?.NotifyCanExecuteChanged();
        }

        partial void OnLineStyleFilterTextChanged(string value) => LineStyleOptionsView?.Refresh();

        /// <summary>Saves mode, placement, shortlist and the current layer mappings.</summary>
        public void SaveSettings()
        {
            _settings.MappingMode = MappingMode;
            _settings.PlaceBesideCad = PlaceBesideCad;
            _settings.Shortlist = LineStyleOptions.Where(o => o.IsShortlisted).Select(o => o.Name).ToList();

            if (IsShortlistMode)
                foreach (var row in LayerRows.Where(r => r.EntityType == CadEntityType.Line && r.ResolvedStyleName != null))
                    _settings.LayerMappings[row.LayerName] = row.ResolvedStyleName;

            SettingsService<DwgToDetailLinesSettings>.Save(SettingsKey, _settings);
        }

        private bool CanConvert() =>
            SelectedCad != null && !IsRunning && LayerRows.Any(r => r.IsSelected)
            && (!IsShortlistMode || ShortlistNames.Count > 0);

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
            UpdateCadSizeText();
            RefreshLayers();
        }

        private void UpdateCadSizeText()
        {
            CadSizeText = string.Empty;
            if (SelectedCad == null)
                return;

            var view = _uiApp.ActiveUIDocument.ActiveView;
            XYZ offset = CadPlacementService.GetBesideOffset(SelectedCad.ImportInstance, view, out double width);
            CadSizeText = offset == null
                ? "CAD bounding box not available — elements will be placed on the CAD."
                : "CAD width: " + UnitFormatUtils.Format(
                    _uiApp.ActiveUIDocument.Document.GetUnits(), SpecTypeId.Length, width, false);
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
            var appearances = ProjectCatalogService.GetLayerAppearances(SelectedCad.ImportInstance);

            foreach (var row in rows.OrderBy(r => r.LayerName, StringComparer.OrdinalIgnoreCase))
            {
                appearances.TryGetValue(row.LayerName, out CadLayerAppearance a);
                row.Appearance = a;
                row.Swatch = ProjectCatalogService.ToBrush(a);
                LayerRows.Add(row);
            }

            ApplyMappingToRows(useSaved: true);

            foreach (var row in LayerRows)
                row.PropertyChanged += OnLayerRowPropertyChanged;

            // Default selected item = first row (current screen's first object), per spec.
            SelectedLayerRow = LayerRows.FirstOrDefault();

            ConvertCommand.NotifyCanExecuteChanged();
        }

        private void OnLayerRowPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LayerRow.IsSelected))
                ConvertCommand.NotifyCanExecuteChanged();

            // A recycled row dropdown can push null when its item list changes;
            // never leave an editable line row without a style.
            if (e.PropertyName == nameof(LayerRow.ResolvedStyleName)
                && sender is LayerRow row && row.IsStyleEditable && row.ResolvedStyleName == null)
            {
                row.ResolvedStyleName = LineStyleMatcher.BestMatch(row.LayerName, row.Appearance, Shortlist());
            }
        }

        private void SetAllVisible(bool value)
        {
            // Scoped to visible/filtered rows only, per DataGrid spec.
            foreach (var row in LayerRowsView.Cast<LayerRow>())
                row.IsSelected = value;

            ConvertCommand.NotifyCanExecuteChanged();
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
                System.Windows.Clipboard.SetText(string.Join(Environment.NewLine, LogEntries.Select(e => e.ToString())));
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // Clipboard can transiently fail if another process holds it
                // (common on Windows). Not worth surfacing as a hard error â€”
                // log it so the attempt isn't silently swallowed.
                AddLog(LogLevel.Warning, "Copy to clipboard failed (clipboard busy) â€” try again.");
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
                System.Windows.Clipboard.SetText(string.Join(
                    Environment.NewLine,
                    SelectedLogEntries.Cast<LogEntry>().Select(e => e.ToString())));
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                AddLog(LogLevel.Warning, "Copy to clipboard failed (clipboard busy) â€” try again.");
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
            bool besideCad = PlaceBesideCad;

            Dictionary<string, string> styleMap = IsShortlistMode
                ? LayerRows
                    .Where(r => r.IsSelected && r.EntityType == CadEntityType.Line && r.ResolvedStyleName != null)
                    .ToDictionary(r => r.LayerName, r => r.ResolvedStyleName)
                : null;

            SaveSettings();

            _convertHandler.Raise(_convertEvent, uiApp =>
            {
                try
                {
                    var service = new DetailLineConversionService(uiApp, e => LogEntries.Add(e));

                    XYZ offset = null;
                    if (besideCad)
                    {
                        offset = CadPlacementService.GetBesideOffset(cad, uiApp.ActiveUIDocument.ActiveView, out double width);
                        AddLog(offset != null ? LogLevel.Info : LogLevel.Warning, offset != null
                            ? "Placing beside CAD: offset right by " + UnitFormatUtils.Format(
                                uiApp.ActiveUIDocument.Document.GetUnits(), SpecTypeId.Length, width, false)
                            : "CAD bounding box not available — elements placed on the CAD.");
                    }

                    AddLog(LogLevel.Info, styleMap != null
                        ? $"Line style mapping: shortlist ({styleMap.Values.Distinct().Count()} style(s) used)"
                        : "Line style mapping: by layer name");

                    var updatedMetrics = service.Execute(
                        cad, spline, transform, entityCount, layerCount,
                        selectedLineLayers, selectedHatchLayers,
                        lineStyle, fillPattern, styleMap, offset);

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

