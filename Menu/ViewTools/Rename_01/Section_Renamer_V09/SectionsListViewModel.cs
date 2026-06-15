using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.SectionAutoRenamer.09.Events;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Data;
using System.Windows.Threading;

namespace Revit26_Plugin.SectionAutoRenamer.09.ViewModels;

public partial class SectionsListViewModel : ObservableObject
{
    // ── Collections ─────────────────────────────────────────────────────────
    public ObservableCollection<SectionItemViewModel> Sections    { get; }
    public ICollectionView                             SectionsView { get; }
    public ObservableCollection<string>                SheetFilters { get; } = new();
    public ObservableCollection<UiLogItem>             Logs         { get; } = new();

    public System.Collections.Generic.IEnumerable<DuplicateFixStrategy> DuplicateFixStrategies { get; }
        = Enum.GetValues<DuplicateFixStrategy>();

    // ── Filter bar ──────────────────────────────────────────────────────────
    [ObservableProperty] private string selectedSheetFilter = "All";
    [ObservableProperty] private string sheetSearchText     = "";

    // ── Rename panel ────────────────────────────────────────────────────────
    [ObservableProperty] private string prefix        = "";
    [ObservableProperty] private string postfix       = "";
    [ObservableProperty] private string findText      = "";
    [ObservableProperty] private string replaceText   = "";
    [ObservableProperty] private bool   addSerial;
    [ObservableProperty] private string serialFormat        = "00";
    [ObservableProperty] private bool   includeDetailNumber;
    [ObservableProperty] private bool   applyTitleCase;
    [ObservableProperty] private string commonEditName      = "";

    // ── Action bar ──────────────────────────────────────────────────────────
    [ObservableProperty] private bool   isDryRun = true;
    [ObservableProperty] private DuplicateFixStrategy duplicateStrategy = DuplicateFixStrategy.NumberedBrackets;

    // ── Derived summary ─────────────────────────────────────────────────────
    [ObservableProperty] private int selectedCount;

    // ── Live-preview debounce timer ─────────────────────────────────────────
    private readonly DispatcherTimer _previewTimer;

    // ── Constructor ─────────────────────────────────────────────────────────
    public SectionsListViewModel(
        System.Collections.Generic.IEnumerable<SectionItemViewModel> sections,
        string activeSheetNumber)
    {
        Sections = new ObservableCollection<SectionItemViewModel>(sections);

        // Subscribe to each item's IsSelected so SelectedCount stays in sync
        foreach (var s in Sections)
            s.PropertyChanged += OnItemPropertyChanged;

        SectionsView        = CollectionViewSource.GetDefaultView(Sections);
        SectionsView.Filter = FilterBySheet;

        BuildSheetFilters(sections);

        SelectedSheetFilter =
            !string.IsNullOrWhiteSpace(activeSheetNumber) &&
            SheetFilters.Contains(activeSheetNumber)
                ? activeSheetNumber
                : "All";

        // Debounce timer: fires live preview 300 ms after the last keystroke
        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _previewTimer.Tick += (_, _) => { _previewTimer.Stop(); RunPreview(); };

        UpdateSelectedCount();
        LogInfo($"Loaded {Sections.Count} sections. Active sheet: {SelectedSheetFilter}");
    }

    // ── Filter helpers ───────────────────────────────────────────────────────
    private void BuildSheetFilters(System.Collections.Generic.IEnumerable<SectionItemViewModel> sections)
    {
        SheetFilters.Clear();
        SheetFilters.Add("All");
        SheetFilters.Add("None");
        foreach (var n in sections
            .Where(x => x.IsPlaced)
            .Select(x => x.SheetNumber)
            .Distinct()
            .OrderBy(x => x))
            SheetFilters.Add(n);
    }

    private bool FilterBySheet(object obj)
    {
        if (obj is not SectionItemViewModel s) return false;

        if (!string.IsNullOrWhiteSpace(SheetSearchText) &&
            (s.SheetNumber == null ||
             !s.SheetNumber.Contains(SheetSearchText, StringComparison.OrdinalIgnoreCase)))
            return false;

        return SelectedSheetFilter switch
        {
            "All"  => true,
            "None" => !s.IsPlaced,
            _      => s.SheetNumber == SelectedSheetFilter
        };
    }

    partial void OnSelectedSheetFilterChanged(string value) => SectionsView.Refresh();
    partial void OnSheetSearchTextChanged(string value)     => SectionsView.Refresh();

