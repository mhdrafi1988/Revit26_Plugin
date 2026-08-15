using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.SheetAutoRearrange.V016.Core.Models;
using Revit26_Plugin.SheetAutoRearrange.V016.Core.Services;
using Revit26_Plugin.SheetAutoRearrange.V016.Infrastructure.ExternalEvents;
using Revit26_Plugin.SheetAutoRearrange.V016.Infrastructure.Helpers;
using Revit26_Plugin.Shared.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;

namespace Revit26_Plugin.SheetAutoRearrange.V016.UI.ViewModels
{
    /// <summary>
    /// V011 CHANGES (all per explicit request):
    ///  - Settings persistence: loads from and saves to
    ///    %AppData%\Revit26_Plugin\SheetAutoRearrange\settings.json on
    ///    construct / window close / after each Run, per suite convention.
    ///    Previously this tool had NO persistence at all — every setting
    ///    reset to its hardcoded default on every open.
    ///  - New Gap & Margin defaults (Top 30 / Bottom 150 / Left 30 /
    ///    Right 200mm) — set in GapSettings.cs, only takes effect on a
    ///    FIRST run (no existing settings.json yet); once persistence is in
    ///    place, a person's saved values always win over hardcoded defaults.
    ///  - New Sheet Position defaults: H = Left, V = Top (overrides the
    ///    V010 Right/Bottom defaults) — same first-run-only caveat.
    ///  - Layout Settings / Sheet Positioning split into two separate
    ///    expanders (previously one always-visible card) — see XAML.
    /// </summary>
    public partial class SheetAutoRearrangeViewModel : ObservableObject
    {
        private const string ToolName = "SheetAutoRearrange";

        private readonly UIDocument _uiDoc;
        private readonly SheetAutoRearrangeEventHandler _handler;
        private readonly ExternalEvent _externalEvent;
        private readonly Dispatcher _dispatcher;

        [ObservableProperty] private string lastLogFilePath = string.Empty;

        [ObservableProperty] private string sheetChipText = "No active sheet";
        [ObservableProperty] private bool isSheetActive;
        [ObservableProperty] private bool isBusy;

        [ObservableProperty] private ObservableCollection<ViewOnSheetItem> views = new();
        [ObservableProperty] private ObservableCollection<LogEntry> log = new();

        [ObservableProperty] private string filterText = string.Empty;

        // ── View Type filter popover state (not persisted — resets per session) ──
        [ObservableProperty] private bool showFloorPlan = true;
        [ObservableProperty] private bool showSection = true;
        [ObservableProperty] private bool showElevation = true;
        [ObservableProperty] private bool show3D = true;
        [ObservableProperty] private bool showLegend;
        [ObservableProperty] private bool showSchedule;
        [ObservableProperty] private bool showDrafting;

        // ── Layout Settings (Row Tolerance, Within-Row Align, Shelf
        // Sub-Row, + V012 NEW: Column Tolerance, Within-Column Align, Shelf
        // Sub-Col Align — moved here from Sheet Positioning). Defaults
        // below are FALLBACKS only — LoadPersistedSettings() overwrites
        // them with saved values if settings.json exists. ──
        [ObservableProperty] private double rowToleranceMm = 50;
        [ObservableProperty] private RowAlignment rowAlignment = RowAlignment.Bottom;

        /// <summary>
        /// V012 NEW: persisted, shown in Layout Settings, but NOT YET WIRED
        /// into any packing logic. A true mirror of Row Tolerance would
        /// need a second column-based packing pass — explicitly out of
        /// scope until confirmed. Flagging so this isn't mistaken for a
        /// working feature.
        /// </summary>
        [ObservableProperty] private double columnToleranceMm = 50;

        /// <summary>
        /// V012 NEW: persisted, shown in Layout Settings, but NOT YET WIRED
        /// into any packing logic — same caveat as ColumnToleranceMm.
        /// </summary>
        [ObservableProperty] private BlockAlignmentH withinColumnAlign = BlockAlignmentH.Right;

