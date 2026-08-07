using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.Shared.Models;
using Revit26_Plugin.SmartViewToSheetPlacer.V212.Models;
using Revit26_Plugin.SmartViewToSheetPlacer.V212.Services;

namespace Revit26_Plugin.SmartViewToSheetPlacer.V212.ViewModels
{
    /// <summary>
    /// Stage 2: Preview Placement — packing algorithm/gap inputs + to-scale
    /// sheet canvas. Pure calculation (no Revit-side handler call): packing
    /// runs directly here via ReadingOrderPackingService.
    /// V212: replaced the single global GapHorizontalMm/GapVerticalMm pair
    /// with one ViewGroupGapSettings per ViewType group (confirmed: gap
    /// style/values are chosen per-group, not globally). Added global
    /// ReadingDirection/RowTiebreak/YToleranceMm — these DO apply uniformly
    /// across all groups (confirmed), unlike gap style.
    /// </summary>
    public partial class SmartViewToSheetPlacerViewModel
    {
        // ---- Stage 2 state ----

        /// <summary>One entry per distinct ViewType among selected views. Built once
        /// when Stage 1 completes (NextToStage2) and persists across repacks — user's
        /// per-group Fixed/EvenGap choice and gap values survive edits, manual
        /// overrides, and Refresh, per confirmed behavior.</summary>
        public ObservableCollection<ViewGroupGapSettings> GapSettingsGroups { get; } = new();

        /// <summary>Stage 5's "Newly Created Sheets" grid, grouped by RevitViewType.
        /// Rebuilt every RunPacking() alongside SuggestedSheets — see
        /// RebuildSheetTypeGroups() below. Each group starts collapsed
        /// (SheetTypeGroup.IsExpanded defaults false) every time this rebuilds,
        /// per Rafi's confirmation (not persisted across repacks/sessions,
        /// unlike the Activity Log's expand state).</summary>
        public ObservableCollection<SheetTypeGroup> SheetTypeGroups { get; } = new();

        [ObservableProperty] private ReadingDirection _readingDirection = ReadingDirection.TopToBottom_LeftToRight;
        [ObservableProperty] private RowTiebreak _rowTiebreak = RowTiebreak.XPosition;
        [ObservableProperty] private double _yToleranceMm = 500.0;

        /// <summary>Display-label wrapper for ComboBox binding — the raw enum names
        /// (e.g. "TopToBottom_LeftToRight") aren't friendly UI text, so each option
        /// pairs the enum value with a human-readable Label. SelectedItem two-way
        /// binds against these wrapper instances, so ReadingDirection/RowTiebreak on
        /// the ViewModel are kept in sync via the setters below rather than binding
        /// directly to the enum property (ComboBox needs object identity to match
        /// SelectedItem against ItemsSource, which the enum itself provides more
        /// awkwardly across a friendly-label list). Plain class (not a C# record) —
        /// no .csproj was available to confirm LangVersion/TargetFramework support
        /// for record types, so this avoids that risk entirely.</summary>
        public class EnumOption<T>
        {
            public T Value { get; }
            public string Label { get; }
            public EnumOption(T value, string label) { Value = value; Label = label; }
        }

        public List<EnumOption<ReadingDirection>> ReadingDirectionOptions { get; } = new()
        {
            new(Models.ReadingDirection.TopToBottom_LeftToRight, "Top → Bottom, Left → Right"),
            new(Models.ReadingDirection.TopToBottom_RightToLeft, "Top → Bottom, Right → Left"),
            new(Models.ReadingDirection.BottomToTop_LeftToRight, "Bottom → Top, Left → Right"),
            new(Models.ReadingDirection.BottomToTop_RightToLeft, "Bottom → Top, Right → Left"),
        };

        public List<EnumOption<RowTiebreak>> RowTiebreakOptions { get; } = new()
        {
            new(Models.RowTiebreak.XPosition, "Position"),
            new(Models.RowTiebreak.Name, "Name"),
            new(Models.RowTiebreak.Size, "Size"),
        };

