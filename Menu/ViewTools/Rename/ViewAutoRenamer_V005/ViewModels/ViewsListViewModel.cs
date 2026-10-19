using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin_Revised.ViewAutoRenamer.V005.Models;
using Revit26_Plugin_Revised.ViewAutoRenamer.V005.Services;
using Revit26_Plugin_Revised.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

namespace Revit26_Plugin_Revised.ViewAutoRenamer.V005.ViewModels;

/// <summary>
/// One row per in-scope Autodesk.Revit.DB.ViewType present in the MODEL —
/// built from the cheap pre-load scan, not from loaded rows, so the picker
/// can show "how many of these exist" before anything is loaded.
/// </summary>
public partial class ViewTypeFilterRow : ObservableObject
{
    public Autodesk.Revit.DB.ViewType ViewType { get; }
    public string Label { get; }
    public int Count { get; }

    [ObservableProperty] private bool isChecked;

    public ViewTypeFilterRow(Autodesk.Revit.DB.ViewType viewType, string label, int count, bool isChecked)
    {
        ViewType = viewType;
        Label = label;
        Count = count;
        this.isChecked = isChecked;
    }
}

/// <summary>Category header row in the popover (e.g. "Plans", "Sections &amp; Callouts").</summary>
public partial class ViewTypeFilterGroup : ObservableObject
{
    public ViewTypeGroup Group { get; }
    public string Label { get; }
    public ObservableCollection<ViewTypeFilterRow> Rows { get; }
    public int TotalCount => Rows.Sum(r => r.Count);

    [ObservableProperty] private bool isExpanded = true;
    [ObservableProperty] private bool isChecked;

    private bool _settingRows;

    /// <summary>
    /// True while SetAllRows is fanning a group-level tick out to its children.
    /// The owning view-model watches this so it can collapse what would
    /// otherwise be one full ViewsGrid.Refresh() PER CHILD ROW into one.
    /// </summary>
    public bool IsSettingRows => _settingRows;

    /// <summary>Raised once after a group-level tick has finished fanning out.</summary>
    public event EventHandler? RowsBulkChanged;

    public ViewTypeFilterGroup(ViewTypeGroup group, string label, IEnumerable<ViewTypeFilterRow> rows)
    {
        Group = group;
        Label = label;
        Rows = new ObservableCollection<ViewTypeFilterRow>(rows);
        isChecked = Rows.All(r => r.IsChecked);

        foreach (var r in Rows)
            r.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ViewTypeFilterRow.IsChecked) && !_settingRows)
                    IsChecked = Rows.All(x => x.IsChecked);
            };
    }

    /// <summary>Sets every child row's IsChecked without re-triggering group-level recompute storms.</summary>
    public void SetAllRows(bool value)
    {
        _settingRows = true;
        foreach (var r in Rows) r.IsChecked = value;
        _settingRows = false;
        RowsBulkChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnIsCheckedChanged(bool value) => SetAllRows(value);
}

public partial class ViewsListViewModel : ObservableObject
{
    // ── Collections ─────────────────────────────────────────────────────────
    public ObservableCollection<ViewItemViewModel> Views     { get; }
    public ICollectionView                          ViewsGrid { get; }
    public ObservableCollection<string>             SheetFilters { get; } = new();
    public ObservableCollection<LogEntry>           Logs         { get; } = new();

    public ObservableCollection<ViewTypeFilterGroup> ViewTypeFilterGroups { get; } = new();

    public IEnumerable<DuplicateFixStrategy> DuplicateFixStrategies { get; } = Enum.GetValues<DuplicateFixStrategy>();
    public IEnumerable<StandardizeCaseOption> StandardizeCaseOptions { get; } = Enum.GetValues<StandardizeCaseOption>();

    // ── Filter bar / popover state ──────────────────────────────────────────
    [ObservableProperty] private string selectedSheetFilter = "All";
    [ObservableProperty] private string sheetSearchText     = "";
    [ObservableProperty] private string viewNameSearchText  = "";
    [ObservableProperty] private bool   isFilterPopoverOpen;
    [ObservableProperty] private bool   showPlacedOnSheet = true;
    [ObservableProperty] private bool   showNotPlaced     = true;
    [ObservableProperty] private int    activeFilterCount;
    public bool HasActiveFilters => ActiveFilterCount > 0;

    // ── Load state ──────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadViewsCommand))]
    private bool isLoading;

    [ObservableProperty] private bool hasLoadedViews;
    [ObservableProperty] private string loadSummary = "No views loaded";

    /// <summary>Grid shows its "pick types, then Load" placeholder while this is true.</summary>
    public bool IsGridEmpty => !HasLoadedViews;