        // ── Sheet Positioning (Sheet Position H/V only — Shelf Sub-Col
        // Align moved OUT to Layout Settings per explicit request) ──
        /// <summary>V011: default changed to Left (was Right in V010) per explicit request.</summary>
        [ObservableProperty] private BlockAlignmentH columnAlignH = BlockAlignmentH.Left;

        /// <summary>V011: default changed to Top (was Bottom in V010) per explicit request.</summary>
        [ObservableProperty] private BlockAlignmentV columnAlignV = BlockAlignmentV.Top;

        // ── Overflow Handling ─────────────────────────────────────────────
        [ObservableProperty] private OverflowHandlingMode overflowHandlingMode = OverflowHandlingMode.PlaceWhatsPlaceable;

        // ── Gap & Margin Settings ─────────────────────────────────────────
        [ObservableProperty] private GapSettings gapSettings = new();

        // ── V014: Row Fill Strategy (replaces Tall/Wide View Detection) ───
        [ObservableProperty] private RowFillStrategy rowFillStrategy = RowFillStrategy.Shelf;

        [ObservableProperty] private bool isMultipleTitleBlocksWarning;

        // ── Expander state (NOT persisted — always collapsed on open, per
        // original confirmed design; only the underlying VALUES persist). ──
        [ObservableProperty] private bool isLayoutSettingsExpanded;
        [ObservableProperty] private bool isSheetPositioningExpanded;
        [ObservableProperty] private bool isRowFillStrategyExpanded;
        [ObservableProperty] private bool isLiveSheetPreviewExpanded;
        [ObservableProperty] private bool isOverflowHandlingExpanded;
        [ObservableProperty] private bool isGapMarginExpanded;

        // ── Metrics ───────────────────────────────────────────────────────
        [ObservableProperty] private int totalViewsMetric;
        [ObservableProperty] private int selectedMetric;
        [ObservableProperty] private int removedMetric;
        [ObservableProperty] private int placedOkMetric;
        [ObservableProperty] private int failedToFitMetric;

        private ViewSheet? _activeSheet;
        private PlaceableRegion? _region;

        private readonly SheetOrderPackingService _sheetOrderPreview = new();

        public SheetAutoRearrangeViewModel(UIDocument uiDoc)
        {
            _uiDoc = uiDoc;
            _dispatcher = Dispatcher.CurrentDispatcher;

            _handler = new SheetAutoRearrangeEventHandler();
            _handler.Completed += OnHandlerCompleted;

            _externalEvent = ExternalEvent.Create(_handler);

            LoadPersistedSettings();
            LoadActiveSheet();
        }

        /// <summary>
        /// V011 NEW: loads settings.json if it exists and applies every
        /// value onto this ViewModel's properties, overriding the
        /// [ObservableProperty] hardcoded defaults declared above. If no
        /// file exists yet (first run), SettingsService.Load returns a
        /// fresh SheetAutoRearrangeSettings with ITS OWN defaults — which
        /// mirror the same values already declared here, so behavior is
        /// identical either way on a true first run.
        /// </summary>
        private void LoadPersistedSettings()
        {
            var saved = SettingsService<SheetAutoRearrangeSettings>.Load(ToolName);

            GapSettings = saved.GapSettings;
            RowFillStrategy = saved.RowFillStrategy;
            RowToleranceMm = saved.RowToleranceMm;
            RowAlignment = saved.RowAlignment;
            ColumnToleranceMm = saved.ColumnToleranceMm;
            WithinColumnAlign = saved.WithinColumnAlign;
            ColumnAlignH = saved.ColumnAlignH;
            ColumnAlignV = saved.ColumnAlignV;
            OverflowHandlingMode = saved.OverflowHandlingMode;
        }

        /// <summary>
        /// V011 NEW: writes the current state of every persisted setting to
        /// settings.json. Called after each successful Run (so changes made
        /// mid-session survive even if the window crashes before close) and
        /// again on window Closing (code-behind calls this directly).
        /// </summary>
        public void SaveSettings()
        {
            var toSave = new SheetAutoRearrangeSettings
            {
                GapSettings = GapSettings,
                RowFillStrategy = RowFillStrategy,
                RowToleranceMm = RowToleranceMm,
                RowAlignment = RowAlignment,
                ColumnToleranceMm = ColumnToleranceMm,
                WithinColumnAlign = WithinColumnAlign,
                ColumnAlignH = ColumnAlignH,
                ColumnAlignV = ColumnAlignV,
                OverflowHandlingMode = OverflowHandlingMode
            };

            SettingsService<SheetAutoRearrangeSettings>.Save(ToolName, toSave);
        }

