using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.ViewAutoRenamer.V003.Models;
using Revit26_Plugin.ViewAutoRenamer.V003.Services;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Data;
using System.Windows.Threading;

namespace Revit26_Plugin.ViewAutoRenamer.V003.ViewModels;

/// <summary>
/// One row per distinct Autodesk.Revit.DB.ViewType present in the loaded
/// set — used to build the popover's "View type" checklist, grouped by
/// ViewTypeGroup category header.
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

    public ViewTypeFilterGroup(ViewTypeGroup group, string label, IEnumerable<ViewTypeFilterRow> rows)
    {
        Group = group;
        Label = label;
        Rows = new ObservableCollection<ViewTypeFilterRow>(rows);
        isChecked = Rows.All(r => r.IsChecked);

        foreach (var r in Rows)
            r.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ViewTypeFilterRow.IsChecked))
                    IsChecked = Rows.All(x => x.IsChecked);
            };
    }

    /// <summary>Sets every child row's IsChecked without re-triggering group-level recompute storms.</summary>
    public void SetAllRows(bool value)
    {
        foreach (var r in Rows) r.IsChecked = value;
    }
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
    [ObservableProperty] private bool   isFilterPopoverOpen;
    [ObservableProperty] private bool   showPlacedOnSheet = true;
    [ObservableProperty] private bool   showNotPlaced     = true;
    [ObservableProperty] private int    activeFilterCount;
    public bool HasActiveFilters => ActiveFilterCount > 0;

    // ── Quick filter chips (one-click triage, separate from popover) ────────
    [ObservableProperty] private QuickFilterMode activeQuickFilter = QuickFilterMode.All;
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

    // ── Constructor ─────────────────────────────────────────────────────────
    public ViewsListViewModel(IEnumerable<ViewItemViewModel> views, string activeSheetNumber)
    {
        _activeSheetNumber = activeSheetNumber;

        Views = new ObservableCollection<ViewItemViewModel>(views);
        foreach (var v in Views)
            v.PropertyChanged += OnItemPropertyChanged;

        ViewsGrid        = CollectionViewSource.GetDefaultView(Views);
        ViewsGrid.Filter = PassesAllFilters;

        BuildSheetFilters();

        var settings = ViewAutoRenamerSettingsService.Load();
        BuildViewTypeFilterGroups(settings);

        ShowPlacedOnSheet          = settings.ShowPlacedOnSheet;
        ShowNotPlaced              = settings.ShowNotPlaced;
        SheetSearchText            = settings.SheetNumberContains ?? "";
        DuplicateStrategy          = settings.DuplicateStrategy;
        IsDryRun                   = settings.IsDryRun;
        StandardizeEnabled         = settings.StandardizeEnabled;
        StandardizeCase            = settings.StandardizeCase;
        CleanWhitespacePunctuation = settings.CleanWhitespacePunctuation;

        SelectedSheetFilter =
            !string.IsNullOrWhiteSpace(activeSheetNumber) && SheetFilters.Contains(activeSheetNumber)
                ? activeSheetNumber
                : "All";

        RecalculateActiveFilterCount();

        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _previewTimer.Tick += (_, _) => { _previewTimer.Stop(); RunPreview(); };

        UpdateSelectedCount();
        RecalculateQuickFilterCounts();
        LogInfo($"Loaded {Views.Count} views across {ViewTypeFilterGroups.Count} categories. Active sheet: {SelectedSheetFilter}");
    }

    private void RecalculateQuickFilterCounts()
    {
        AllCount          = Views.Count;
        UnplacedCount     = Views.Count(v => !v.IsPlaced);
        ThisSheetCount    = Views.Count(v => v.PlacedSheetNumbers.Contains(_activeSheetNumber));
        DuplicatesCount   = Views.Count(v => v.IsDuplicate);
        SelectedChipCount = Views.Count(v => v.IsSelected);
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

    private void BuildViewTypeFilterGroups(ViewAutoRenamerSettings settings)
    {
        ViewTypeFilterGroups.Clear();

        // First run (no persisted state) -> default to all checked, per confirmed decision.
        bool hasPersistedState = settings.CheckedViewTypeNames is { Count: > 0 };
        var checkedSet = hasPersistedState
            ? new HashSet<string>(settings.CheckedViewTypeNames)
            : null;

        foreach (var (group, label) in GroupOrder)
        {
            var rows = Views
                .Where(v => v.TypeGroup == group)
                .GroupBy(v => v.ViewType)
                .Select(g => new ViewTypeFilterRow(
                    g.Key,
                    g.First().ViewTypeDisplay,
                    g.Count(),
                    isChecked: checkedSet == null || checkedSet.Contains(g.Key.ToString())))
                .OrderBy(r => r.Label)
                .ToList();

            if (rows.Count == 0) continue;

            var groupRow = new ViewTypeFilterGroup(group, label, rows);
            foreach (var r in rows)
                r.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName != nameof(ViewTypeFilterRow.IsChecked)) return;
                    ViewsGrid.Refresh();
                    RecalculateActiveFilterCount();
                };

            ViewTypeFilterGroups.Add(groupRow);
        }
    }

    // ── Filter helpers ───────────────────────────────────────────────────────
    private void BuildSheetFilters()
    {
        SheetFilters.Clear();
        SheetFilters.Add("All");
        SheetFilters.Add("None");
        foreach (var n in Views
            .Where(x => x.IsPlaced)
            .Select(x => x.SheetNumber)
            .Distinct()
            .OrderBy(x => x))
            SheetFilters.Add(n!);
    }

    private bool PassesAllFilters(object obj)
    {
        if (obj is not ViewItemViewModel v) return false;

        // ── View Type popover checklist ──
        var row = ViewTypeFilterGroups.SelectMany(g => g.Rows).FirstOrDefault(r => r.ViewType == v.ViewType);
        if (row != null && !row.IsChecked) return false;

        // ── Placed / Not placed ──
        if (v.IsPlaced && !ShowPlacedOnSheet) return false;
        if (!v.IsPlaced && !ShowNotPlaced) return false;

        // ── Sheet-number-contains text (popover) ──
        if (!string.IsNullOrWhiteSpace(SheetSearchText))
        {
            bool anySheetMatches = v.PlacedSheetNumbers.Any(s => s.Contains(SheetSearchText, StringComparison.OrdinalIgnoreCase));
            if (!anySheetMatches) return false;
        }

        // ── Sheet ComboBox (legacy single-select, kept alongside popover) ──
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

    partial void OnSelectedSheetFilterChanged(string value) => ViewsGrid.Refresh();
    partial void OnSheetSearchTextChanged(string value)     => ViewsGrid.Refresh();
    partial void OnShowPlacedOnSheetChanged(bool value)     { ViewsGrid.Refresh(); RecalculateActiveFilterCount(); }
    partial void OnShowNotPlacedChanged(bool value)         { ViewsGrid.Refresh(); RecalculateActiveFilterCount(); }
    partial void OnActiveQuickFilterChanged(QuickFilterMode value) => ViewsGrid.Refresh();

    private void RecalculateActiveFilterCount()
    {
        int count = 0;
        var allRows = ViewTypeFilterGroups.SelectMany(g => g.Rows).ToList();
        if (allRows.Count > 0 && allRows.Any(r => !r.IsChecked)) count++;
        if (!ShowPlacedOnSheet || !ShowNotPlaced) count++;
        if (!string.IsNullOrWhiteSpace(SheetSearchText)) count++;
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
        foreach (var g in ViewTypeFilterGroups)
            g.SetAllRows(true);
        ShowPlacedOnSheet = true;
        ShowNotPlaced     = true;
        SheetSearchText   = "";
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
    [RelayCommand]
    private void SelectAll()
    {
        foreach (var v in ViewsGrid.Cast<ViewItemViewModel>())
            v.IsSelected = true;
        UpdateSelectedCount();
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var v in ViewsGrid.Cast<ViewItemViewModel>())
            v.IsSelected = false;
        UpdateSelectedCount();
    }

    [RelayCommand]
    private void InvertSelection()
    {
        foreach (var v in ViewsGrid.Cast<ViewItemViewModel>())
            v.IsSelected = !v.IsSelected;
        UpdateSelectedCount();
    }

    [RelayCommand]
    private void ResetSelectedNames()
    {
        foreach (var v in Views.Where(x => x.IsSelected))
        {
            v.EditableName = v.OriginalName;
            v.PreviewName  = v.OriginalName;
            v.IsDuplicate  = false;
        }
        LogInfo("Reset editable names for selected rows.");
    }

    private bool HasSelection() => SelectedCount > 0;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void TruncateLastNumber()
    {
        int changed = 0;
        foreach (var v in Views.Where(x => x.IsSelected))
        {
            var name = v.PreviewName;
            if (string.IsNullOrEmpty(name) || !char.IsDigit(name[^1])) continue;
            v.PreviewName = name[..^1].TrimEnd();
            changed++;
        }
        if (changed == 0) LogWarning("No selected rows end in a number.");
        DetectDuplicates();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void TruncateLastText()
    {
        int changed = 0;
        foreach (var v in Views.Where(x => x.IsSelected))
        {
            var name = v.PreviewName;
            if (string.IsNullOrEmpty(name) || !char.IsLetter(name[^1])) continue;
            v.PreviewName = name[..^1].TrimEnd();
            changed++;
        }
        if (changed == 0) LogWarning("No selected rows end in a letter.");
        DetectDuplicates();
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
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    [RelayCommand]
    private void Preview() => RunPreview(logResult: true);

    /// <summary>
    /// Recomputes PreviewName for every row. Runs silently (no log line) on
    /// every debounced keystroke via SchedulePreview — logging there would
    /// spam the log panel while the user is still typing. A log entry is
    /// only written for an explicit Preview click or right before commit,
    /// where "preview updated" is actually meaningful information.
    /// </summary>
    private void RunPreview(bool logResult = false)
    {
        foreach (var v in Views)
        {
            var baseName = v.EditableName ?? v.OriginalName;

            if (StandardizeEnabled)
                baseName = ApplyStandardize(baseName);

            if (!string.IsNullOrEmpty(FindText))
                baseName = baseName.Replace(FindText, ReplaceText ?? "");

            var detail = IncludeDetailNumber && !string.IsNullOrEmpty(v.DetailNumber)
                ? $" {v.DetailNumber}" : "";

            var raw = $"{Prefix}{baseName}{Postfix}{detail}";
            raw = Regex.Replace(raw, @"\s+", " ").Trim();

            v.PreviewName = raw;
        }

        if (AddSerial)
        {
            // Serial applies across the FULL loaded set in a stable order
            // (grid row order), not just the currently-filtered view —
            // otherwise changing a filter after preview would leave
            // newly-revealed rows with stale/missing serial suffixes.
            int i = 1;
            foreach (var v in Views)
            {
                var raw = Regex.Replace($"{v.PreviewName} {i.ToString(SerialFormat)}", @"\s+", " ").Trim();
                v.PreviewName = raw;
                i++;
            }
        }

        DetectDuplicates();

        if (logResult)
            LogInfo($"Preview updated — {SelectedCount} rows selected");
    }

    private string ApplyStandardize(string name)
    {
        if (CleanWhitespacePunctuation)
        {
            name = Regex.Replace(name, @"[.:#'""]", "");
            name = Regex.Replace(name, @"\s+", " ").Trim();
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

        bool any = false;
        foreach (var g in groups)
        {
            any = true;
            foreach (var v in g) v.IsDuplicate = true;
        }

        if (any)
            LogWarning("Duplicate preview names detected within a view-type group — auto-fix will apply on commit.");

        DuplicatesCount = Views.Count(v => v.IsDuplicate);
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
            UpdateSelectedCount();
            SelectedChipCount = SelectedCount;
            if (ActiveQuickFilter == QuickFilterMode.SelectedOnly)
                ViewsGrid.Refresh();
        }
    }

    private void UpdateSelectedCount() =>
        SelectedCount = Views.Count(v => v.IsSelected);

    // ── Log helpers ──────────────────────────────────────────────────────────
    public void LogInfo(string m)    => Logs.Add(new LogEntry(LogLevel.Info,    m));
    public void LogWarning(string m) => Logs.Add(new LogEntry(LogLevel.Warning, m));
    public void LogError(string m)   => Logs.Add(new LogEntry(LogLevel.Error,   m));
    public void LogSuccess(string m) => Logs.Add(new LogEntry(LogLevel.Success, m));
}

public enum QuickFilterMode
{
    All,
    Unplaced,
    ThisSheet,
    DuplicatesOnly,
    SelectedOnly
}
