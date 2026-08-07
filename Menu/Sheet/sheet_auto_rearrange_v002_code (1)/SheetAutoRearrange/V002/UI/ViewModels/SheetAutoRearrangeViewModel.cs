using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.SheetAutoRearrange.V002.Core.Models;
using Revit26_Plugin.SheetAutoRearrange.V002.Core.Services;
using Revit26_Plugin.SheetAutoRearrange.V002.Infrastructure.ExternalEvents;
using Revit26_Plugin.SheetAutoRearrange.V002.Infrastructure.Helpers;
using Revit26_Plugin.Shared.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;

namespace Revit26_Plugin.SheetAutoRearrange.V002.UI.ViewModels
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

        // ── Metrics ───────────────────────────────────────────────────────
        [ObservableProperty] private int totalViewsMetric;
        [ObservableProperty] private int selectedMetric;
        [ObservableProperty] private int removedMetric;
        [ObservableProperty] private int placedOkMetric;
        [ObservableProperty] private int failedToFitMetric;

        private ViewSheet? _activeSheet;
        private XYZ _usableAreaMin = XYZ.Zero;
        private XYZ _usableAreaMax = new(1, 1, 0);

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
            _externalEvent.Raise();
        }

        /// <summary>
        /// Runs the SAME packing service Run() would use, purely in memory
        /// (no transaction, no element moves) — used by the window's live
        /// preview so the preview always matches what Run() will actually do.
        /// </summary>
        public System.Collections.Generic.List<PackedViewPlacement> PreviewPack()
        {
            var ticked = Views.Where(v => v.IsChecked).ToList();
            if (ticked.Count == 0)
                return new System.Collections.Generic.List<PackedViewPlacement>();

            return SelectedAlgorithm switch
            {
                RearrangeAlgorithm.ReadingOrder =>
                    _readingOrderPreview.Pack(ticked, _usableAreaMin, _usableAreaMax, GapSettings),

                RearrangeAlgorithm.SheetOrder =>
                    _sheetOrderPreview.Pack(ticked, _usableAreaMin, _usableAreaMax, GapSettings,
                        RowToleranceMm, RowAlignment, BlockAlignH, BlockAlignV),

                _ => new System.Collections.Generic.List<PackedViewPlacement>()
            };
        }

        public XYZ UsableAreaMin => _usableAreaMin;
        public XYZ UsableAreaMax => _usableAreaMax;

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

                        if (_handler.UsableAreaMinFeet != null && _handler.UsableAreaMaxFeet != null)
                        {
                            _usableAreaMin = _handler.UsableAreaMinFeet;
                            _usableAreaMax = _handler.UsableAreaMaxFeet;
                        }

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

        private void UpdateMetrics()
        {
            TotalViewsMetric = Views.Count;
            SelectedMetric = Views.Count(v => v.IsChecked);
        }
    }
}