        private void LoadActiveSheet()
        {
            _activeSheet = _uiDoc.ActiveView as ViewSheet;

            if (_activeSheet == null)
            {
                IsSheetActive = false;
                SheetChipText = "Active view is not a sheet";
                Log.Add(new LogEntry(LogLevel.Warning, "Active view is not a sheet — open a sheet and reopen this tool."));
                return;
            }

            IsSheetActive = true;
            SheetChipText = $"Active Sheet: {_activeSheet.SheetNumber} — {_activeSheet.Name}";
            Log.Add(new LogEntry(LogLevel.Info, $"Active sheet detected: {_activeSheet.SheetNumber} — {_activeSheet.Name}."));

            RaiseLoad();
        }

        [RelayCommand]
        private void Refresh()
        {
            if (_activeSheet == null)
                return;

            Log.Add(new LogEntry(LogLevel.Info, "Refreshing views on sheet…"));
            RaiseLoad();
        }

        private void RaiseLoad()
        {
            IsBusy = true;
            _handler.Action = SheetAutoRearrangeAction.LoadViewsOnSheet;
            _handler.TargetSheet = _activeSheet;
            _handler.GapSettings = GapSettings;
            _externalEvent.Raise();
        }

        public System.Collections.Generic.List<PackedViewPlacement> PreviewPack()
        {
            var ticked = Views.Where(v => v.IsChecked).ToList();
            if (ticked.Count == 0 || _region == null)
                return new System.Collections.Generic.List<PackedViewPlacement>();

            return _sheetOrderPreview.Pack(ticked, _region, GapSettings,
                RowToleranceMm, RowAlignment, ColumnAlignH, ColumnAlignV,
                RowFillStrategy);
        }

