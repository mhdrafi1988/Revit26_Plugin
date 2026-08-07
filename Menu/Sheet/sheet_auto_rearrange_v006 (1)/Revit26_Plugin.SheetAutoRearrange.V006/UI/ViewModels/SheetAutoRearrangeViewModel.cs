using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.SheetAutoRearrange.V006.Core.Models;
using Revit26_Plugin.SheetAutoRearrange.V006.Core.Services;
using Revit26_Plugin.SheetAutoRearrange.V006.Infrastructure.ExternalEvents;
using Revit26_Plugin.SheetAutoRearrange.V006.Infrastructure.Helpers;
using Revit26_Plugin.Shared.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;

namespace Revit26_Plugin.SheetAutoRearrange.V006.UI.ViewModels
{
    public partial class SheetAutoRearrangeViewModel : ObservableObject
    {
        private readonly UIDocument _uiDoc;
        private readonly SheetAutoRearrangeEventHandler _handler;
        private readonly ExternalEvent _externalEvent;
        private readonly Dispatcher _dispatcher;

        // Cached for the session so the user is only asked once (per suite convention).
        private string? _sessionSaveFolder;

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

        // ── Placeable Area (V006 — NEW) ────────────────────────────────────
        [ObservableProperty] private string detectionStatusText = "Not yet detected";
        [ObservableProperty] private bool isDetectionSuccess;      // drives success (green) vs warning (orange) pill
        [ObservableProperty] private string titleBlockPositionText = "—";
        [ObservableProperty] private bool isRegionLShape;
        [ObservableProperty] private bool isManualFallback;        // true when Mode == Undetected or Manual — shows the manual input fields
        [ObservableProperty] private bool isMultipleTitleBlocksWarning;

        // Manual fallback input fields (mm, sheet space). Defaulted to the
        // sheet's own bbox extents once loaded — ASSUMPTION flagged in the
        // mockup review: reduces user typing versus starting blank/zero.
        [ObservableProperty] private double manualMinXMm;
        [ObservableProperty] private double manualMinYMm;
        [ObservableProperty] private double manualMaxXMm;
        [ObservableProperty] private double manualMaxYMm;

        // ── Expander state (V006 — NEW). CONFIRMED: all collapsed by default,
        // independent multi-expand (not accordion) — no code needed to
        // enforce mutual exclusion, each bool is fully independent. ──
        [ObservableProperty] private bool isRearrangeMethodExpanded;
        [ObservableProperty] private bool isPlaceableAreaExpanded;
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

        public SheetAutoRearrangeViewModel(UIDocument uiDoc)
        {
            _uiDoc = uiDoc;
            _dispatcher = Dispatcher.CurrentDispatcher;

            _handler = new SheetAutoRearrangeEventHandler();
            _handler.Completed += OnHandlerCompleted;

            // ExternalEvent.Create() called here, inside a valid API context
            // (constructor runs from the Command's Execute), never lazily.
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

            SeedManualFieldsFromSheetBBox();
            RaiseLoad();
        }

        /// <summary>
        /// Seeds the manual fallback fields with the sheet's own bounding box
        /// extents (converted to mm) so the fields aren't blank/zero if the
        /// user ends up needing manual input. ASSUMPTION flagged during
        /// mockup review — confirm if blank/zero is preferred instead.
        /// </summary>
        private void SeedManualFieldsFromSheetBBox()
        {
            if (_activeSheet == null)
                return;

            var bbox = _activeSheet.get_BoundingBox(null);
            if (bbox == null)
                return;

            const double feetToMm = 304.8;
            ManualMinXMm = bbox.Min.X * feetToMm;
            ManualMinYMm = bbox.Min.Y * feetToMm;
            ManualMaxXMm = bbox.Max.X * feetToMm;
            ManualMaxYMm = bbox.Max.Y * feetToMm;
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
            _handler.ManualRegionOverride = IsManualFallback ? BuildManualOverrideTuple() : null;
            _externalEvent.Raise();
        }

        /// <summary>
        /// V006 NEW: re-runs title block detection only (doesn't reload the
        /// views grid). Bound to the "Re-detect" button in the Placeable Area
        /// expander. Clears any manual override first so this always attempts
        /// a fresh auto-detection — per confirmed design that manual entry is
        /// an explicit, sticky user choice that only auto-detect explicitly
        /// re-tries when asked.
        /// </summary>
        [RelayCommand]
        private void Redetect()
        {
            if (_activeSheet == null)
                return;

            IsBusy = true;
            Log.Add(new LogEntry(LogLevel.Info, "Re-detecting title block…"));

            _handler.Action = SheetAutoRearrangeAction.RedetectRegion;
            _handler.TargetSheet = _activeSheet;
            _handler.ManualRegionOverride = null; // explicit re-detect always tries auto first
            _externalEvent.Raise();
        }

        /// <summary>
        /// V006 NEW: commits the manual Min/Max X/Y fields as the active
        /// region. Bound to "Apply Manual Area" in the Undetected fallback
        /// state. Distinct from Run — just updates the region used by the
        /// Live Sheet Preview and the next Run, per mockup review.
        /// </summary>
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
            _handler.ManualRegionOverride = BuildManualOverrideTuple();
            _externalEvent.Raise();
        }