        /// <summary>ComboBox SelectedItem target for Reading Direction — wraps
        /// ReadingDirection so the ComboBox can match against ReadingDirectionOptions
        /// by object rather than needing a converter. Setter forwards to the real
        /// ReadingDirection property (which triggers RepackIfReady via its own
        /// partial OnReadingDirectionChanged).</summary>
        public EnumOption<ReadingDirection> SelectedReadingDirectionOption
        {
            get => ReadingDirectionOptions.First(o => o.Value == ReadingDirection);
            set { if (value != null) ReadingDirection = value.Value; }
        }

        /// <summary>See SelectedReadingDirectionOption — same pattern for Row Tiebreak.</summary>
        public EnumOption<RowTiebreak> SelectedRowTiebreakOption
        {
            get => RowTiebreakOptions.First(o => o.Value == RowTiebreak);
            set { if (value != null) RowTiebreak = value.Value; }
        }

        [ObservableProperty] private int _suggestedSheetCount;
        [ObservableProperty] private bool _stage2Complete;
        public string Stage2StatusLabel => Stage2Complete ? "Complete" : (Stage1Complete ? "In Progress" : "Not Started");

        /// <summary>
        /// V212: controls visibility of the pinned "Placement Results" metric
        /// row above the accordion (confirmed with Rafi: hidden/blank until
        /// the first RunPacking() completes, then stays visible showing the
        /// last computed values for the rest of the session — including
        /// while later stages are active, not just Stage 2). Set true once
        /// in RunPacking() and never reset back to false.
        /// </summary>
        [ObservableProperty] private bool _stage2ResultsAvailable;

        // ---- Stage 2/3 metric cards (V212) ----

        /// <summary>Row 1 (input): one count per ViewType group present among selected views.</summary>
        public ObservableCollection<ViewTypeInputCount> InputCountsByType { get; } = new();

        /// <summary>
        /// V212: true once InputCountsByType has at least one entry (i.e. views
        /// have been selected and packed at least once this session). Drives the
        /// "No views selected yet" placeholder card's Visibility (inverse) in the
        /// pinned Placement Metrics row — confirmed with Rafi: show a generic
        /// placeholder until the first real ViewType group appears, then it's
        /// replaced by actual per-group cards. Notified manually from RunPacking()
        /// right after InputCountsByType is rebuilt, same pattern as other
        /// computed properties in this file (e.g. Stage2StatusLabel).
        /// </summary>
        public bool HasInputCounts => InputCountsByType.Count > 0;

        [ObservableProperty] private int _totalPlacedCount;
        [ObservableProperty] private int _totalSkippedOversizedCount;
        [ObservableProperty] private int _totalFailedBadDataCount;
        [ObservableProperty] private int _totalSheetsUsedCount;

        /// <summary>Sheet currently shown in the Stage 2 Sheet Preview canvas dropdown.</summary>
        [ObservableProperty] private SheetGroup? _selectedPreviewSheet;

        /// <summary>Pixel scale factor (px per mm) used to draw the selected sheet's
        /// Canvas.Left/Top/Width/Height bindings. Recomputed whenever the selected
        /// preview sheet or titleblock changes, so the sheet always fits the fixed-size canvas.</summary>
        [ObservableProperty] private double _previewScale = 1.0;

        /// <summary>Sheet width/height in px for the Canvas + outer usable-area rect bindings.</summary>
        [ObservableProperty] private double _previewSheetWidthPx;
        [ObservableProperty] private double _previewSheetHeightPx;
        [ObservableProperty] private double _previewUsableLeftPx;
        [ObservableProperty] private double _previewUsableTopPx;
        [ObservableProperty] private double _previewUsableWidthPx;
        [ObservableProperty] private double _previewUsableHeightPx;

