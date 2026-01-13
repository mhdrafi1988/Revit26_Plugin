using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.SARV6.Events;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using Revit26_Plugin.SARV6.ViewModels;


namespace Revit26_Plugin.SARV6.ViewModels;

public partial class SectionsListViewModel : ObservableObject
{
    public ObservableCollection<SectionItemViewModel> Sections { get; }
    public ICollectionView SectionsView { get; }

    public ObservableCollection<string> SheetFilters { get; } = new();
    public ObservableCollection<UiLogItem> Logs { get; } = new();

    [ObservableProperty] private bool isDryRun = true;
    [ObservableProperty] private string prefix = "";
    [ObservableProperty] private string postfix = "";
    [ObservableProperty] private string findText = "";
    [ObservableProperty] private string replaceText = "";
    [ObservableProperty] private bool addSerial;
    [ObservableProperty] private string serialFormat = "00";
    [ObservableProperty] private bool includeDetailNumber;
    [ObservableProperty] private string commonEditName;
    [ObservableProperty] private string selectedSheetFilter = "All";
    [ObservableProperty] private string sheetSearchText = "";
    [ObservableProperty] private DuplicateFixStrategy duplicateStrategy = DuplicateFixStrategy.NumberedBrackets;

    public SectionsListViewModel(
        IEnumerable<SectionItemViewModel> sections,
        string activeSheetNumber)
    {
        Sections = new ObservableCollection<SectionItemViewModel>(sections);

        SectionsView = CollectionViewSource.GetDefaultView(Sections);
        SectionsView.Filter = FilterBySheet;

        BuildSheetFilters(sections);

        SelectedSheetFilter =
            !string.IsNullOrWhiteSpace(activeSheetNumber) &&
            SheetFilters.Contains(activeSheetNumber)
                ? activeSheetNumber
                : "All";

        LogInfo($"Loaded {Sections.Count} sections. Default sheet: {SelectedSheetFilter}");
    }

    private void BuildSheetFilters(IEnumerable<SectionItemViewModel> sections)
    {
        SheetFilters.Clear();
        SheetFilters.Add("All");
        SheetFilters.Add("None");

        foreach (var s in sections
            .Where(x => x.IsPlaced)
            .Select(x => x.SheetNumber)
            .Distinct()
            .OrderBy(x => x))
        {
            SheetFilters.Add(s);
        }
    }

    private bool FilterBySheet(object obj)
    {
        if (obj is not SectionItemViewModel s)
            return false;

        if (!string.IsNullOrWhiteSpace(SheetSearchText) &&
            (s.SheetNumber == null ||
             !s.SheetNumber.Contains(SheetSearchText, StringComparison.OrdinalIgnoreCase)))
            return false;

        return SelectedSheetFilter switch
        {
            "All" => true,
            "None" => !s.IsPlaced,
            _ => s.SheetNumber == SelectedSheetFilter
        };
    }

    partial void OnSelectedSheetFilterChanged(string value) => SectionsView.Refresh();
    partial void OnSheetSearchTextChanged(string value) => SectionsView.Refresh();

    partial void OnCommonEditNameChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        foreach (var s in Sections)
            s.EditableName = value;

        Preview();
    }

    [RelayCommand]
    private void Preview()
    {
        int i = 1;

        foreach (var s in Sections)
        {
            var baseName = s.EditableName ?? s.OriginalName;

            if (!string.IsNullOrEmpty(FindText))
                baseName = baseName.Replace(FindText, ReplaceText ?? "");

            var serial = AddSerial ? $" {i.ToString(SerialFormat)}" : "";
            var detail = IncludeDetailNumber && !string.IsNullOrEmpty(s.DetailNumber)
                ? $" {s.DetailNumber}"
                : "";

            var raw = $"{Prefix}{baseName}{Postfix}{detail}{serial}";
            raw = System.Text.RegularExpressions.Regex.Replace(raw, @"\s+", " ").Trim();

            s.PreviewName = CultureInfo.CurrentCulture.TextInfo
                .ToTitleCase(raw.ToLower());

            i++;
        }

        DetectDuplicates();
        LogInfo("Preview updated");
    }

    private void DetectDuplicates()
    {
        foreach (var s in Sections)
            s.IsDuplicate = false;

        var groups = Sections
            .GroupBy(x => x.PreviewName, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);

        foreach (var g in groups)
            foreach (var s in g)
                s.IsDuplicate = true;

        if (groups.Any())
            LogWarning("Duplicate preview names detected (auto-fix on Commit).");
    }

    [RelayCommand]
    private void CommitRename()
    {
        if (IsDryRun)
        {
            LogWarning("Dry-run enabled. No changes committed.");
            return;
        }

        RevitEventManager.RequestRename(Sections.ToList(), this);
    }

    public void LogInfo(string m) => Logs.Add(new UiLogItem(UiLogLevel.Info, m));
    public void LogWarning(string m) => Logs.Add(new UiLogItem(UiLogLevel.Warning, m));
    public void LogError(string m) => Logs.Add(new UiLogItem(UiLogLevel.Error, m));
    public void LogSuccess(string m) => Logs.Add(new UiLogItem(UiLogLevel.Success, m));
}
