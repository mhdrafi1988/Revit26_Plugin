using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using Revit26_Plugin.CalloutCOP.V018.ExternalEvents;
using Revit26_Plugin.CalloutCOP.V018.Helpers;
using Revit26_Plugin.CalloutCOP.V018.Services;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.CalloutCOP.V018.ViewModels
{
    public partial class CalloutCOPViewModel : ObservableObject
    {
        public ObservableCollection<ViewItemViewModel> Views { get; }
        public ICollectionView ViewsCollection { get; }

        public ObservableCollection<DraftingViewItemViewModel> DraftingViews { get; }

        // Sheet-filter popup checklist. First entry is always the "ALL" row.
        public ObservableCollection<SheetFilterItemViewModel> SheetFilterItems { get; }
        public ICollectionView SheetFilterItemsView { get; }

        public ObservableCollection<LogEntry> Logs { get; } = new();

        private readonly ExternalEvent _externalEvent;
        private readonly CalloutPlacementExternalEvent _handler;
        private bool _suppressSheetFilterReactions;

        [ObservableProperty] private bool _showPlaced = true;
        // ASSUMPTION (V018, per Rafi): default flipped to unchecked.
        [ObservableProperty] private bool _showUnplaced = false;
        [ObservableProperty] private bool _showSections = true;
        [ObservableProperty] private bool _showElevations = true;

        // Sheet-filter popup state
        [ObservableProperty] private bool _isSheetFilterPopupOpen;
        [ObservableProperty] private string _sheetFilterSearchText = string.Empty;
        [ObservableProperty] private string _sheetFilterSummaryText = "ALL";

        // Bulk-fill toolbar - applies to all checked (IsSelected) rows on demand.
        // A null slot here is left untouched on target rows; only non-null slots overwrite.
        [ObservableProperty] private DraftingViewItemViewModel _bulkFillLeftView;
        [ObservableProperty] private DraftingViewItemViewModel _bulkFillCenterView;
        [ObservableProperty] private DraftingViewItemViewModel _bulkFillRightView;

        // Default callout size, mm. No fallback view anymore - Center is
        // blank unless a row (or bulk-fill) explicitly sets it.
        // ASSUMPTION: default bumped 500 -> 400 per Rafi's instruction.
        [ObservableProperty] private double _calloutSize = 400;
        // ASSUMPTION (V018, per Rafi): 400 is now a sticky default. Auto-suggest
        // no longer fires on selection change - only via the explicit
        // "Reset to suggested" command below. Previously true, which caused the
        // size to silently recompute (e.g. to 75) whenever selection changed.
        [ObservableProperty] private bool _isSizeAutoSuggested = false;

        // Execution state
        // REFACTOR (V018): NotifyCanExecuteChangedFor added on both flags below.
        // Previously IsRunning=true (in PlaceCallouts()) never re-queried
        // PlaceCalloutsCommand.CanExecute - only OnPlacementFinished did, via a
        // manual NotifyCanExecuteChanged() call. That meant the Run button could
        // stay visually enabled for the whole duration of a run instead of
        // greying out immediately on click. The source-generated attribute
        // below covers both this flag and the new HasPlacedThisSession flag,
        // so CanExecute is always re-queried the instant either changes -
        // no reliance on a manual call site elsewhere staying in sync.
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PlaceCalloutsCommand))]
        private bool _isRunning;
        [ObservableProperty] private string _progressText = string.Empty;

        // Summary card counts
        [ObservableProperty] private int _selectedCount;
        [ObservableProperty] private int _placedCount;
        [ObservableProperty] private int _runSuccessCount;
        [ObservableProperty] private int _runFailedCount;
        [ObservableProperty] private int _runSkippedCount;
        // V018: sum of filled Left/Center/Right slots (0-3 each) across selected rows.
        [ObservableProperty] private int _expectedPlacementCount;

        // V018: once a run places >=1 callout, Run disables for the rest of
        // this window's lifetime (per Rafi - reopen the tool to run again).
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PlaceCalloutsCommand))]
        private bool _hasPlacedThisSession;
        [ObservableProperty] private string _runDisabledReason = string.Empty;

        public CalloutCOPViewModel(ExternalCommandData data)
        {
            // FIX: data / data.Application / ActiveUIDocument were dereferenced
            // unchecked. If the command is invoked with no active document (or a
            // stale UIApplication reference), this threw before Views was ever
            // assigned - consistent with the "ArgumentNullException (Parameter
            // 'source')" failing immediately on window construction.
            if (data?.Application?.ActiveUIDocument?.Document is not { } doc)
                throw new InvalidOperationException(
                    "Callout COP V018: no active document. Open a document and an active view before running this tool.");

            try
            {
                Views = ViewCollectionService.CollectViews(doc) ?? new ObservableCollection<ViewItemViewModel>();
                DraftingViews = ViewCollectionService.CollectDraftingViews(doc) ?? new ObservableCollection<DraftingViewItemViewModel>();

                // ── Sheet filter checklist setup ──
                // FIX: this block must run BEFORE ViewsCollection.Filter is assigned
                // below. Setting ICollectionView.Filter triggers an immediate,
                // synchronous Refresh() that calls FilterViews(), which reads
                // SheetFilterItems.First(...). With the old ordering, SheetFilterItems
                // was still null at that point, so the very first row filtered threw
                // ArgumentNullException("source") from LINQ's First() - this was the
                // root cause of the "Value cannot be null (Parameter 'source')" crash.
                var allSheetNumbers = ViewCollectionService.CollectSheetNumbers(doc) ?? new ObservableCollection<string>();
                SheetFilterItems = new ObservableCollection<SheetFilterItemViewModel>();
                SheetFilterItems.Add(new SheetFilterItemViewModel(sheetNumber: null, isAllRow: true));
                foreach (var num in allSheetNumbers)
                    SheetFilterItems.Add(new SheetFilterItemViewModel(num));

                foreach (var item in SheetFilterItems)
                    item.PropertyChanged += OnSheetFilterItemPropertyChanged;

                SheetFilterItemsView = CollectionViewSource.GetDefaultView(SheetFilterItems)
                    ?? throw new InvalidOperationException("Callout COP V018: failed to build the sheet-filter collection view.");
                SheetFilterItemsView.Filter = FilterSheetFilterItems;

                // ── Views collection + filter (depends on SheetFilterItems above) ──
                // FIX: CollectionViewSource.GetDefaultView(object source) throws
                // ArgumentNullException("source") if handed null. Views can't be
                // null from the call above anymore, but this guard removes that
                // exact failure mode regardless of upstream cause.
                ViewsCollection = CollectionViewSource.GetDefaultView(Views)
                    ?? throw new InvalidOperationException("Callout COP V018: failed to build the views collection view.");
                ViewsCollection.Filter = FilterViews;

                foreach (var vm in Views)
                    vm.PropertyChanged += OnViewItemPropertyChanged;

                InitializeSheetFilterDefault(doc);

                // ── Bulk-fill / per-row last-used session defaults ──
                ApplySessionDefaultsToBlankRows();
                BulkFillLeftView = CalloutCOPSessionDefaults.ResolveLastUsed(CalloutCOPSessionDefaults.LastLeftViewName, DraftingViews);
                BulkFillCenterView = CalloutCOPSessionDefaults.ResolveLastUsed(CalloutCOPSessionDefaults.LastCenterViewName, DraftingViews);
                BulkFillRightView = CalloutCOPSessionDefaults.ResolveLastUsed(CalloutCOPSessionDefaults.LastRightViewName, DraftingViews);

                _handler = new CalloutPlacementExternalEvent(
                    doc,
                    Views,
                    Logs,
                    () => CalloutSize,
                    OnPlacementFinished);

                _externalEvent = ExternalEvent.Create(_handler);

                PlacedCount = Views.Count(v => v.IsPlaced);
                UpdateSelectedCount();
                UpdateExpectedPlacementCount();
                LogInfo("Callout COP V018 initialized.");
            }
            catch (Exception ex)
            {
                // FIX: previously any exception here escaped straight to Revit's
                // generic "Command Failure for External Command" dialog with no
                // context. Log it (visible if a caller inspects Logs) and rethrow
                // with the original exception preserved as InnerException, so the
                // real type/message/stack survives for diagnosis.
                Logs.Add(new LogEntry(LogLevel.Error, $"Initialization failed: {ex.GetType().Name}: {ex.Message}"));
                throw new InvalidOperationException($"Callout COP V018 failed to initialize: {ex.Message}", ex);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Sheet filter: default + selection handling
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// First open this Revit session: default to the active sheet (if the
        /// tool was launched from one), else ALL. Every subsequent open in the
        /// same session restores the last-used selection instead, ignoring
        /// the active view entirely - per Rafi's explicit precedence call.
        /// </summary>
        private void InitializeSheetFilterDefault(Document doc)
        {
            _suppressSheetFilterReactions = true;
            try
            {
                if (CalloutCOPSessionDefaults.HasSheetFilterSelection)
                {
                    RestoreLastUsedSheetSelection();
                }
                else if (doc.ActiveView is ViewSheet activeSheet)
                {
                    var match = SheetFilterItems.FirstOrDefault(
                        i => !i.IsAllRow && i.SheetNumber == activeSheet.SheetNumber);

                    if (match != null)
                    {
                        match.IsChecked = true;
                        SheetFilterItems.First(i => i.IsAllRow).IsChecked = false;
                    }
                    else
                    {
                        // Active sheet has no matching entry (shouldn't normally
                        // happen since it comes from the same document) - fall
                        // back to ALL rather than leaving nothing checked.
                        SheetFilterItems.First(i => i.IsAllRow).IsChecked = true;
                    }
                }
                else
                {
                    SheetFilterItems.First(i => i.IsAllRow).IsChecked = true;
                }
            }
            finally
            {
                _suppressSheetFilterReactions = false;
            }

            UpdateSheetFilterSummary();
            RememberCurrentSheetSelection();
        }

        private void RestoreLastUsedSheetSelection()
        {
            var allRow = SheetFilterItems.First(i => i.IsAllRow);

            if (CalloutCOPSessionDefaults.LastIsAllSelected || CalloutCOPSessionDefaults.LastSelectedSheetNumbers == null)
            {
                allRow.IsChecked = true;
                return;
            }

            allRow.IsChecked = false;
            foreach (var item in SheetFilterItems.Where(i => !i.IsAllRow))
                item.IsChecked = CalloutCOPSessionDefaults.LastSelectedSheetNumbers.Contains(item.SheetNumber);
        }

        private void RememberCurrentSheetSelection()
        {
            var allRow = SheetFilterItems.First(i => i.IsAllRow);
            CalloutCOPSessionDefaults.LastIsAllSelected = allRow.IsChecked;
            CalloutCOPSessionDefaults.LastSelectedSheetNumbers =
                new HashSet<string>(SheetFilterItems.Where(i => !i.IsAllRow && i.IsChecked).Select(i => i.SheetNumber));
            CalloutCOPSessionDefaults.HasSheetFilterSelection = true;
        }

        private void OnSheetFilterItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_suppressSheetFilterReactions) return;
            if (e.PropertyName != nameof(SheetFilterItemViewModel.IsChecked)) return;
            if (sender is not SheetFilterItemViewModel changed) return;

            // "ALL" is mutually exclusive with every specific-sheet row.
            _suppressSheetFilterReactions = true;
            try
            {
                if (changed.IsAllRow && changed.IsChecked)
                {
                    foreach (var item in SheetFilterItems.Where(i => !i.IsAllRow))
                        item.IsChecked = false;
                }
                else if (!changed.IsAllRow && changed.IsChecked)
                {
                    SheetFilterItems.First(i => i.IsAllRow).IsChecked = false;
                }
                else if (changed.IsAllRow && !changed.IsChecked)
                {
                    // Unchecking ALL with nothing else selected - leave as-is;
                    // an empty specific selection still means "no sheets match",
                    // which is a valid (if unusual) explicit state the user chose.
                }
            }
            finally
            {
                _suppressSheetFilterReactions = false;
            }

            UpdateSheetFilterSummary();
            RememberCurrentSheetSelection();
            ViewsCollection.Refresh();
        }

        private void UpdateSheetFilterSummary()
        {
            var allRow = SheetFilterItems.First(i => i.IsAllRow);
            if (allRow.IsChecked)
            {
                SheetFilterSummaryText = "ALL";
                return;
            }

            var checkedSheets = SheetFilterItems.Where(i => !i.IsAllRow && i.IsChecked).ToList();
            SheetFilterSummaryText = checkedSheets.Count switch
            {
                0 => "None selected",
                1 => checkedSheets[0].SheetNumber,
                _ => $"{checkedSheets.Count} sheets selected"
            };
        }

        private bool FilterSheetFilterItems(object obj)
        {
            if (obj is not SheetFilterItemViewModel item)
                return false;

            if (item.IsAllRow)
                return true;

            if (string.IsNullOrWhiteSpace(SheetFilterSearchText))
                return true;

            return item.SheetNumber?.Contains(SheetFilterSearchText, StringComparison.OrdinalIgnoreCase) ?? false;
        }

        partial void OnSheetFilterSearchTextChanged(string value) => SheetFilterItemsView.Refresh();

        [RelayCommand]
        private void OpenSheetFilterPopup() => IsSheetFilterPopupOpen = true;

        [RelayCommand]
        private void CloseSheetFilterPopup() => IsSheetFilterPopupOpen = false;

        [RelayCommand]
        private void SheetFilterSelectAll()
        {
            _suppressSheetFilterReactions = true;
            try
            {
                SheetFilterItems.First(i => i.IsAllRow).IsChecked = false;
                foreach (var item in SheetFilterItemsView.Cast<SheetFilterItemViewModel>().Where(i => !i.IsAllRow))
                    item.IsChecked = true;
            }
            finally
            {
                _suppressSheetFilterReactions = false;
            }

            UpdateSheetFilterSummary();
            RememberCurrentSheetSelection();
            ViewsCollection.Refresh();
        }

        [RelayCommand]
        private void SheetFilterClear()
        {
            _suppressSheetFilterReactions = true;
            try
            {
                foreach (var item in SheetFilterItems)
                    item.IsChecked = false;
            }
            finally
            {
                _suppressSheetFilterReactions = false;
            }

            UpdateSheetFilterSummary();
            RememberCurrentSheetSelection();
            ViewsCollection.Refresh();
        }

        // ─────────────────────────────────────────────────────────────
        // Session "last used" defaults for bulk-fill / per-row combos
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Pre-fills any row whose Left/Center/Right slot is still blank with
        /// the remembered last-used value for that slot. Rows that already
        /// have a real assignment (e.g. re-opening after a partial run, if the
        /// window were ever reused) are left untouched.
        /// </summary>
        private void ApplySessionDefaultsToBlankRows()
        {
            var lastLeft = CalloutCOPSessionDefaults.ResolveLastUsed(CalloutCOPSessionDefaults.LastLeftViewName, DraftingViews);
            var lastCenter = CalloutCOPSessionDefaults.ResolveLastUsed(CalloutCOPSessionDefaults.LastCenterViewName, DraftingViews);
            var lastRight = CalloutCOPSessionDefaults.ResolveLastUsed(CalloutCOPSessionDefaults.LastRightViewName, DraftingViews);

            if (lastLeft == null && lastCenter == null && lastRight == null)
                return;

            foreach (var vm in Views)
            {
                if (vm.LeftView == null && lastLeft != null) vm.LeftView = lastLeft;
                if (vm.CenterView == null && lastCenter != null) vm.CenterView = lastCenter;
                if (vm.RightView == null && lastRight != null) vm.RightView = lastRight;
            }
        }

        private void RememberBulkFillSlots()
        {
            if (BulkFillLeftView != null) CalloutCOPSessionDefaults.LastLeftViewName = BulkFillLeftView.Name;
            if (BulkFillCenterView != null) CalloutCOPSessionDefaults.LastCenterViewName = BulkFillCenterView.Name;
            if (BulkFillRightView != null) CalloutCOPSessionDefaults.LastRightViewName = BulkFillRightView.Name;
        }

        partial void OnShowPlacedChanged(bool value) => ViewsCollection.Refresh();
        partial void OnShowUnplacedChanged(bool value) => ViewsCollection.Refresh();
        partial void OnShowSectionsChanged(bool value) => ViewsCollection.Refresh();
        partial void OnShowElevationsChanged(bool value) => ViewsCollection.Refresh();
        partial void OnCalloutSizeChanged(double value) => IsSizeAutoSuggested = false;

        private bool CanPlaceCallouts() => !IsRunning && !HasPlacedThisSession;

        [RelayCommand(CanExecute = nameof(CanPlaceCallouts))]
        private void PlaceCallouts()
        {
            var count = Views.Count(v => v.IsSelected);
            if (count == 0)
            {
                LogWarning("No target views selected.");
                return;
            }

            IsRunning = true;
            ProgressText = $"Running - {count} view(s)";
            _externalEvent.Raise();
        }

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var vm in VisibleViews())
                vm.IsSelected = true;
        }

        [RelayCommand]
        private void SelectNone()
        {
            foreach (var vm in VisibleViews())
                vm.IsSelected = false;
        }

        [RelayCommand]
        private void InverseSelection()
        {
            foreach (var vm in VisibleViews())
                vm.IsSelected = !vm.IsSelected;
        }

        // Selection controls only ever touch rows currently visible under the
        // active filters - hidden/filtered-out rows are left untouched.
        private IEnumerable<ViewItemViewModel> VisibleViews()
            => ViewsCollection.Cast<ViewItemViewModel>().ToList();

        [RelayCommand]
        private void ApplyBulkFill()
        {
            var targets = Views.Where(v => v.IsSelected).ToList();
            if (!targets.Any())
            {
                LogWarning("No target views selected for bulk-fill.");
                return;
            }

            if (BulkFillLeftView == null && BulkFillCenterView == null && BulkFillRightView == null)
            {
                LogWarning("Bulk-fill: nothing set in Left/Center/Right - nothing to apply.");
                return;
            }

            foreach (var vm in targets)
            {
                if (BulkFillLeftView != null) vm.LeftView = BulkFillLeftView;
                if (BulkFillCenterView != null) vm.CenterView = BulkFillCenterView;
                if (BulkFillRightView != null) vm.RightView = BulkFillRightView;
            }

            RememberBulkFillSlots();
            LogInfo($"Bulk-fill applied to {targets.Count} view(s).");
        }

        [RelayCommand]
        private void ResetSizeToSuggested()
        {
            IsSizeAutoSuggested = true;
            UpdateSuggestedCalloutSize();
        }

        [RelayCommand]
        private void CopyAllLogs()
        {
            if (Logs.Count == 0)
                return;

            var text = string.Join(System.Environment.NewLine, Logs.Select(l => l.ToString()));
            Clipboard.SetText(text);
        }

        [RelayCommand]
        private void ClearLogs() => Logs.Clear();

        private void OnPlacementFinished(int success, int failed, int skipped)
        {
            IsRunning = false;
            ProgressText = string.Empty;
            RunSuccessCount = success;
            RunFailedCount = failed;
            RunSkippedCount = skipped;

            // Remember whatever was actually used per-row this run too, so
            // the "last used" default reflects the most recent real placement,
            // not just whatever sat in the bulk-fill boxes.
            RememberSlotsFromLastRun();

            LogInfo($"Placement complete. Placed: {success}, Failed: {failed}, Skipped: {skipped}");

            if (success > 0)
            {
                HasPlacedThisSession = true;
                RunDisabledReason = "Run disabled - callouts already placed this session. Reopen the tool to run again.";
                LogInfo("Run button disabled for the remainder of this session.");
            }

            // REFACTOR (V018): no longer needed here - IsRunning and
            // HasPlacedThisSession both carry [NotifyCanExecuteChangedFor]
            // now, so PlaceCalloutsCommand re-queries CanExecute automatically
            // as soon as either is set, above and in PlaceCallouts() itself.
        }

        private void RememberSlotsFromLastRun()
        {
            var lastLeft = Views.Where(v => v.IsSelected && v.LeftView != null).Select(v => v.LeftView.Name).LastOrDefault();
            var lastCenter = Views.Where(v => v.IsSelected && v.CenterView != null).Select(v => v.CenterView.Name).LastOrDefault();
            var lastRight = Views.Where(v => v.IsSelected && v.RightView != null).Select(v => v.RightView.Name).LastOrDefault();

            if (lastLeft != null) CalloutCOPSessionDefaults.LastLeftViewName = lastLeft;
            if (lastCenter != null) CalloutCOPSessionDefaults.LastCenterViewName = lastCenter;
            if (lastRight != null) CalloutCOPSessionDefaults.LastRightViewName = lastRight;
        }

        private void OnViewItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewItemViewModel.IsSelected))
            {
                // V018: size suggestion no longer auto-fires here (see
                // IsSizeAutoSuggested default change above) - only
                // ResetSizeToSuggested() still calls UpdateSuggestedCalloutSize().
                UpdateSelectedCount();
                UpdateExpectedPlacementCount();
            }
            else if (e.PropertyName == nameof(ViewItemViewModel.LeftView)
                  || e.PropertyName == nameof(ViewItemViewModel.CenterView)
                  || e.PropertyName == nameof(ViewItemViewModel.RightView))
            {
                UpdateExpectedPlacementCount();
            }
        }

        private void UpdateSelectedCount()
            => SelectedCount = Views.Count(v => v.IsSelected);

        // V018: sum of filled Left/Center/Right slots across selected rows,
        // capped at 3 per row (matches what a run would actually attempt).
        private void UpdateExpectedPlacementCount()
            => ExpectedPlacementCount = Views.Where(v => v.IsSelected)
                .Sum(v => (v.LeftView != null ? 1 : 0)
                        + (v.CenterView != null ? 1 : 0)
                        + (v.RightView != null ? 1 : 0));

        private void UpdateSuggestedCalloutSize()
        {
            if (!IsSizeAutoSuggested)
                return;

            var views = Views.Where(v => v.IsSelected).Select(v => v.View).ToList();
            if (!views.Any())
                return;

            CalloutSize = CalloutSizeSuggestionService.GetSuggestedSizeMm(views);
        }

        private bool FilterViews(object obj)
        {
            if (obj is not ViewItemViewModel vm)
                return false;

            // FIX: SheetFilterItems must exist before this predicate can run -
            // assigning ICollectionView.Filter triggers an immediate synchronous
            // Refresh(). If this predicate is ever wired up before SheetFilterItems
            // is built (as happened previously), show everything unfiltered rather
            // than throwing ArgumentNullException("source") from LINQ's First().
            if (SheetFilterItems == null)
                return true;

            var allRow = SheetFilterItems.First(i => i.IsAllRow);
            if (!allRow.IsChecked)
            {
                var checkedSheets = SheetFilterItems.Where(i => !i.IsAllRow && i.IsChecked).Select(i => i.SheetNumber);
                if (!vm.SheetNumberList.Intersect(checkedSheets).Any())
                    return false;
            }

            if (!ShowPlaced && vm.IsPlaced) return false;
            if (!ShowUnplaced && !vm.IsPlaced) return false;
            if (vm.ViewType == ViewType.Section && !ShowSections) return false;
            if (vm.ViewType == ViewType.Elevation && !ShowElevations) return false;

            return true;
        }

        private void LogInfo(string msg)
            => Logs.Add(new LogEntry(LogLevel.Info, msg));

        private void LogWarning(string msg)
            => Logs.Add(new LogEntry(LogLevel.Warning, msg));

        private void LogError(string msg)
            => Logs.Add(new LogEntry(LogLevel.Error, msg));
    }
}