        private const double CanvasMaxWidthPx = 520;
        private const double CanvasMaxHeightPx = 380;

        /// <summary>
        /// Builds one ViewGroupGapSettings per distinct ViewType among currently
        /// selected views — additive only: existing entries (and whatever the user
        /// already set on them) are preserved; only missing types get a new
        /// Fixed/5mm-default entry. Called once from Stage 1's NextToStage2, before
        /// the first RunPacking() of a session.
        /// </summary>
        private void EnsureGapSettingsGroups()
        {
            var selectedTypes = AllViews.Where(v => v.IsSelected).Select(v => v.RevitViewType).Distinct().ToList();
            var existingTypes = GapSettingsGroups.Select(g => g.RevitViewType).ToHashSet();

            foreach (var vt in selectedTypes)
            {
                if (existingTypes.Contains(vt)) continue;
                var newGroup = new ViewGroupGapSettings(vt, Services.ViewTypeLabelHelper.Label(vt));
                RestorePersistedGapSettings(newGroup);
                newGroup.PropertyChanged += OnGapSettingsGroupPropertyChanged;
                GapSettingsGroups.Add(newGroup);
            }
        }

        /// <summary>
        /// Rebuilds SheetTypeGroups (Stage 5's collapsible grouped grid) from
        /// the current SuggestedSheets — full rebuild every RunPacking() call,
        /// not additive like EnsureGapSettingsGroups, since sheet suggestions
        /// themselves are fully regenerated each repack (unlike gap settings,
        /// which are a standing per-group preference worth preserving).
        /// Every group starts collapsed (SheetTypeGroup default), per Rafi's
        /// confirmation.
        /// </summary>
        private void RebuildSheetTypeGroups()
        {
            SheetTypeGroups.Clear();
            foreach (var typeGroup in SuggestedSheets.GroupBy(s => s.RevitViewType).OrderBy(g => g.Key.ToString()))
            {
                var group = new SheetTypeGroup(typeGroup.Key, Services.ViewTypeLabelHelper.Label(typeGroup.Key));
                foreach (var sheet in typeGroup)
                    group.Sheets.Add(sheet);
                SheetTypeGroups.Add(group);
            }
        }

