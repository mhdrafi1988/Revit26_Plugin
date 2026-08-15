using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.SheetAutoRearrange.V010.Core.Models;
using Revit26_Plugin.SheetAutoRearrange.V010.Core.Services;
using Revit26_Plugin.SheetAutoRearrange.V010.Infrastructure.ExternalEvents;
using Revit26_Plugin.SheetAutoRearrange.V010.Infrastructure.Helpers;
using Revit26_Plugin.Shared.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;

namespace Revit26_Plugin.SheetAutoRearrange.V010.UI.ViewModels
{
    /// <summary>
    /// V009 CHANGES (all per explicit request):
    ///  - Placeable Area UI removed entirely (detection still runs
    ///    internally on Load/Run, just not shown or user-overridable —
    ///    ManualRegionOverride, Redetect, ApplyManualArea all removed).
    ///  - Rearrange Method UI removed — Sheet Order is now the only
    ///    algorithm, hardcoded (SelectedAlgorithm / RearrangeAlgorithm
    ///    property removed).
    ///  - Row Align, Row Tolerance, and Column Align (renamed from
    ///    BlockAlignH/V — same underlying mechanism) moved to an always-
    ///    visible section at the top of the right panel, not an expander.
    ///  - Gap & Margin Settings simplified to a single global H/V gap
    ///    (GapSettings.GlobalHorizontalGapMm/VerticalGapMm) — no more
    ///    per-ViewType groups, ViewTypeGroupResolver removed.
    /// </summary>
    public partial class SheetAutoRearrangeViewModel : ObservableObject
    {
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

        // ── View Type filter popover state ──────────────────────────────
        [ObservableProperty] private bool showFloorPlan = true;
        [ObservableProperty] private bool showSection = true;
        [ObservableProperty] private bool showElevation = true;
        [ObservableProperty] private bool show3D = true;
        [ObservableProperty] private bool showLegend;
        [ObservableProperty] private bool showSchedule;
        [ObservableProperty] private bool showDrafting;

        // ── Layout Settings (V009 — always-visible top section, not an
        // expander; replaces the removed Rearrange Method card) ──────────
        [ObservableProperty] private double rowToleranceMm = 50;
        // V010: defaults changed to match stated overall preference ("most
        // of my alignment are bottom for rows and right for columns").
        // ColumnAlignV wasn't explicitly specified — defaulted to Bottom
        // too, flagged as an assumption; change back to Top if that's wrong.
        [ObservableProperty] private RowAlignment rowAlignment = RowAlignment.Bottom;

        /// <summary>"Sheet Position H" in the UI — reuses BlockAlignmentH internally.</summary>
        [ObservableProperty] private BlockAlignmentH columnAlignH = BlockAlignmentH.Right;

        /// <summary>"Sheet Position V" in the UI — reuses BlockAlignmentV internally. ASSUMPTION: defaulted to Bottom (unconfirmed) — flag if Top was intended.</summary>
        [ObservableProperty] private BlockAlignmentV columnAlignV = BlockAlignmentV.Bottom;

        // ── Overflow Handling ─────────────────────────────────────────────
        [ObservableProperty] private OverflowHandlingMode overflowHandlingMode = OverflowHandlingMode.PlaceWhatsPlaceable;

        // ── Gap & Margin Settings (V009: flat global gap, no groups) ───────
        [ObservableProperty] private GapSettings gapSettings = new();

        // ── Tall / Wide View Detection ─────────────────────────────────────
        [ObservableProperty] private TallWideDetectionSettings tallDetectionSettings = new();
        [ObservableProperty] private TallWideDetectionSettings wideDetectionSettings = new();

        [ObservableProperty] private bool isMultipleTitleBlocksWarning;

        // ── Expander state. V009: Placeable Area and Rearrange Method
        // expanders removed — only 3 remain (Tall/Wide Detection, Live
        // Sheet Preview, Overflow Handling, Gap & Margin Settings = 4
        // actually). All collapsed by default, independent multi-expand. ──
        [ObservableProperty] private bool isTallViewDetectionExpanded;
        [ObservableProperty] private bool isWideViewDetectionExpanded;
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
        private readonly ViewSizeClassifierService _classifier = new();

        public SheetAutoRearrangeViewModel(UIDocument uiDoc)
        {
            _uiDoc = uiDoc;
            _dispatcher = Dispatcher.CurrentDispatcher;

            _handler = new SheetAutoRearrangeEventHandler();
            _handler.Completed += OnHandlerCompleted;

            _externalEvent = ExternalEvent.Create(_handler);

            LoadActiveSheet();
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
            _handler.GapSettings = GapSettings; // detection needs margins
            _externalEvent.Raise();
        }

        /// <summary>
        /// Runs the SAME packing service Run() would use, purely in memory —
        /// used by the window's live preview. V009: SelectedAlgorithm switch
        /// removed — always Sheet Order.
        /// </summary>
        public System.Collections.Generic.List<PackedViewPlacement> PreviewPack()
        {
            var ticked = Views.Where(v => v.IsChecked).ToList();
            if (ticked.Count == 0 || _region == null)
                return new System.Collections.Generic.List<PackedViewPlacement>();

            return _sheetOrderPreview.Pack(ticked, _region, GapSettings,
                RowToleranceMm, RowAlignment, ColumnAlignH, ColumnAlignV,
                TallDetectionSettings, WideDetectionSettings);
        }

        public PlaceableRegion? Region => _region;

        /// <summary>
        /// Re-runs ViewSizeClassifierService against the currently TICKED
        /// items and writes the result onto each item's SizeCategory, so the
        /// grid's TALL/WIDE tags stay live. V009: always runs (Sheet Order
        /// is the only algorithm now — no more Reading-Order-resets-to-Normal
        /// branch).
        /// </summary>
        public void RefreshSizeCategories()
        {
            var ticked = Views.Where(v => v.IsChecked).ToList();
            var classification = _classifier.Classify(ticked, TallDetectionSettings, WideDetectionSettings);

            foreach (var item in Views)
            {
                item.SizeCategory = classification.Categories.TryGetValue(item, out var cat)
                    ? cat
                    : ViewSizeCategory.Normal;
            }
        }

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

        /// <summary>
        /// V009: inlined the FloorPlan/Section/Elevation/etc. mapping that
        /// used to live in ViewTypeGroupResolver.ToFilterCategory (that
        /// class is removed — it only existed for the gap-group lookup,
        /// which no longer exists — but the View Type FILTER popover still
        /// needs this mapping, so it's kept here as a small local switch).
        /// </summary>
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
            _handler.TallSettings = TallDetectionSettings;
            _handler.WideSettings = WideDetectionSettings;
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
                        RefreshSizeCategories();
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

        /// <summary>
        /// V009: Placeable Area is no longer surfaced in the UI, so this
        /// only updates the internal state needed to block Run
        /// (IsMultipleTitleBlocksWarning) and log a message — no more
        /// DetectionStatusText/TitleBlockPositionText/IsRegionLShape/
        /// IsManualFallback bindings to maintain.
        /// </summary>
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