    // ── Quick filter chips (one-click triage, separate from popover) ────────
    [ObservableProperty] private QuickFilterMode activeQuickFilter = QuickFilterMode.All;
    [ObservableProperty] private int totalCount;
    [ObservableProperty] private int allCount;
    [ObservableProperty] private int unplacedCount;
    [ObservableProperty] private int thisSheetCount;
    [ObservableProperty] private int duplicatesCount;
    [ObservableProperty] private int selectedChipCount;

    // ── Rename panel — Prefix/Postfix, Find/Replace, Options ─────────────────
    [ObservableProperty] private string prefix        = "";
    [ObservableProperty] private string postfix       = "";
    [ObservableProperty] private string findText      = "";
    [ObservableProperty] private string replaceText   = "";
    [ObservableProperty] private bool   addSerial;
    [ObservableProperty] private string serialFormat        = "00";
    [ObservableProperty] private bool   includeDetailNumber;
    [ObservableProperty] private string commonEditName      = "";

    // ── Standardize row ────────────────────────────────────────────────────
    [ObservableProperty] private bool                  standardizeEnabled = true;
    [ObservableProperty] private StandardizeCaseOption  standardizeCase    = StandardizeCaseOption.TitleCase;
    [ObservableProperty] private bool                   cleanWhitespacePunctuation = true;

    // ── Action bar ──────────────────────────────────────────────────────────
    [ObservableProperty] private bool   isDryRun = true;
    [ObservableProperty] private DuplicateFixStrategy duplicateStrategy = DuplicateFixStrategy.NumberedBrackets;

    // ── Derived summary ─────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TruncateLastNumberCommand))]
    [NotifyCanExecuteChangedFor(nameof(TruncateLastTextCommand))]
    private int selectedCount;

    private readonly string _activeSheetNumber;
    private readonly DispatcherTimer _previewTimer;
    private bool _suppressSelectionPreview;

    /// <summary>
    /// Set while the constructor and ApplyLoadedViews are assigning state.
    /// Without it, every settings-backed property assignment below fires its
    /// OnXxxChanged -> ViewsGrid.Refresh(), i.e. a full filter pass over
    /// every row, five or six times before the grid is even shown.
    /// </summary>
    private bool _initializing;

    /// <summary>
    /// ViewTypes whose popover checkbox is unticked. PassesAllFilters used to
    /// resolve this per row with
    /// ViewTypeFilterGroups.SelectMany(g => g.Rows).FirstOrDefault(...) —
    /// two iterator allocations and a linear scan for EVERY row on EVERY
    /// refresh. One hash lookup now; the set is rebuilt only when a checkbox
    /// actually changes.
    /// </summary>
    private readonly HashSet<Autodesk.Revit.DB.ViewType> _uncheckedTypes = new();

    /// <summary>Last duplicate count logged, so the warning isn't re-emitted on every debounced preview tick.</summary>
    private int _lastLoggedDuplicateCount = -1;

    private const int MaxLogEntries = 500;

    // ── Constructor ─────────────────────────────────────────────────────────
    /// <summary>
    /// Takes the cheap pre-load SCAN (ViewType -> how many exist in the
    /// model), not a materialised list of views. Nothing is loaded until the
    /// user ticks types and clicks Load — see LoadViews / ApplyLoadedViews.
    /// </summary>
    public ViewsListViewModel(
        IReadOnlyDictionary<Autodesk.Revit.DB.ViewType, int> typeCounts,
        string activeSheetNumber,
        long scanElapsedMs = 0)
    {
        _initializing = true;
        _activeSheetNumber = activeSheetNumber;

        Views = new ObservableCollection<ViewItemViewModel>();

        ViewsGrid        = CollectionViewSource.GetDefaultView(Views);
        ViewsGrid.Filter = PassesAllFilters;

        BuildSheetFilters();

        // Must exist before any property setter below can fire — several of the
        // settings-driven assignments (StandardizeEnabled/-Case,
        // CleanWhitespacePunctuation) trigger OnXxxChanged -> SchedulePreview(),
        // which dereferences _previewTimer. A persisted settings value that
        // differs from the field's compile-time default flips the property and
        // fires that callback while still inside this constructor, so building
        // the timer any later throws a NullReferenceException on every open
        // once the user has saved a non-default value for one of these.
        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _previewTimer.Tick += (_, _) => { _previewTimer.Stop(); RunPreview(); };

        var settings = ViewAutoRenamerSettingsService.Load();
        BuildViewTypeFilterGroups(typeCounts, settings);

        ShowPlacedOnSheet          = settings.ShowPlacedOnSheet;
        ShowNotPlaced              = settings.ShowNotPlaced;
        SheetSearchText            = settings.SheetNumberContains ?? "";
        DuplicateStrategy          = settings.DuplicateStrategy;
        IsDryRun                   = settings.IsDryRun;
        StandardizeEnabled         = settings.StandardizeEnabled;
        StandardizeCase            = settings.StandardizeCase;
        CleanWhitespacePunctuation = settings.CleanWhitespacePunctuation;

        SelectedSheetFilter = "All";

        RebuildUncheckedTypes();

        _initializing = false;

        RecalculateActiveFilterCount();
        UpdateSelectedCount();
        RecalculateQuickFilterCounts();

        int modelTotal = typeCounts.Values.Sum();
        LogInfo($"Scanned {modelTotal} views across {ViewTypeFilterGroups.Count} categories in {scanElapsedMs} ms. " +
                "Pick the types you need, then click Load." +
                (string.IsNullOrWhiteSpace(_activeSheetNumber) ? "" : $" Active sheet: {_activeSheetNumber}."));
    }