        private void RunPacking()
        {
            EnsureGapSettingsGroups();

            SuggestedSheets.Clear();
            var selected = AllViews.Where(v => v.IsSelected).ToList();

            Logs.Add(new LogEntry(LogLevel.Info,
                $"Packing started: {selected.Count} view(s) selected | Margins T{MarginTopMm}/B{MarginBottomMm}/L{MarginLeftMm}/R{MarginRightMm}mm | Reading order: {ReadingDirection}, tiebreak {RowTiebreak}, Y-tolerance {YToleranceMm}mm."));

            var gapSettingsByType = GapSettingsGroups.ToDictionary(g => g.RevitViewType, g => g);
            var packResult = ReadingOrderPackingService.Pack(
                selected, SelectedTitleblock!, gapSettingsByType,
                ReadingDirection, RowTiebreak, YToleranceMm);

            foreach (var sheet in packResult.Sheets)
                SuggestedSheets.Add(sheet);

            AllPlacements.Clear();
            foreach (var sheet in SuggestedSheets)
                foreach (var placement in sheet.Placements)
                {
                    AllPlacements.Add(placement);
                    placement.PropertyChanged += OnPlacementPropertyChanged;
                }

            // Each placement's dropdown may only offer sheets sharing its own
            // ViewType (non-mixed rule) — wire this up now that all sheets exist.
            foreach (var placement in AllPlacements)
            {
                var sameTypeSheets = SuggestedSheets.Where(s => s.RevitViewType == placement.View.RevitViewType);
                placement.AvailableSheets.Clear();
                foreach (var s in sameTypeSheets)
                    placement.AvailableSheets.Add(s);
            }

            SuggestedSheetCount = SuggestedSheets.Count;
            RebuildSheetTypeGroups();

            // ---- V212 metric cards ----
            InputCountsByType.Clear();
            foreach (var group in selected.GroupBy(v => v.RevitViewType).OrderBy(g => g.Key.ToString()))
                InputCountsByType.Add(new ViewTypeInputCount(group.Key, Services.ViewTypeLabelHelper.Label(group.Key), group.Count()));
            OnPropertyChanged(nameof(HasInputCounts));

            TotalPlacedCount = AllPlacements.Count;
            TotalSkippedOversizedCount = packResult.SkippedOversized.Count;
            TotalFailedBadDataCount = packResult.FailedBadData.Count;
            TotalSheetsUsedCount = SuggestedSheets.Count;
            Stage2ResultsAvailable = true;

            Logs.Add(new LogEntry(LogLevel.Info,
                $"Packing complete: {TotalPlacedCount} placed | {TotalSkippedOversizedCount} skipped (oversized) | {TotalFailedBadDataCount} failed (bad data) | {TotalSheetsUsedCount} sheet(s) used."));

            foreach (var v in packResult.SkippedOversized)
                Logs.Add(new LogEntry(LogLevel.Warning, $"Packing: '{v.Name}' skipped — exceeds usable sheet width ({v.WidthMm:F0}mm > {SelectedTitleblock!.UsableWidthMm:F0}mm)."));
            foreach (var v in packResult.FailedBadData)
                Logs.Add(new LogEntry(LogLevel.Warning, $"Packing: '{v.Name}' failed — missing or zero dimensions (W{v.WidthMm:F0} x H{v.HeightMm:F0}mm)."));

            PlaceViewsCommand.NotifyCanExecuteChanged();

            // Default the Sheet Preview dropdown to the first suggested sheet.
            SelectedPreviewSheet = SuggestedSheets.FirstOrDefault();
            RecomputePreviewGeometry();
        }

        partial void OnSelectedPreviewSheetChanged(SheetGroup? value)
        {
            RecomputePreviewGeometry();
        }

