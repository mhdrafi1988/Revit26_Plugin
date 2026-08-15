using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.SheetAutoRearrange.V008.Core.Models;
using Revit26_Plugin.SheetAutoRearrange.V008.Core.Services;
using Revit26_Plugin.SheetAutoRearrange.V008.Infrastructure.ExternalEvents;
using Revit26_Plugin.SheetAutoRearrange.V008.Infrastructure.Helpers;
using Revit26_Plugin.Shared.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;

namespace Revit26_Plugin.SheetAutoRearrange.V008.UI.ViewModels
{
    public partial class SheetAutoRearrangeViewModel : ObservableObject
    {
        private readonly UIDocument _uiDoc;
        private readonly SheetAutoRearrangeEventHandler _handler;
        private readonly ExternalEvent _externalEvent;
        private readonly Dispatcher _dispatcher;

        // V008: no more session save-folder prompt — logs always go to a
        // fixed default location (LogExportHelper.GetDefaultLogFolder()).
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

        // ── Rearrange Method ─────────────────────────────────────────────
        [ObservableProperty] private RearrangeAlgorithm selectedAlgorithm = RearrangeAlgorithm.SheetOrder;
        [ObservableProperty] private double rowToleranceMm = 50;
        [ObservableProperty] private RowAlignment rowAlignment = RowAlignment.Center;
        [ObservableProperty] private BlockAlignmentH blockAlignH = BlockAlignmentH.Center;
        [ObservableProperty] private BlockAlignmentV blockAlignV = BlockAlignmentV.Top;

        // ── Overflow Handling ─────────────────────────────────────────────
        [ObservableProperty] private OverflowHandlingMode overflowHandlingMode = OverflowHandlingMode.PlaceWhatsPlaceable;

        // ── Gap & Margin Settings ─────────────────────────────────────────
        [ObservableProperty] private GapSettings gapSettings = new();

        // ── Tall / Wide View Detection (V007 — NEW) ─────────────────────────
        // Each is fully independent per confirmed design: separate Enable,
        // Multiplier, Tolerance, and OverflowGrouping. Only meaningful for
        // Sheet Order algorithm (confirmed scope) — Reading Order ignores these.
        [ObservableProperty] private TallWideDetectionSettings tallDetectionSettings = new();
        [ObservableProperty] private TallWideDetectionSettings wideDetectionSettings = new();

        // ── Placeable Area (V006) ────────────────────────────────────
        [ObservableProperty] private string detectionStatusText = "Not yet detected";
        [ObservableProperty] private bool isDetectionSuccess;
        [ObservableProperty] private string titleBlockPositionText = "—";
        [ObservableProperty] private bool isRegionLShape;
        [ObservableProperty] private bool isManualFallback;
        [ObservableProperty] private bool isMultipleTitleBlocksWarning;

        [ObservableProperty] private double manualMinXMm;
        [ObservableProperty] private double manualMinYMm;
        [ObservableProperty] private double manualMaxXMm;
        [ObservableProperty] private double manualMaxYMm;

        // ── Expander state. CONFIRMED: all collapsed by default,
        // independent multi-expand. V007 adds two new expander bools. ──
        [ObservableProperty] private bool isRearrangeMethodExpanded;
        [ObservableProperty] private bool isPlaceableAreaExpanded;
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

        private readonly ReadingOrderPackingService _readingOrderPreview = new();
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
            _handler.GapSettings = GapSettings; // V008 fix: detection now needs margins — was previously only set before Run
            _handler.ManualRegionOverride = IsManualFallback ? BuildManualOverrideTuple() : null;
            _externalEvent.Raise();
        }

        [RelayCommand]
        private void Redetect()
        {
            if (_activeSheet == null)
                return;

            IsBusy = true;
            Log.Add(new LogEntry(LogLevel.Info, "Re-detecting title block…"));

            _handler.Action = SheetAutoRearrangeAction.RedetectRegion;
            _handler.TargetSheet = _activeSheet;
            _handler.GapSettings = GapSettings; // V008 fix
            _handler.ManualRegionOverride = null;
            _externalEvent.Raise();
        }

        [RelayCommand]
        private void ApplyManualArea()
        {
            if (_activeSheet == null)
                return;

            IsBusy = true;
            Log.Add(new LogEntry(LogLevel.Info,
                $"Applying manual usable area: ({ManualMinXMm:0}, {ManualMinYMm:0}) → ({ManualMaxXMm:0}, {ManualMaxYMm:0}) mm."));

            _handler.Action = SheetAutoRearrangeAction.RedetectRegion;
            _handler.TargetSheet = _activeSheet;
            _handler.GapSettings = GapSettings; // V008 fix
            _handler.ManualRegionOverride = BuildManualOverrideTuple();
            _externalEvent.Raise();
        }

        private (double, double, double, double) BuildManualOverrideTuple()
            => (ManualMinXMm, ManualMinYMm, ManualMaxXMm, ManualMaxYMm);