    // ── Selection commands ───────────────────────────────────────────────────
    [RelayCommand]
    private void SelectAll()
    {
        foreach (var s in SectionsView.Cast<SectionItemViewModel>())
            s.IsSelected = true;
        UpdateSelectedCount();
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var s in SectionsView.Cast<SectionItemViewModel>())
            s.IsSelected = false;
        UpdateSelectedCount();
    }

    [RelayCommand]
    private void InvertSelection()
    {
        foreach (var s in SectionsView.Cast<SectionItemViewModel>())
            s.IsSelected = !s.IsSelected;
        UpdateSelectedCount();
    }

    [RelayCommand]
    private void ResetSelectedNames()
    {
        foreach (var s in Sections.Where(x => x.IsSelected))
        {
            s.EditableName = s.OriginalName;
            s.PreviewName  = s.OriginalName;
            s.IsDuplicate  = false;
        }
        LogInfo("Reset editable names for selected rows.");
    }

    // ── CommonEditName → push to all selected ───────────────────────────────
    partial void OnCommonEditNameChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        foreach (var s in Sections.Where(x => x.IsSelected))
            s.EditableName = value;
        SchedulePreview();
    }

    // ── Rename options → kick live preview ──────────────────────────────────
    partial void OnPrefixChanged(string _)              => SchedulePreview();
    partial void OnPostfixChanged(string _)             => SchedulePreview();
    partial void OnFindTextChanged(string _)            => SchedulePreview();
    partial void OnReplaceTextChanged(string _)         => SchedulePreview();
    partial void OnAddSerialChanged(bool _)             => SchedulePreview();
    partial void OnSerialFormatChanged(string _)        => SchedulePreview();
    partial void OnIncludeDetailNumberChanged(bool _)   => SchedulePreview();
    partial void OnApplyTitleCaseChanged(bool _)        => SchedulePreview();

    private void SchedulePreview()
    {
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    // ── Preview logic ────────────────────────────────────────────────────────
    [RelayCommand]
    private void Preview() => RunPreview();

    private void RunPreview()
    {
        // First pass: build composed name for every section (no serial yet)
        foreach (var s in Sections)
        {
            var baseName = s.EditableName ?? s.OriginalName;

            if (!string.IsNullOrEmpty(FindText))
                baseName = baseName.Replace(FindText, ReplaceText ?? "");

            var detail = IncludeDetailNumber && !string.IsNullOrEmpty(s.DetailNumber)
                ? $" {s.DetailNumber}" : "";

            var raw = $"{Prefix}{baseName}{Postfix}{detail}";
            raw = Regex.Replace(raw, @"\s+", " ").Trim();

            if (ApplyTitleCase)
                raw = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(raw.ToLower());

            s.PreviewName = raw;
        }

        // Second pass: serial numbering only on visible (filtered) rows in order
        if (AddSerial)
        {
            int i = 1;
            foreach (var s in SectionsView.Cast<SectionItemViewModel>())
            {
                var raw = Regex.Replace($"{s.PreviewName} {i.ToString(SerialFormat)}", @"\s+", " ").Trim();
                s.PreviewName = raw;
                i++;
            }
        }

        DetectDuplicates();
        LogInfo($"Preview updated — {SelectedCount} rows selected");
    }

    private void DetectDuplicates()
    {
        foreach (var s in Sections) s.IsDuplicate = false;

        var groups = Sections
            .GroupBy(x => x.PreviewName, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);

        foreach (var g in groups)
            foreach (var s in g)
                s.IsDuplicate = true;

        if (groups.Any())
            LogWarning("Duplicate preview names detected — auto-fix will apply on commit.");
    }

    // ── Commit ───────────────────────────────────────────────────────────────
    [RelayCommand]
    private void CommitRename()
    {
        if (IsDryRun)
        {
            LogWarning("Dry run is ON — no changes committed.");
            return;
        }

        var selected = Sections.Where(s => s.IsSelected).ToList();
        if (selected.Count == 0)
        {
            LogWarning("No rows selected — nothing to commit.");
            return;
        }

        RevitEventManager.RequestRename(selected, this);
    }

    // ── Selection count ──────────────────────────────────────────────────────
    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SectionItemViewModel.IsSelected))
            UpdateSelectedCount();
    }

    private void UpdateSelectedCount() =>
        SelectedCount = Sections.Count(s => s.IsSelected);

    // ── Log helpers ──────────────────────────────────────────────────────────
    public void LogInfo(string m)    => Logs.Add(new UiLogItem(LogLevel.Info,    m));
    public void LogWarning(string m) => Logs.Add(new UiLogItem(LogLevel.Warning, m));
    public void LogError(string m)   => Logs.Add(new UiLogItem(LogLevel.Error,   m));
    public void LogSuccess(string m) => Logs.Add(new UiLogItem(LogLevel.Success, m));
}