    // ── Load ────────────────────────────────────────────────────────────────
    private bool CanLoadViews()
    {
        if (IsLoading) return false;
        foreach (var g in ViewTypeFilterGroups)
            foreach (var r in g.Rows)
                if (r.IsChecked) return true;
        return false;
    }

    [RelayCommand(CanExecute = nameof(CanLoadViews))]
    private void LoadViews()
    {
        if (HasPendingEdits())
        {
            var answer = MessageBox.Show(
                "Reloading replaces everything currently in the grid, including edited names and selections.\n\nReload anyway?",
                "View Auto Renamer",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (answer != MessageBoxResult.Yes) return;
        }

        var criteria = new ViewLoadCriteria
        {
            ViewTypes           = CheckedViewTypes(),
            IncludePlaced       = ShowPlacedOnSheet,
            IncludeNotPlaced    = ShowNotPlaced,
            SheetNumberContains = SheetSearchText ?? "",
        };

        IsFilterPopoverOpen = false;
        IsLoading           = true;
        SaveSettings();

        LogInfo($"Loading {criteria.ViewTypes.Count} view type(s) from the model…");
        RevitEventManager.RequestLoad(criteria, this);
    }

    private HashSet<Autodesk.Revit.DB.ViewType> CheckedViewTypes()
    {
        var set = new HashSet<Autodesk.Revit.DB.ViewType>();
        foreach (var g in ViewTypeFilterGroups)
            foreach (var r in g.Rows)
                if (r.IsChecked) set.Add(r.ViewType);
        return set;
    }

    private bool HasPendingEdits()
    {
        foreach (var v in Views)
        {
            if (v.IsSelected) return true;
            if (!string.Equals(v.EditableName, v.OriginalName, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    /// <summary>
    /// Called on the UI thread by LoadViewsHandler once the Revit-side read
    /// is done. Replaces the grid contents wholesale (confirmed decision:
    /// re-load replaces rather than appends).
    /// </summary>
    public void ApplyLoadedViews(IReadOnlyList<ViewItemViewModel> items, long elapsedMs)
    {
        _initializing = true;

        foreach (var v in Views)
            v.PropertyChanged -= OnItemPropertyChanged;

        // DeferRefresh collapses Clear + N Adds into a single collection-view
        // pass; without it each Add re-runs the filter over the whole set.
        using (ViewsGrid.DeferRefresh())
        {
            Views.Clear();
            foreach (var item in items)
            {
                item.PropertyChanged += OnItemPropertyChanged;
                Views.Add(item);
            }
        }

        BuildSheetFilters();
        SelectedSheetFilter = "All";
        ActiveQuickFilter   = QuickFilterMode.All;
        ViewNameSearchText  = "";

        _initializing = false;

        _lastLoggedDuplicateCount = -1;
        // A load that matched nothing falls back to the empty state rather
        // than a blank grid with no explanation — the panel's call to action
        // is exactly what the user needs next.
        HasLoadedViews = items.Count > 0;

        UpdateSelectedCount();
        RecalculateQuickFilterCounts();
        RecalculateActiveFilterCount();
        ViewsGrid.Refresh();

        LoadSummary = items.Count switch
        {
            0 => "No views loaded",
            1 => "1 view loaded",
            _ => $"{items.Count} views loaded"
        };
        IsLoading = false;

        if (items.Count == 0)
            LogWarning($"No views matched those criteria ({elapsedMs} ms) — widen the types, or the placed/sheet filters, and Load again.");
        else
            LogSuccess($"Loaded {items.Count} view(s) in {elapsedMs} ms.");
    }

    /// <summary>Called on the UI thread by LoadViewsHandler when a load can't proceed.</summary>
    public void ReportLoadFailed(string message)
    {
        IsLoading = false;
        LogError(message);
    }

    private void RecalculateQuickFilterCounts()
    {
        int all = 0, unplaced = 0, thisSheet = 0, duplicates = 0, selected = 0;
        bool hasActiveSheet = !string.IsNullOrWhiteSpace(_activeSheetNumber);

        foreach (var v in Views)
        {
            all++;
            if (!v.IsPlaced) unplaced++;
            if (v.IsDuplicate) duplicates++;
            if (v.IsSelected) selected++;
            if (hasActiveSheet && v.PlacedSheetNumbers.Contains(_activeSheetNumber)) thisSheet++;
        }

        TotalCount        = all;
        AllCount          = all;
        UnplacedCount     = unplaced;
        ThisSheetCount    = thisSheet;
        DuplicatesCount   = duplicates;
        SelectedChipCount = selected;
    }

    // ── View Type popover construction ──────────────────────────────────────
    private static readonly (ViewTypeGroup Group, string Label)[] GroupOrder =
    {
        (ViewTypeGroup.SectionOrCallout, "Sections & Callouts"),
        (ViewTypeGroup.FloorPlan,        "Floor Plans"),
        (ViewTypeGroup.CeilingPlan,      "Ceiling Plans"),
        (ViewTypeGroup.StructuralPlan,   "Structural Plans"),
        (ViewTypeGroup.AreaPlan,         "Area Plans"),
        (ViewTypeGroup.Elevation,        "Elevations"),
        (ViewTypeGroup.Drafting,         "Drafting Views"),
        (ViewTypeGroup.Legend,           "Legends"),
        (ViewTypeGroup.Schedule,         "Schedules"),
    };

    /// <summary>
    /// Built from the model-wide scan, so the counts shown are "how many
    /// exist", which is what you need to decide what to load — not "how many
    /// are currently in the grid".
    /// </summary>
    private void BuildViewTypeFilterGroups(
        IReadOnlyDictionary<Autodesk.Revit.DB.ViewType, int> typeCounts,
        ViewAutoRenamerSettings settings)
    {
        ViewTypeFilterGroups.Clear();

        // First run (no persisted state) -> default to all checked, per confirmed decision.
        bool hasPersistedState = settings.CheckedViewTypeNames is { Count: > 0 };
        var checkedSet = hasPersistedState
            ? new HashSet<string>(settings.CheckedViewTypeNames)
            : null;

        foreach (var (group, label) in GroupOrder)
        {
            var rows = new List<ViewTypeFilterRow>();

            foreach (var kvp in typeCounts)
            {
                if (!ViewClassificationService.TryClassify(kvp.Key, out var g, out var display)) continue;
                if (g != group) continue;

                rows.Add(new ViewTypeFilterRow(
                    kvp.Key,
                    display,
                    kvp.Value,
                    isChecked: checkedSet == null || checkedSet.Contains(kvp.Key.ToString())));
            }

            if (rows.Count == 0) continue;
            rows.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.Ordinal));

            var groupRow = new ViewTypeFilterGroup(group, label, rows);

            foreach (var r in rows)
                r.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName != nameof(ViewTypeFilterRow.IsChecked)) return;
                    // A group-level tick fans out to every child row; let
                    // RowsBulkChanged do the single refresh instead of one per row.
                    if (groupRow.IsSettingRows) return;
                    OnTypeFilterChanged();
                };

            groupRow.RowsBulkChanged += (_, _) => OnTypeFilterChanged();

            ViewTypeFilterGroups.Add(groupRow);
        }
    }

    private void OnTypeFilterChanged()
    {
        RebuildUncheckedTypes();
        LoadViewsCommand.NotifyCanExecuteChanged();
        if (_initializing) return;
        ViewsGrid.Refresh();
        RecalculateActiveFilterCount();
    }

    private void RebuildUncheckedTypes()
    {
        _uncheckedTypes.Clear();
        foreach (var g in ViewTypeFilterGroups)
            foreach (var r in g.Rows)
                if (!r.IsChecked) _uncheckedTypes.Add(r.ViewType);
    }

    // ── Filter helpers ───────────────────────────────────────────────────────
    private void BuildSheetFilters()
    {
        SheetFilters.Clear();
        SheetFilters.Add("All");
        SheetFilters.Add("None");

        var seen = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in Views)
        {
            if (!v.IsPlaced) continue;
            foreach (var s in v.PlacedSheetNumbers)
                if (!string.IsNullOrWhiteSpace(s)) seen.Add(s);
        }
        foreach (var s in seen) SheetFilters.Add(s);
    }