        /// <summary>Editing a group's gap style/values in Stage 2 re-runs packing
        /// immediately so the Sheet Preview canvas reflects the new spacing without
        /// requiring "Next". Wired per-instance in HandleLoadViewsCompleted /
        /// EnsureGapSettingsGroups via ViewGroupGapSettings.PropertyChanged, since
        /// these are collection items rather than direct ViewModel properties.</summary>
        private void OnGapSettingsGroupPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ViewGroupGapSettings.GapStyle) &&
                e.PropertyName != nameof(ViewGroupGapSettings.HorizontalGapMm) &&
                e.PropertyName != nameof(ViewGroupGapSettings.VerticalGapMm))
                return;
            RepackIfReady();
        }

        partial void OnReadingDirectionChanged(ReadingDirection value)
        {
            OnPropertyChanged(nameof(SelectedReadingDirectionOption));
            RepackIfReady();
        }

        partial void OnRowTiebreakChanged(RowTiebreak value)
        {
            OnPropertyChanged(nameof(SelectedRowTiebreakOption));
            RepackIfReady();
        }

        partial void OnYToleranceMmChanged(double value) => RepackIfReady();

        private void RepackIfReady()
        {
            if (SelectedTitleblock == null || AllViews.Count == 0) return;
            if (!Stage1Complete) return; // no packing to redo yet
            RunPacking();
        }

        private void RecomputePreviewGeometry()
        {
            if (SelectedTitleblock == null || SelectedTitleblock.SheetWidthMm <= 0 || SelectedTitleblock.SheetHeightMm <= 0)
            {
                PreviewScale = 1.0;
                PreviewSheetWidthPx = 0;
                PreviewSheetHeightPx = 0;
                PreviewUsableLeftPx = 0;
                PreviewUsableTopPx = 0;
                PreviewUsableWidthPx = 0;
                PreviewUsableHeightPx = 0;
                return;
            }

            double scaleX = CanvasMaxWidthPx / SelectedTitleblock.SheetWidthMm;
            double scaleY = CanvasMaxHeightPx / SelectedTitleblock.SheetHeightMm;
            PreviewScale = Math.Min(scaleX, scaleY);

            PreviewSheetWidthPx = SelectedTitleblock.SheetWidthMm * PreviewScale;
            PreviewSheetHeightPx = SelectedTitleblock.SheetHeightMm * PreviewScale;
            PreviewUsableLeftPx = SelectedTitleblock.MarginLeftMm * PreviewScale;
            PreviewUsableTopPx = SelectedTitleblock.MarginTopMm * PreviewScale;
            PreviewUsableWidthPx = SelectedTitleblock.UsableWidthMm * PreviewScale;
            PreviewUsableHeightPx = SelectedTitleblock.UsableHeightMm * PreviewScale;

            if (SelectedPreviewSheet == null) return;

            foreach (var placement in SelectedPreviewSheet.Placements)
            {
                placement.PreviewLeftPx = PreviewUsableLeftPx + (placement.OffsetXMm * PreviewScale);
                placement.PreviewTopPx = PreviewUsableTopPx + (placement.OffsetYMm * PreviewScale);
                placement.PreviewWidthPx = placement.View.WidthMm * PreviewScale;
                placement.PreviewHeightPx = placement.View.HeightMm * PreviewScale;
            }
        }

        /// <summary>
        /// Fires whenever any placement's AssignedSheet changes — including
        /// via the Stage 2 DataGrid's "Suggested Sheet #" dropdown binding.
        /// Moves the placement between SheetGroup.Placements collections and
        /// re-validates fit (GeneratedName on both sheets updates
        /// automatically via SheetGroup's own CollectionChanged wiring).
        /// V212: looks up the target sheet's ViewType in GapSettingsGroups
        /// to pass the correct per-group gap settings into RevalidateFit.
        /// </summary>
        private void OnPlacementPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ViewPlacement.AssignedSheet)) return;
            if (sender is not ViewPlacement placement) return;
            if (SelectedTitleblock == null) return;

            foreach (var sheet in SuggestedSheets.Where(s => s != placement.AssignedSheet))
                sheet.Placements.Remove(placement);

            if (!placement.AssignedSheet.Placements.Contains(placement))
                placement.AssignedSheet.Placements.Add(placement);

            var gapSettings = GapSettingsGroups.FirstOrDefault(g => g.RevitViewType == placement.AssignedSheet.RevitViewType);
            if (gapSettings != null)
                ReadingOrderPackingService.RevalidateFit(placement.AssignedSheet, SelectedTitleblock, gapSettings);

            RecomputePreviewGeometry();
        }

        [RelayCommand]
        private void SetGapStyleFixed(ViewGroupGapSettings group)
        {
            if (group != null) group.GapStyle = Models.GapStyle.Fixed;
        }

        [RelayCommand]
        private void SetGapStyleEvenGap(ViewGroupGapSettings group)
        {
            if (group != null) group.GapStyle = Models.GapStyle.EvenGap;
        }

        [RelayCommand]
        private void BackToStage1()
        {
            Stage2Expanded = false;
            Stage1Expanded = true;
        }

        /// <summary>Advances from Stage 2 (Preview Placement) into Stage 3
        /// (Suggested Placement — badges + editable grid). Re-runs packing
        /// first so any per-group gap-style edits made in Stage 2 are
        /// reflected before the grid is shown.</summary>
        [RelayCommand]
        private void NextToStage3()
        {
            RunPacking();
            Stage2Complete = true;
            Stage2Expanded = false;
            Stage3Expanded = true;
        }

        partial void OnStage2CompleteChanged(bool value)
        {
            OnPropertyChanged(nameof(Stage2StatusLabel));
            OnPropertyChanged(nameof(Stage3StatusLabel));
        }
    }
}
