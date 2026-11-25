#nullable enable
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.AutomatedSectionPlacer.V003.Models;
using Revit26_Plugin.SectionToSheet.V001.Core;
using Revit26_Plugin.Shared.Models;
using Revit26_Plugin.Shared.Services;
using Revit26_Plugin.Shared.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;

namespace Revit26_Plugin.SectionToSheet.V001.UI
{
    /// <summary>One row of the sections list.</summary>
    public partial class SectionRow : ObservableObject
    {
        /// <summary>The underlying section record.</summary>
        public SectionRecord Record { get; }
        /// <summary>Section view name.</summary>
        public string Name => Record.Info.Name;
        /// <summary>"1:50".</summary>
        public string ScaleLabel => $"1:{Record.Info.Scale}";
        /// <summary>Size on sheet in mm.</summary>
        public string SizeLabel => $"{Record.Info.WidthMm:0} x {Record.Info.HeightMm:0}";
        /// <summary>True when the section is already on a sheet.</summary>
        public bool IsPlaced => Record.CurrentSheetNumber != null;
        /// <summary>"Not placed" or "On A-301 → will move".</summary>
        public string StatusLabel => IsPlaced ? $"On {Record.CurrentSheetNumber} → will move" : "Not placed";

        /// <summary>Ticked = placed by the next run.</summary>
        [ObservableProperty] private bool _isSelected = true;

        /// <summary>Wraps a collected section.</summary>
        public SectionRow(SectionRecord record) => Record = record;
    }

    /// <summary>An existing sheet in the overflow pick list.</summary>
    public partial class SheetPickRow : ObservableObject
    {
        /// <summary>The sheet.</summary>
        public SheetChoice Sheet { get; }
        /// <summary>Label shown.</summary>
        public string Label => Sheet.Label;
        /// <summary>Ticked = used for overflow (in list order).</summary>
        [ObservableProperty] private bool _isPicked;

        /// <summary>Wraps an existing sheet.</summary>
        public SheetPickRow(SheetChoice sheet) => Sheet = sheet;
    }

    /// <summary>
    /// Section To Sheet window state: sections found in the plan, the setup, and the Place command.
    /// The window is modal, so Place runs directly in Revit's API context.
    /// </summary>
    public partial class SectionToSheetViewModel : ToolViewModelBase
    {
        private const string SettingsKey = "SectionToSheet_V001";
        private readonly UIDocument _uiDoc;
        private readonly ElementId _planId;

        /// <summary>Sections found in the plan.</summary>
        public ObservableCollection<SectionRow> Sections { get; } = new();
        /// <summary>Filtered view of <see cref="Sections"/> (hides placed ones when not included).</summary>
        public ICollectionView SectionsView { get; }
        /// <summary>Titleblock types for new sheets.</summary>
        public ObservableCollection<TitleblockOption> Titleblocks { get; } = new();
        /// <summary>"New sheet" + every existing sheet.</summary>
        public ObservableCollection<SheetChoice> StartSheets { get; } = new();
        /// <summary>Existing sheets for overflow.</summary>
        public ObservableCollection<SheetPickRow> OverflowSheets { get; } = new();
        /// <summary>Layout algorithms.</summary>
        public IReadOnlyList<RowFillStrategy> Strategies { get; } = Enum.GetValues(typeof(RowFillStrategy)).Cast<RowFillStrategy>().ToList();