    private bool PassesAllFilters(object obj)
    {
        if (obj is not ViewItemViewModel v) return false;

        // ── View name search ──
        if (!string.IsNullOrWhiteSpace(ViewNameSearchText))
        {
            bool nameMatch = v.OriginalName.Contains(ViewNameSearchText, StringComparison.OrdinalIgnoreCase)
                          || (v.EditableName ?? "").Contains(ViewNameSearchText, StringComparison.OrdinalIgnoreCase);
            if (!nameMatch) return false;
        }

        // ── View Type popover checklist (one hash lookup, see _uncheckedTypes) ──
        if (_uncheckedTypes.Count > 0 && _uncheckedTypes.Contains(v.ViewType)) return false;

        // ── Placed / Not placed ──
        if (v.IsPlaced && !ShowPlacedOnSheet) return false;
        if (!v.IsPlaced && !ShowNotPlaced) return false;

        // ── Sheet-number-contains text (popover) ──
        if (!string.IsNullOrWhiteSpace(SheetSearchText))
        {
            bool anySheetMatches = false;
            foreach (var s in v.PlacedSheetNumbers)
            {
                if (s.Contains(SheetSearchText, StringComparison.OrdinalIgnoreCase)) { anySheetMatches = true; break; }
            }
            if (!anySheetMatches) return false;
        }

        // ── Sheet ComboBox (single-select, alongside popover) ──
        bool sheetComboPasses = SelectedSheetFilter switch
        {
            "All"  => true,
            "None" => !v.IsPlaced,
            _      => v.PlacedSheetNumbers.Contains(SelectedSheetFilter)
        };
        if (!sheetComboPasses) return false;

        // ── Quick filter chip ──
        switch (ActiveQuickFilter)
        {
            case QuickFilterMode.Unplaced:
                if (v.IsPlaced) return false;
                break;
            case QuickFilterMode.ThisSheet:
                if (!v.PlacedSheetNumbers.Contains(_activeSheetNumber)) return false;
                break;
            case QuickFilterMode.DuplicatesOnly:
                if (!v.IsDuplicate) return false;
                break;
            case QuickFilterMode.SelectedOnly:
                if (!v.IsSelected) return false;
                break;
        }

        return true;
    }

