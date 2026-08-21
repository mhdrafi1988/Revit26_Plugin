using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.SectionAutoRenamer.V024.Models;
using Revit26_Plugin.SectionAutoRenamer.V024.Services;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Data;
using System.Windows.Threading;

namespace Revit26_Plugin.SectionAutoRenamer.V024.ViewModels;

public partial class SectionsListViewModel : ObservableObject
{
    // ── Collections ─────────────────────────────────────────────────────────
    public ObservableCollection<SectionItemViewModel> Sections    { get; }
    public ICollectionView                             SectionsView { get; }
    public ObservableCollection<string>                SheetFilters { get; } = new();
    public ObservableCollection<LogEntry>              Logs         { get; } = new();

    public System.Collections.Generic.IEnumerable<DuplicateFixStrategy> DuplicateFixStrategies { get; }
        = Enum.GetValues<DuplicateFixStrategy>();

    public System.Collections.Generic.IEnumerable<StandardizeCaseOption> StandardizeCaseOptions { get; }
        = Enum.GetValues<StandardizeCaseOption>();

    // ── Filter bar ──────────────────────────────────────────────────────────
    [ObservableProperty] private string selectedSheetFilter = "All";
    [ObservableProperty] private string sheetSearchText     = "";

    // ── Rename panel — Prefix/Postfix, Find/Replace, Options ─────────────────
    [ObservableProperty] private string prefix        = "";
    [ObservableProperty] private string postfix       = "";
    [ObservableProperty] private string separator      = "None";
    [ObservableProperty] private string findText      = "";
    [ObservableProperty] private string replaceText   = "";
    [ObservableProperty] private bool   addSerial;
    [ObservableProperty] private string serialFormat        = "00";
    [ObservableProperty] private bool   includeDetailNumber;
    [ObservableProperty] private string commonEditName      = "";

    // ── Standardize row (applied BEFORE Prefix/Postfix + Find/Replace) ───────
    [ObservableProperty] private bool                   standardizeEnabled = true;
    [ObservableProperty] private StandardizeCaseOption   standardizeCase    = StandardizeCaseOption.TitleCase;
    [ObservableProperty] private bool                    cleanWhitespacePunctuation = true;

    // ── Action bar ──────────────────────────────────────────────────────────
    [ObservableProperty] private bool   isDryRun = true;
    [ObservableProperty] private DuplicateFixStrategy duplicateStrategy = DuplicateFixStrategy.NumberedBrackets;

    // ── Derived summary ─────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TruncateLastNumberCommand))]
    [NotifyCanExecuteChangedFor(nameof(TruncateLastTextCommand))]
    private int selectedCount;

    [ObservableProperty] private int duplicateCount;
    [ObservableProperty] private int toRenameCount;

    public int TotalCount => Sections.Count;

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
        UpdateMetrics();
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
            SheetFilters.Add(n!);
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

    // ── Truncate helpers — operate on PreviewName directly (post-pipeline), ──
    // one trailing character per click. A later pipeline change (Prefix,
    // Standardize, etc.) will recompute PreviewName and override this.
    private bool HasSelection() => SelectedCount > 0;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void TruncateLastNumber()
    {
        int changed = 0;
        foreach (var s in Sections.Where(x => x.IsSelected))
        {
            var name = s.PreviewName;
            if (string.IsNullOrEmpty(name) || !char.IsDigit(name[^1])) continue;

            s.PreviewName = name[..^1].TrimEnd();
            changed++;
        }

        if (changed == 0)
            LogWarning("No selected rows end in a number.");

        DetectDuplicates();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void TruncateLastText()
    {
        int changed = 0;
        foreach (var s in Sections.Where(x => x.IsSelected))
        {
            var name = s.PreviewName;
            if (string.IsNullOrEmpty(name) || !char.IsLetter(name[^1])) continue;

            s.PreviewName = name[..^1].TrimEnd();
            changed++;
        }

        if (changed == 0)
            LogWarning("No selected rows end in a letter.");

        DetectDuplicates();
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
    partial void OnPrefixChanged(string _)                     => SchedulePreview();
    partial void OnPostfixChanged(string _)                    => SchedulePreview();
    partial void OnSeparatorChanged(string _)                  => SchedulePreview();
    partial void OnFindTextChanged(string _)                   => SchedulePreview();
    partial void OnReplaceTextChanged(string _)                => SchedulePreview();
    partial void OnAddSerialChanged(bool _)                    => SchedulePreview();
    partial void OnSerialFormatChanged(string _)                => SchedulePreview();
    partial void OnIncludeDetailNumberChanged(bool _)          => SchedulePreview();

    // ── Standardize row → kick live preview ──────────────────────────────────
    partial void OnStandardizeEnabledChanged(bool _)            => SchedulePreview();
    partial void OnStandardizeCaseChanged(StandardizeCaseOption _) => SchedulePreview();
    partial void OnCleanWhitespacePunctuationChanged(bool _)    => SchedulePreview();

    private void SchedulePreview()
    {
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    // ── Separator options for the combo box ──────────────────────────────────
    public static readonly string[] SeparatorOptions = { "None", "-", "_", ".", "Space", "|" };

    private static string ResolveSeparator(string choice) => choice switch
    {
        "None"  => "",
        "Space" => " ",
        null    => "",
        _       => choice
    };

    // ── Preview logic ────────────────────────────────────────────────────────
    [RelayCommand]
    private void Preview() => RunPreview();

    private void RunPreview()
    {
        // First pass: build composed name for every section (no serial yet)
        foreach (var s in Sections)
        {
            var baseName = s.EditableName ?? s.OriginalName;

            // Standardize runs first as base cleanup, ahead of Find/Replace and Prefix/Postfix.
            if (StandardizeEnabled)
                baseName = ApplyStandardize(baseName);

            if (!string.IsNullOrEmpty(FindText))
                baseName = baseName.Replace(FindText, ReplaceText ?? "");

            var detail = IncludeDetailNumber && !string.IsNullOrEmpty(s.DetailNumber)
                ? $" {s.DetailNumber}" : "";

            var sep = ResolveSeparator(Separator);
            var leftSep  = !string.IsNullOrEmpty(Prefix)  ? sep : "";
            var rightSep = !string.IsNullOrEmpty(Postfix) ? sep : "";

            var raw = $"{Prefix}{leftSep}{baseName}{rightSep}{Postfix}{detail}";
            raw = Regex.Replace(raw, @"\s+", " ").Trim();

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

    // ── Standardize helpers ───────────────────────────────────────────────────
    private string ApplyStandardize(string name)
    {
        if (CleanWhitespacePunctuation)
        {
            // Strip stray punctuation commonly left over from manual naming (periods,
            // colons, hashes, quotes) then collapse repeated whitespace.
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

        UpdateMetrics();
    }

    // ── Metrics card ─────────────────────────────────────────────────────────
    private void UpdateMetrics()
    {
        DuplicateCount = Sections.Count(s => s.IsDuplicate);
        ToRenameCount  = Sections.Count(s =>
            !string.Equals(s.PreviewName, s.OriginalName, StringComparison.Ordinal));
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
    public void LogInfo(string m)    => Logs.Add(new LogEntry(LogLevel.Info,    m));
    public void LogWarning(string m) => Logs.Add(new LogEntry(LogLevel.Warning, m));
    public void LogError(string m)   => Logs.Add(new LogEntry(LogLevel.Error,   m));
    public void LogSuccess(string m) => Logs.Add(new LogEntry(LogLevel.Success, m));
}