        public PlaceableRegion? Region => _region;

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var v in FilteredViews)
                v.IsChecked = true;
            UpdateMetrics();
        }

        [RelayCommand]
        private void ClearAll()
        {
            foreach (var v in FilteredViews)
                v.IsChecked = false;
            UpdateMetrics();
        }

        public System.Collections.Generic.IEnumerable<ViewOnSheetItem> FilteredViews =>
            Views.Where(v =>
                (string.IsNullOrWhiteSpace(FilterText) || v.ViewName.Contains(FilterText, System.StringComparison.OrdinalIgnoreCase))
                && TypeIsVisible(v.ViewType));

        private bool TypeIsVisible(Autodesk.Revit.DB.ViewType viewType)
        {
            return viewType switch
            {
                Autodesk.Revit.DB.ViewType.FloorPlan or Autodesk.Revit.DB.ViewType.CeilingPlan or Autodesk.Revit.DB.ViewType.AreaPlan => ShowFloorPlan,
                Autodesk.Revit.DB.ViewType.Section => ShowSection,
                Autodesk.Revit.DB.ViewType.Elevation => ShowElevation,
                Autodesk.Revit.DB.ViewType.ThreeD => Show3D,
                Autodesk.Revit.DB.ViewType.Legend => ShowLegend,
                Autodesk.Revit.DB.ViewType.Schedule or Autodesk.Revit.DB.ViewType.PanelSchedule => ShowSchedule,
                Autodesk.Revit.DB.ViewType.DraftingView or Autodesk.Revit.DB.ViewType.Detail => ShowDrafting,
                _ => true
            };
        }

        [RelayCommand]
        private void Run()
        {
            if (_activeSheet == null || Views.Count == 0)
                return;

            if (IsMultipleTitleBlocksWarning)
            {
                Log.Add(new LogEntry(LogLevel.Warning, "Run blocked — sheet has multiple title blocks. Resolve to a single title block and Refresh."));
                return;
            }

            IsBusy = true;
            Log.Add(new LogEntry(LogLevel.Info, "Run started."));

            _handler.Action = SheetAutoRearrangeAction.RunRearrange;
            _handler.TargetSheet = _activeSheet;
            _handler.ItemsToProcess = Views.ToList();
            _handler.OverflowHandlingMode = OverflowHandlingMode;
            _handler.GapSettings = GapSettings;
            _handler.RowToleranceMm = RowToleranceMm;
            _handler.RowAlignment = RowAlignment;
            _handler.ColumnAlignmentH = ColumnAlignH;
            _handler.ColumnAlignmentV = ColumnAlignV;
            _handler.RowFillStrategy = RowFillStrategy;
            _handler.Log = Log;

            _externalEvent.Raise();
        }

        [RelayCommand]
        private void ExportLog()
        {
            if (Log.Count == 0)
                return;

            string fullPath = LogExportHelper.SaveToDefaultFolder(Log);
            LastLogFilePath = fullPath;
            Log.Add(new LogEntry(LogLevel.Success, $"Log exported to {fullPath}."));
        }

        [RelayCommand]
        private void CopyAllLog()
        {
            var text = string.Join(System.Environment.NewLine, Log.Select(l => l.ToString()));
            System.Windows.Clipboard.SetText(text);
        }

        [RelayCommand]
        private void ClearLog() => Log.Clear();

        private void OnHandlerCompleted()
        {
            _dispatcher.Invoke(() =>
            {
                IsBusy = false;

                switch (_handler.Action)
                {
                    case SheetAutoRearrangeAction.LoadViewsOnSheet:
                        Views = new ObservableCollection<ViewOnSheetItem>(_handler.LoadedItems ?? new());
                        ApplyDetectionResultToState();
                        UpdateMetrics();
                        Log.Add(new LogEntry(LogLevel.Info, $"Loaded {Views.Count} view(s) from sheet."));
                        break;

                    case SheetAutoRearrangeAction.RunRearrange:
                        var result = _handler.LastRunResult;
                        if (result != null)
                        {
                            TotalViewsMetric = result.TotalViews;
                            SelectedMetric = result.Selected;
                            RemovedMetric = result.Removed;
                            PlacedOkMetric = result.PlacedSuccessfully;
                            FailedToFitMetric = result.FailedToFit;

                            string fullPath = LogExportHelper.SaveToDefaultFolder(Log);
                            LastLogFilePath = fullPath;

                            // V011 NEW: persist settings after every Run, in
                            // addition to on window Closing — survives a
                            // crash/force-close between Run and normal close.
                            SaveSettings();
                        }

                        var removedIds = new System.Collections.Generic.HashSet<ElementId>(
                            Views.Where(v => !v.IsChecked).Select(v => v.ViewportId));
                        if (removedIds.Count > 0)
                        {
                            Views = new ObservableCollection<ViewOnSheetItem>(
                                Views.Where(v => !removedIds.Contains(v.ViewportId)));
                        }

                        UpdateMetrics();
                        break;
                }
            });
        }

        private void ApplyDetectionResultToState()
        {
            _region = _handler.Region;
            IsMultipleTitleBlocksWarning = _handler.MultipleTitleBlocksFound;

            if (_handler.MultipleTitleBlocksFound)
            {
                Log.Add(new LogEntry(LogLevel.Warning, "Sheet has multiple title blocks — Run is disabled until resolved to a single title block."));
                return;
            }

            if (_handler.NoTitleBlockFound || _region == null)
            {
                Log.Add(new LogEntry(LogLevel.Warning,
                    _handler.NoTitleBlockFound
                        ? "No title block found on sheet — Run will be blocked until one is added."
                        : "Title block geometry could not be read — Run will be blocked."));
                return;
            }

            Log.Add(new LogEntry(LogLevel.Info, "Title block detected — usable area computed from its bounding box, inset by configured margins."));
        }

        private void UpdateMetrics()
        {
            TotalViewsMetric = Views.Count;
            SelectedMetric = Views.Count(v => v.IsChecked);
        }
    }
}