    private void RefreshGridAndFilterCount()
    {
        if (_initializing) return;
        ViewsGrid.Refresh();
        RecalculateActiveFilterCount();
    }

    partial void OnSelectedSheetFilterChanged(string value)  => RefreshGridAndFilterCount();
    partial void OnSheetSearchTextChanged(string value)      => RefreshGridAndFilterCount();
    partial void OnViewNameSearchTextChanged(string value)   => RefreshGridAndFilterCount();
    partial void OnShowPlacedOnSheetChanged(bool value)      => RefreshGridAndFilterCount();
    partial void OnShowNotPlacedChanged(bool value)          => RefreshGridAndFilterCount();
    partial void OnHasLoadedViewsChanged(bool value)         => OnPropertyChanged(nameof(IsGridEmpty));

    partial void OnActiveQuickFilterChanged(QuickFilterMode value)
    {
        if (_initializing) return;
        ViewsGrid.Refresh();
    }

    private void RecalculateActiveFilterCount()
    {
        int count = 0;
        if (_uncheckedTypes.Count > 0) count++;
        if (!ShowPlacedOnSheet || !ShowNotPlaced) count++;
        if (!string.IsNullOrWhiteSpace(SheetSearchText)) count++;
        if (SelectedSheetFilter != "All") count++;
        if (!string.IsNullOrWhiteSpace(ViewNameSearchText)) count++;
        ActiveFilterCount = count;
        OnPropertyChanged(nameof(HasActiveFilters));
    }

    [RelayCommand]
    private void ToggleFilterPopover() => IsFilterPopoverOpen = !IsFilterPopoverOpen;

    [RelayCommand]
    private void ApplyFilterPopover()
    {
        IsFilterPopoverOpen = false;
        RecalculateActiveFilterCount();
        SaveSettings();
        ViewsGrid.Refresh();
    }

    [RelayCommand]
    private void ResetFilterPopover()
    {
        _initializing = true;
        foreach (var g in ViewTypeFilterGroups)
            g.SetAllRows(true);
        ShowPlacedOnSheet  = true;
        ShowNotPlaced      = true;
        SheetSearchText    = "";
        ViewNameSearchText = "";
        _initializing = false;

        RebuildUncheckedTypes();
        LoadViewsCommand.NotifyCanExecuteChanged();
        RecalculateActiveFilterCount();
        ViewsGrid.Refresh();
    }