        /// <summary>Plan view name.</summary>
        [ObservableProperty] private string _planName = string.Empty;
        /// <summary>Include already-placed sections (they are moved).</summary>
        [ObservableProperty] private bool _includePlaced = true;
        /// <summary>Titleblock for new sheets.</summary>
        [ObservableProperty] private TitleblockOption? _selectedTitleblock;
        /// <summary>Start sheet.</summary>
        [ObservableProperty] private SheetChoice? _selectedStartSheet;
        /// <summary>Overflow to existing sheets (false = new sheets).</summary>
        [ObservableProperty] private bool _overflowToExisting;
        /// <summary>Existing-content mode: stack below.</summary>
        [ObservableProperty] private bool _contentStack = true;
        /// <summary>Existing-content mode: skip sheet.</summary>
        [ObservableProperty] private bool _contentSkip;
        /// <summary>Existing-content mode: rearrange all.</summary>
        [ObservableProperty] private bool _contentRearrange;
        /// <summary>New sheet number prefix.</summary>
        [ObservableProperty] private string _sheetNumberPrefix = "SEC-";
        /// <summary>New sheet start number.</summary>
        [ObservableProperty] private string _sheetNumberStart = "001";
        /// <summary>New sheet name.</summary>
        [ObservableProperty] private string _sheetName = string.Empty;
        /// <summary>Layout algorithm.</summary>
        [ObservableProperty] private RowFillStrategy _strategy = RowFillStrategy.MaxFill;
        /// <summary>Margin, mm.</summary>
        [ObservableProperty] private double _marginTopMm = 10;
        /// <summary>Margin, mm.</summary>
        [ObservableProperty] private double _marginBottomMm = 10;
        /// <summary>Margin, mm.</summary>
        [ObservableProperty] private double _marginLeftMm = 10;
        /// <summary>Margin, mm.</summary>
        [ObservableProperty] private double _marginRightMm = 10;
        /// <summary>Gap, mm.</summary>
        [ObservableProperty] private double _gapHorizontalMm = 5;
        /// <summary>Gap, mm.</summary>
        [ObservableProperty] private double _gapVerticalMm = 5;
        /// <summary>Sheets used by the last run.</summary>
        [ObservableProperty] private int _sheetsUsedCount;

        /// <summary>Sections found.</summary>
        public int TotalCount => Sections.Count;
        /// <summary>Ticked sections that are not on a sheet yet.</summary>
        public int NotPlacedCount => ToRun().Count(r => !r.IsPlaced);
        /// <summary>Ticked sections that will be moved.</summary>
        public int MoveCount => ToRun().Count(r => r.IsPlaced);

        /// <summary>Loads settings and reads the plan.</summary>
        public SectionToSheetViewModel(UIDocument uiDoc, ElementId planId)
        {
            _uiDoc = uiDoc;
            _planId = planId;
            SectionsView = CollectionViewSource.GetDefaultView(Sections);
            SectionsView.Filter = o => IncludePlaced || o is SectionRow { IsPlaced: false };
            ApplySettings(SettingsService<SectionToSheetSettings>.Load(SettingsKey));
            Reload();
        }

        private IEnumerable<SectionRow> ToRun() => Sections.Where(r => r.IsSelected && (IncludePlaced || !r.IsPlaced));

        private void RaiseCounts()
        {
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(NotPlacedCount));
            OnPropertyChanged(nameof(MoveCount));
            SummaryText = $"{NotPlacedCount + MoveCount} section(s) will be placed ({MoveCount} moved).";
        }

        partial void OnIncludePlacedChanged(bool value)
        {
            SectionsView.Refresh();
            RaiseCounts();
        }