        /// <summary>
        /// Runs the SAME packing service Run() would use, purely in memory —
        /// used by the window's live preview. V007: now threads
        /// TallDetectionSettings/WideDetectionSettings through for Sheet
        /// Order so the preview reflects shelf packing exactly as Run() will.
        /// </summary>
        public System.Collections.Generic.List<PackedViewPlacement> PreviewPack()
        {
            var ticked = Views.Where(v => v.IsChecked).ToList();
            if (ticked.Count == 0 || _region == null)
                return new System.Collections.Generic.List<PackedViewPlacement>();

            return SelectedAlgorithm switch
            {
                RearrangeAlgorithm.ReadingOrder =>
                    _readingOrderPreview.Pack(ticked, _region, GapSettings),

                RearrangeAlgorithm.SheetOrder =>
                    _sheetOrderPreview.Pack(ticked, _region, GapSettings,
                        RowToleranceMm, RowAlignment, BlockAlignH, BlockAlignV,
                        TallDetectionSettings, WideDetectionSettings),

                _ => new System.Collections.Generic.List<PackedViewPlacement>()
            };
        }

        public PlaceableRegion? Region => _region;

        /// <summary>
        /// V007 NEW: re-runs ViewSizeClassifierService against the currently
        /// TICKED items and writes the result onto each item's SizeCategory,
        /// so the grid's TALL/WIDE tags stay live as the user ticks/unticks
        /// rows or edits the Tall/Wide Detection settings. Only meaningful
        /// for Sheet Order (confirmed scope) — for Reading Order, every item
        /// is reset to Normal since tall/wide detection doesn't apply there.
        /// Unticked items are also reset to Normal (they're excluded from
        /// the mode calculation and from packing, so a stale tag would be
        /// misleading).
        /// </summary>
        public void RefreshSizeCategories()
        {
            if (SelectedAlgorithm != RearrangeAlgorithm.SheetOrder)
            {
                foreach (var item in Views)
                    item.SizeCategory = ViewSizeCategory.Normal;
                return;
            }

            var ticked = Views.Where(v => v.IsChecked).ToList();
            var classification = _classifier.Classify(ticked, TallDetectionSettings, WideDetectionSettings);

            foreach (var item in Views)
            {
                item.SizeCategory = classification.Categories.TryGetValue(item, out var cat)
                    ? cat
                    : ViewSizeCategory.Normal; // unticked items, or items excluded from classification
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

        private bool TypeIsVisible(Autodesk.Revit.DB.ViewType viewType)
        {
            string? category = ViewTypeGroupResolver.ToFilterCategory(viewType);
            return category switch
            {
                "FloorPlan" => ShowFloorPlan,
                "Section" => ShowSection,
                "Elevation" => ShowElevation,
                "ThreeD" => Show3D,
                "Legend" => ShowLegend,
                "Schedule" => ShowSchedule,
                "DraftingView" => ShowDrafting,
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
            _handler.Algorithm = SelectedAlgorithm;
            _handler.OverflowHandlingMode = OverflowHandlingMode;
            _handler.GapSettings = GapSettings;
            _handler.RowToleranceMm = RowToleranceMm;
            _handler.RowAlignment = RowAlignment;
            _handler.BlockAlignmentH = BlockAlignH;
            _handler.BlockAlignmentV = BlockAlignV;
            _handler.TallSettings = TallDetectionSettings;
            _handler.WideSettings = WideDetectionSettings;
            _handler.ManualRegionOverride = IsManualFallback ? BuildManualOverrideTuple() : null;
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

                    case SheetAutoRearrangeAction.RedetectRegion:
                        ApplyDetectionResultToState();
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

        private void ApplyDetectionResultToState()
        {
            _region = _handler.Region;
            IsMultipleTitleBlocksWarning = _handler.MultipleTitleBlocksFound;

            if (_handler.MultipleTitleBlocksFound)
            {
                DetectionStatusText = "Multiple title blocks";
                IsDetectionSuccess = false;
                TitleBlockPositionText = "Sheet has 2+ title blocks — Run disabled";
                IsRegionLShape = false;
                IsManualFallback = false;
                Log.Add(new LogEntry(LogLevel.Warning, "Sheet has multiple title blocks — Run is disabled until resolved to a single title block."));
                return;
            }

            if (_handler.NoTitleBlockFound || _region == null)
            {
                DetectionStatusText = "Not detected";
                IsDetectionSuccess = false;
                TitleBlockPositionText = _handler.NoTitleBlockFound
                    ? "No title block on sheet"
                    : "Title block geometry could not be read";
                IsRegionLShape = false;
                IsManualFallback = true;
                Log.Add(new LogEntry(LogLevel.Warning, "Title block could not be auto-detected — enter the usable area manually."));
                return;
            }

            if (_region.Mode == TitleBlockDetectionMode.Manual)
            {
                DetectionStatusText = "Manual";
                IsDetectionSuccess = true;
                TitleBlockPositionText = "User-defined rectangle";
                IsRegionLShape = false;
                IsManualFallback = true;
                Log.Add(new LogEntry(LogLevel.Success, "Manual usable area applied."));
                return;
            }

            DetectionStatusText = "Detected";
            IsDetectionSuccess = true;
            TitleBlockPositionText = "Title block found — usable area = its bounds inset by margins";
            IsRegionLShape = false; // V008: always false, no title-block-driven L-shape exists anymore
            IsManualFallback = false;
            Log.Add(new LogEntry(LogLevel.Info, "Title block detected — usable area computed from its bounding box, inset by configured margins."));
        }

        private void UpdateMetrics()
        {
            TotalViewsMetric = Views.Count;
            SelectedMetric = Views.Count(v => v.IsChecked);
        }
    }
}