        private (double, double, double, double) BuildManualOverrideTuple()
            => (ManualMinXMm, ManualMinYMm, ManualMaxXMm, ManualMaxYMm);

        /// <summary>
        /// Runs the SAME packing service Run() would use, purely in memory
        /// (no transaction, no element moves) — used by the window's live
        /// preview so the preview always matches what Run() will actually do.
        /// Returns empty if the region hasn't resolved yet (e.g. multiple
        /// title blocks found) — caller should show a blank/warning preview.
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
                        RowToleranceMm, RowAlignment, BlockAlignH, BlockAlignV),

                _ => new System.Collections.Generic.List<PackedViewPlacement>()
            };
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

        /// <summary>Views passing the current text filter + View Type popover filter.</summary>
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
                _ => true // unmapped ViewTypes (Walkthrough, Rendering, etc.) always visible
            };
        }

        [RelayCommand]
        private void Run()
        {
            if (_activeSheet == null || Views.Count == 0)
                return;

            // Blocked at the ViewModel level too (not just the engine) so the
            // user gets immediate feedback without a round-trip through the
            // external event, if the last-known detection already flagged
            // multiple title blocks.
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
            _handler.ManualRegionOverride = IsManualFallback ? BuildManualOverrideTuple() : null;
            _handler.Log = Log;

            _externalEvent.Raise();
        }

        [RelayCommand]
        private void ExportLog()
        {
            if (Log.Count == 0)
                return;

            _sessionSaveFolder ??= LogExportHelper.PromptForFolder();
            if (_sessionSaveFolder == null)
                return;

            LogExportHelper.SaveToFolder(Log, _sessionSaveFolder);
            Log.Add(new LogEntry(LogLevel.Success, $"Log exported to {_sessionSaveFolder}."));
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
            // Execute() runs on Revit's API thread — marshal back to the UI thread.
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

                            if (!result.Success && !string.IsNullOrEmpty(result.ErrorMessage))
                            {
                                // Multiple-title-blocks skip (and other hard
                                // failures) already logged by the engine/handler —
                                // nothing further to do here except skip the
                                // auto-save-log block below for a no-op run.
                            }

                            // Auto-save log on completion, per suite convention.
                            _sessionSaveFolder ??= LogExportHelper.PromptForFolder();
                            if (_sessionSaveFolder != null)
                                LogExportHelper.SaveToFolder(Log, _sessionSaveFolder);
                        }

                        // NOTE: deliberately NOT reloading Views from Revit here.
                        // ItemsToProcess passed to the engine are the SAME
                        // ViewOnSheetItem instances bound in Views (shallow copy
                        // of the list, not the items), so the engine's FitStatus
                        // updates (Fits/Overflow) are already reflected live in
                        // the grid. Reloading would re-collect fresh rows from
                        // Revit and reset every FitStatus back to Pending —
                        // wiping the exact "stays ticked, flagged Failed" result
                        // Rafi confirmed should persist until the next Run.
                        // Unticked/removed rows ARE stale (their viewports were
                        // deleted) — strip them out locally instead of a full reload.
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
        /// Maps the handler's detection result onto the Placeable Area card's
        /// bindable state after a Load / Redetect / ApplyManualArea round-trip.
        /// </summary>
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

            if (_handler.NoTitleBlockFound || _region == null || _region.Mode == TitleBlockDetectionMode.Undetected)
            {
                DetectionStatusText = "Not detected";
                IsDetectionSuccess = false;
                TitleBlockPositionText = _handler.NoTitleBlockFound ? "No title block on sheet" : "Floating / non-standard";
                IsRegionLShape = false;
                IsManualFallback = true;
                Log.Add(new LogEntry(LogLevel.Warning, "Title block position could not be auto-detected — enter the usable area manually."));
                return;
            }

            if (_region.Mode == TitleBlockDetectionMode.Manual)
            {
                DetectionStatusText = "Manual";
                IsDetectionSuccess = true;
                TitleBlockPositionText = "User-defined rectangle";
                IsRegionLShape = false;
                IsManualFallback = true; // keep fields visible/editable while Manual is active
                Log.Add(new LogEntry(LogLevel.Success, "Manual usable area applied."));
                return;
            }

            // RightEdge / BottomEdge / Corner — successful auto-detection.
            DetectionStatusText = "Auto-detected";
            IsDetectionSuccess = true;
            TitleBlockPositionText = _region.DisplayText;
            IsRegionLShape = _region.IsLShape;
            IsManualFallback = false;
            Log.Add(new LogEntry(LogLevel.Info, $"Title block classified: {_region.DisplayText}{(_region.IsLShape ? " (L-shape)" : "")}."));
        }

        private void UpdateMetrics()
        {
            TotalViewsMetric = Views.Count;
            SelectedMetric = Views.Count(v => v.IsChecked);
        }
    }
}