    [RelayCommand]
    private void SetQuickFilter(string mode)
    {
        if (Enum.TryParse<QuickFilterMode>(mode, out var parsed))
            ActiveQuickFilter = parsed;
    }

    // ── Selection commands ───────────────────────────────────────────────────
    // Bulk toggles suppress the per-item RunPreview normally triggered by
    // OnItemPropertyChanged (each toggle would otherwise re-scan every row) and
    // run it once after the loop instead.
    [RelayCommand]
    private void SelectAll() => SetAllSelected(true);

    [RelayCommand]
    private void SelectNone() => SetAllSelected(false);

    [RelayCommand]
    private void InvertSelection() => ApplyBulkSelection(v => v.IsSelected = !v.IsSelected);

    /// <summary>
    /// Sets IsSelected on every row currently visible in the grid, with the
    /// per-item preview suppressed — one RunPreview at the end instead of one
    /// per row. The window's header checkbox calls this; doing the loop
    /// itself (as it used to) makes select-all O(N^2) with a regex pass per
    /// step, which freezes the UI for seconds on a few thousand rows.
    /// </summary>
    public void SetAllSelected(bool value) => ApplyBulkSelection(v => v.IsSelected = value);

    /// <summary>Shift+Click range select, same suppression as the bulk toggles.</summary>
    public void SetRangeSelected(IEnumerable<ViewItemViewModel> items, bool value)
    {
        _suppressSelectionPreview = true;
        foreach (var v in items) v.IsSelected = value;
        _suppressSelectionPreview = false;

        UpdateSelectedCount();
        SelectedChipCount = SelectedCount;
        RunPreview();
        if (ActiveQuickFilter == QuickFilterMode.SelectedOnly) ViewsGrid.Refresh();
    }

    private void ApplyBulkSelection(Action<ViewItemViewModel> apply)
    {
        _suppressSelectionPreview = true;
        foreach (var v in ViewsGrid.Cast<ViewItemViewModel>().ToList())
            apply(v);
        _suppressSelectionPreview = false;

        UpdateSelectedCount();
        SelectedChipCount = SelectedCount;
        RunPreview();
        if (ActiveQuickFilter == QuickFilterMode.SelectedOnly) ViewsGrid.Refresh();
    }

    [RelayCommand]
    private void ResetSelectedNames()
    {
        foreach (var v in Views.Where(x => x.IsSelected))
        {
            v.EditableName = v.OriginalName;
            v.PreviewName  = v.OriginalName;
        }
        LogInfo("Reset editable names for selected rows.");
        // Recompute across all rows rather than just clearing IsDuplicate on the
        // reset ones — a row that stays duplicated against one we just reset
        // needs its flag re-evaluated too, not left stale.
        DetectDuplicates();
    }

    private bool HasSelection() => SelectedCount > 0;

    // Truncate edits EditableName (the pipeline's base name), not PreviewName —
    // PreviewName is fully recomputed by RunPreview on every subsequent change
    // (Prefix/Postfix/Standardize/...), so writing to it directly here meant the
    // truncation was silently discarded the next time anything else changed.
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void TruncateLastNumber()
    {
        int changed = 0;
        foreach (var v in Views.Where(x => x.IsSelected))
        {
            var name = v.EditableName;
            if (string.IsNullOrEmpty(name) || !char.IsDigit(name[^1])) continue;
            v.EditableName = name[..^1].TrimEnd();
            changed++;
        }
        if (changed == 0) LogWarning("No selected rows end in a number.");
        RunPreview();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void TruncateLastText()
    {
        int changed = 0;
        foreach (var v in Views.Where(x => x.IsSelected))
        {
            var name = v.EditableName;
            if (string.IsNullOrEmpty(name) || !char.IsLetter(name[^1])) continue;
            v.EditableName = name[..^1].TrimEnd();
            changed++;
        }
        if (changed == 0) LogWarning("No selected rows end in a letter.");
        RunPreview();
    }

    partial void OnCommonEditNameChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        foreach (var v in Views.Where(x => x.IsSelected))
            v.EditableName = value;
        SchedulePreview();
    }

    // Parameter names must match the generator's "value" exactly — a differing
    // name (e.g. "_") makes this a distinct overload from the generated
    // partial declaration, so it silently never gets called (CS8826).
    partial void OnPrefixChanged(string value)              => SchedulePreview();
    partial void OnPostfixChanged(string value)             => SchedulePreview();
    partial void OnFindTextChanged(string value)            => SchedulePreview();
    partial void OnReplaceTextChanged(string value)         => SchedulePreview();
    partial void OnAddSerialChanged(bool value)             => SchedulePreview();
    partial void OnSerialFormatChanged(string value)        => SchedulePreview();
    partial void OnIncludeDetailNumberChanged(bool value)   => SchedulePreview();
    partial void OnStandardizeEnabledChanged(bool value)            => SchedulePreview();
    partial void OnStandardizeCaseChanged(StandardizeCaseOption value) => SchedulePreview();
    partial void OnCleanWhitespacePunctuationChanged(bool value)    => SchedulePreview();