        /// <summary>Re-reads sections, titleblocks and sheets from the model.</summary>
        [RelayCommand]
        private void Reload()
        {
            var doc = _uiDoc.Document;
            if (doc.GetElement(_planId) is not View plan)
            {
                AddLog(LogLevel.Error, "The plan view this tool was started from no longer exists. Close and run it again.");
                return;
            }
            PlanName = plan.Name;
            if (string.IsNullOrWhiteSpace(SheetName)) SheetName = $"Sections - {plan.Name}";

            foreach (var r in Sections) r.PropertyChanged -= OnRowChanged;
            Sections.Clear();
            foreach (var rec in SectionCollector.CollectSections(doc, plan, m => AddLog(LogLevel.Warning, m)))
            {
                var row = new SectionRow(rec);
                row.PropertyChanged += OnRowChanged;
                Sections.Add(row);
            }

            string? tbName = SelectedTitleblock?.Name ?? _savedTitleblock;
            Titleblocks.Clear();
            foreach (var t in SectionCollector.CollectTitleblocks(doc)) Titleblocks.Add(t);
            SelectedTitleblock = Titleblocks.FirstOrDefault(t => t.Name == tbName) ?? Titleblocks.FirstOrDefault();

            var startId = SelectedStartSheet?.Id ?? ElementId.InvalidElementId;
            var picked = new HashSet<ElementId>(OverflowSheets.Where(o => o.IsPicked).Select(o => o.Sheet.Id));
            var sheets = SectionCollector.CollectSheets(doc);
            StartSheets.Clear();
            StartSheets.Add(new SheetChoice { Label = "New sheet" });
            foreach (var s in sheets) StartSheets.Add(s);
            SelectedStartSheet = StartSheets.FirstOrDefault(s => s.Id == startId) ?? StartSheets[0];
            OverflowSheets.Clear();
            foreach (var s in sheets) OverflowSheets.Add(new SheetPickRow(s) { IsPicked = picked.Contains(s.Id) });

            AddLog(LogLevel.Info, $"'{plan.Name}': {Sections.Count} section(s) found, {Sections.Count(s => s.IsPlaced)} already on a sheet.");
            if (Titleblocks.Count == 0)
                AddLog(LogLevel.Error, "No titleblock types are loaded in this project — load one to create new sheets.");
            RaiseCounts();
        }