    private void SchedulePreview()
    {
        if (_initializing) return;
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    [RelayCommand]
    private void Preview() => RunPreview(logResult: true);

    // Patterns are hoisted and compiled once. Inline Regex.Replace calls
    // re-parse the pattern on every invocation — and these run per row, per
    // debounced keystroke.
    private static readonly Regex WhitespaceRuns   = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex PunctuationToStrip = new(@"[.:#'""]", RegexOptions.Compiled);

    /// <summary>
    /// Recomputes PreviewName for every row. The batch transforms (Standardize,
    /// Find/Replace, Prefix/Postfix, Detail number, Serial) only apply to
    /// currently-SELECTED rows — they're the rename panel's bulk-edit controls,
    /// scoped the same way Commit/Truncate/Reset already are. An unselected
    /// row's Preview simply mirrors its EditableName, so a direct in-grid edit
    /// still shows up without pulling in batch transforms meant for the checked
    /// rows only.
    /// Runs silently (no log line) on every debounced keystroke via
    /// SchedulePreview — logging there would spam the log panel while the user
    /// is still typing. A log entry is only written for an explicit Preview
    /// click or right before commit, where "preview updated" is actually
    /// meaningful information.
    /// </summary>
    private void RunPreview(bool logResult = false)
    {
        foreach (var v in Views)
        {
            var baseName = v.EditableName ?? v.OriginalName;

            if (!v.IsSelected)
            {
                v.PreviewName = baseName;
                continue;
            }

            if (StandardizeEnabled)
                baseName = ApplyStandardize(baseName);

            if (!string.IsNullOrEmpty(FindText))
                baseName = baseName.Replace(FindText, ReplaceText ?? "");

            var detail = IncludeDetailNumber && !string.IsNullOrEmpty(v.DetailNumber)
                ? $" {v.DetailNumber}" : "";

            v.PreviewName = CollapseWhitespace($"{Prefix}{baseName}{Postfix}{detail}");
        }

        if (AddSerial)
        {
            // Serial applies across the FULL selected set in stable row order
            // (not just the currently-filtered view) — otherwise changing a
            // grid filter after preview would leave newly-revealed rows with
            // stale/missing serial suffixes.
            //
            // SerialFormat is free-form user text handed to int.ToString(format);
            // an invalid custom numeric format (e.g. stray letters) throws
            // FormatException, which used to crash the debounced preview tick.
            // Validate it once per pass and fall back to a plain number instead.
            bool formatValid = true;
            try { _ = 1.ToString(SerialFormat); }
            catch (FormatException) { formatValid = false; }

            int i = 1;
            foreach (var v in Views.Where(x => x.IsSelected))
            {
                var serialText = formatValid ? i.ToString(SerialFormat) : i.ToString(CultureInfo.InvariantCulture);
                v.PreviewName = CollapseWhitespace($"{v.PreviewName} {serialText}");
                i++;
            }

            if (!formatValid)
                LogWarning($"Serial format \"{SerialFormat}\" is invalid — using plain numbering instead.");
        }

        DetectDuplicates();

        if (logResult)
            LogInfo($"Preview updated — {SelectedCount} rows selected");
    }

    private static string CollapseWhitespace(string value) => WhitespaceRuns.Replace(value, " ").Trim();

    private string ApplyStandardize(string name)
    {
        if (CleanWhitespacePunctuation)
        {
            name = PunctuationToStrip.Replace(name, "");
            name = CollapseWhitespace(name);
        }

        name = StandardizeCase switch
        {
            StandardizeCaseOption.UpperCase    => name.ToUpper(CultureInfo.CurrentCulture),
            StandardizeCaseOption.TitleCase    => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLower(CultureInfo.CurrentCulture)),
            StandardizeCaseOption.SentenceCase => ToSentenceCase(name),
            _                                   => name
        };

        return name;
    }

    private static string ToSentenceCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        var lower = name.ToLower(CultureInfo.CurrentCulture);
        return char.ToUpper(lower[0], CultureInfo.CurrentCulture) + lower.Substring(1);
    }

    /// <summary>
    /// Duplicate detection is scoped PER ViewTypeGroup, matching Revit's
    /// actual name-uniqueness rule (Section+Callout share one namespace;
    /// each other view-type family is independent — a Floor Plan and a
    /// Drafting View may legally share a name).
    /// </summary>
    private void DetectDuplicates()
    {
        foreach (var v in Views) v.IsDuplicate = false;

        var groups = Views
            .GroupBy(v => v.TypeGroup)
            .SelectMany(typeGroup => typeGroup
                .GroupBy(v => v.PreviewName, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1));

        int duplicates = 0;
        foreach (var g in groups)
            foreach (var v in g) { v.IsDuplicate = true; duplicates++; }

        // Only speak up when the number actually moves — this runs on every
        // debounced keystroke, and re-logging the same warning each time both
        // spams the panel and grows the Logs collection without bound.
        if (duplicates > 0 && duplicates != _lastLoggedDuplicateCount)
            LogWarning($"{duplicates} duplicate preview name(s) within a view-type group — auto-fix will apply on commit.");
        _lastLoggedDuplicateCount = duplicates;

        DuplicatesCount = duplicates;
        if (ActiveQuickFilter == QuickFilterMode.DuplicatesOnly)
            ViewsGrid.Refresh();
    }

    // ── Commit ───────────────────────────────────────────────────────────────
    [RelayCommand]
    private void CommitRename()
    {
        // Force any pending debounced preview to run synchronously first —
        // guards against committing stale PreviewName values if the user
        // clicks Commit within the 300ms debounce window after typing.
        if (_previewTimer.IsEnabled)
        {
            _previewTimer.Stop();
            RunPreview();
        }

        if (IsDryRun)
        {
            LogWarning("Dry run is ON — no changes committed.");
            return;
        }

        var selected = Views.Where(v => v.IsSelected).ToList();
        if (selected.Count == 0)
        {
            LogWarning("No rows selected — nothing to commit.");
            return;
        }

        SaveSettings();
        RevitEventManager.RequestRename(selected, this);
    }

    // ── Settings persistence ─────────────────────────────────────────────────
    public void SaveSettings()
    {
        var settings = new ViewAutoRenamerSettings
        {
            CheckedViewTypeNames = ViewTypeFilterGroups
                .SelectMany(g => g.Rows)
                .Where(r => r.IsChecked)
                .Select(r => r.ViewType.ToString())
                .ToList(),
            ShowPlacedOnSheet          = ShowPlacedOnSheet,
            ShowNotPlaced              = ShowNotPlaced,
            SheetNumberContains        = SheetSearchText,
            DuplicateStrategy          = DuplicateStrategy,
            IsDryRun                   = IsDryRun,
            StandardizeEnabled         = StandardizeEnabled,
            StandardizeCase            = StandardizeCase,
            CleanWhitespacePunctuation = CleanWhitespacePunctuation,
        };
        ViewAutoRenamerSettingsService.Save(settings);
    }

    // ── Selection count ──────────────────────────────────────────────────────
    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewItemViewModel.IsSelected))
        {
            if (_suppressSelectionPreview) return;

            UpdateSelectedCount();
            SelectedChipCount = SelectedCount;
            if (ActiveQuickFilter == QuickFilterMode.SelectedOnly)
                ViewsGrid.Refresh();

            // Batch transforms in RunPreview are selection-scoped (see RunPreview) —
            // a row that just got checked/unchecked needs its Preview recomputed
            // immediately under the current Prefix/Postfix/Standardize/... state,
            // not left showing whatever it had before the toggle. Bulk paths
            // (SetAllSelected / SetRangeSelected / InvertSelection) suppress this
            // and call RunPreview once after the loop instead.
            RunPreview();
        }
    }

    private void UpdateSelectedCount()
    {
        int n = 0;
        foreach (var v in Views) if (v.IsSelected) n++;
        SelectedCount = n;
    }

    // ── Log helpers ──────────────────────────────────────────────────────────
    private void AddLog(LogEntry entry)
    {
        Logs.Add(entry);
        // Bounded — the log panel scrolls to the newest entry on every add, so
        // an unbounded collection is both a memory and a layout cost.
        while (Logs.Count > MaxLogEntries) Logs.RemoveAt(0);
    }

    public void LogInfo(string m)    => AddLog(new LogEntry(LogLevel.Info,    m));
    public void LogWarning(string m) => AddLog(new LogEntry(LogLevel.Warning, m));
    public void LogError(string m)   => AddLog(new LogEntry(LogLevel.Error,   m));
    public void LogSuccess(string m) => AddLog(new LogEntry(LogLevel.Success, m));
}

public enum QuickFilterMode
{
    All,
    Unplaced,
    ThisSheet,
    DuplicatesOnly,
    SelectedOnly
}