        private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SectionRow.IsSelected)) RaiseCounts();
        }

        /// <summary>Ticks every shown section.</summary>
        [RelayCommand]
        private void SelectAll() { foreach (var r in Sections) r.IsSelected = true; }

        /// <summary>Unticks every section.</summary>
        [RelayCommand]
        private void SelectNone() { foreach (var r in Sections) r.IsSelected = false; }

        /// <summary>Places the ticked sections (one undo step), then re-reads the plan.</summary>
        [RelayCommand]
        private void Place()
        {
            var rows = ToRun().ToList();
            if (rows.Count == 0) { AddLog(LogLevel.Warning, "Tick at least one section."); return; }
            if (SelectedTitleblock == null) { AddLog(LogLevel.Error, "Pick a titleblock for new sheets."); return; }
            if (SelectedTitleblock.SheetWidthMm <= 0 || SelectedTitleblock.SheetHeightMm <= 0)
            { AddLog(LogLevel.Error, $"Titleblock '{SelectedTitleblock.Name}' has no readable size — pick another."); return; }
            if (new[] { MarginTopMm, MarginBottomMm, MarginLeftMm, MarginRightMm, GapHorizontalMm, GapVerticalMm }.Any(v => v < 0 || double.IsNaN(v)))
            { AddLog(LogLevel.Error, "Margins and gaps must be 0 or more."); return; }
            if (!int.TryParse((SheetNumberStart ?? "").Trim(), out int n) || n < 0)
            { AddLog(LogLevel.Error, "Start number must be a whole number, e.g. 001."); return; }
            var overflow = OverflowSheets.Where(o => o.IsPicked).Select(o => o.Sheet.Id).ToList();
            if (OverflowToExisting && overflow.Count == 0)
                AddLog(LogLevel.Warning, "No overflow sheets ticked — new sheets will be used.");

            var request = new PlacementRequest
            {
                NewSheetTitleblock = new TitleblockOption(SelectedTitleblock.FamilySymbolId, SelectedTitleblock.Name,
                    SelectedTitleblock.SheetWidthMm, SelectedTitleblock.SheetHeightMm),
                StartSheetId = SelectedStartSheet?.Id ?? ElementId.InvalidElementId,
                Overflow = OverflowToExisting ? OverflowMode.ExistingSheets : OverflowMode.NewSheets,
                OverflowSheetIds = overflow,
                ExistingContent = CurrentContentMode,
                SheetNumberPrefix = SheetNumberPrefix ?? string.Empty,
                SheetNumberStart = SheetNumberStart!.Trim(),
                SheetName = SheetName ?? string.Empty,
                Strategy = Strategy,
                MarginTopMm = MarginTopMm, MarginBottomMm = MarginBottomMm,
                MarginLeftMm = MarginLeftMm, MarginRightMm = MarginRightMm,
                GapHorizontalMm = GapHorizontalMm, GapVerticalMm = GapVerticalMm
            };

            SaveSettings();
            IsRunning = true;
            try
            {
                AddLog(LogLevel.Info, $"Placing {rows.Count} section(s)…");
                var result = new SectionPlacementEngine(_uiDoc.Document, request, AddLog).Run(rows.Select(r => r.Record.Info).ToList());
                SheetsUsedCount = result.SheetsUsed.Count;
                SummaryText = result.Succeeded
                    ? $"{result.Placed} placed, {result.Moved} moved, {result.Skipped} skipped on {result.SheetsUsed.Count} sheet(s)."
                    : "Failed — nothing was changed. See the log.";
            }
            finally
            {
                IsRunning = false;
            }
            Reload();
        }

        private ExistingContentMode CurrentContentMode
            => ContentSkip ? ExistingContentMode.SkipSheet
             : ContentRearrange ? ExistingContentMode.RearrangeAll
             : ExistingContentMode.StackBelow;

        // ── Settings ────────────────────────────────────────────────────────

        private string? _savedTitleblock;

        private void ApplySettings(SectionToSheetSettings s)
        {
            _savedTitleblock = s.TitleblockName;
            IncludePlaced = s.IncludePlaced;
            OverflowToExisting = s.Overflow == OverflowMode.ExistingSheets;
            ContentStack = s.ExistingContent == ExistingContentMode.StackBelow;
            ContentSkip = s.ExistingContent == ExistingContentMode.SkipSheet;
            ContentRearrange = s.ExistingContent == ExistingContentMode.RearrangeAll;
            SheetNumberPrefix = s.SheetNumberPrefix ?? "SEC-";
            SheetNumberStart = string.IsNullOrWhiteSpace(s.SheetNumberStart) ? "001" : s.SheetNumberStart;
            SheetName = s.SheetName ?? string.Empty;
            Strategy = Enum.TryParse(s.FillStrategy, out RowFillStrategy st) ? st : RowFillStrategy.MaxFill;
            MarginTopMm = s.MarginTopMm; MarginBottomMm = s.MarginBottomMm;
            MarginLeftMm = s.MarginLeftMm; MarginRightMm = s.MarginRightMm;
            GapHorizontalMm = s.GapHorizontalMm; GapVerticalMm = s.GapVerticalMm;
        }

        /// <summary>Saves the current setup (called on Place and on close).</summary>
        public void SaveSettings()
        {
            SettingsService<SectionToSheetSettings>.Save(SettingsKey, new SectionToSheetSettings
            {
                TitleblockName = SelectedTitleblock?.Name ?? _savedTitleblock ?? string.Empty,
                IncludePlaced = IncludePlaced,
                Overflow = OverflowToExisting ? OverflowMode.ExistingSheets : OverflowMode.NewSheets,
                ExistingContent = CurrentContentMode,
                SheetNumberPrefix = SheetNumberPrefix,
                SheetNumberStart = SheetNumberStart,
                // Keep the plan-based default out of the file so each plan gets its own name.
                SheetName = SheetName == $"Sections - {PlanName}" ? string.Empty : SheetName,
                FillStrategy = Strategy.ToString(),
                MarginTopMm = MarginTopMm, MarginBottomMm = MarginBottomMm,
                MarginLeftMm = MarginLeftMm, MarginRightMm = MarginRightMm,
                GapHorizontalMm = GapHorizontalMm, GapVerticalMm = GapVerticalMm
            });
        }
    }
}
