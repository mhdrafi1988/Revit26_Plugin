using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.Shared.Models;
using Revit26_Plugin.SmartViewToSheetPlacer.V204.Models;
using Revit26_Plugin.SmartViewToSheetPlacer.V204.Services;

namespace Revit26_Plugin.SmartViewToSheetPlacer.V204.ViewModels
{
    /// <summary>
    /// Stage 2: Preview Placement — packing algorithm/gap inputs + to-scale
    /// sheet canvas. Pure calculation (no Revit-side handler call): packing
    /// runs directly here via GreedyRowPackingService.
    /// </summary>
    public partial class SmartViewToSheetPlacerViewModel
    {
        // ---- Stage 2 state ----
        [ObservableProperty] private double _gapHorizontalMm = 5.0;
        [ObservableProperty] private double _gapVerticalMm = 5.0;
        [ObservableProperty] private int _suggestedSheetCount;
        [ObservableProperty] private bool _stage2Complete;
        public string Stage2StatusLabel => Stage2Complete ? "Complete" : (Stage1Complete ? "In Progress" : "Not Started");

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

        private void RunPacking()
        {
            SuggestedSheets.Clear();
            var selected = AllViews.Where(v => v.IsSelected).ToList();

            Logs.Add(new LogEntry(LogLevel.Info,
                $"Packing started: {selected.Count} view(s) selected | Margins T{MarginTopMm}/B{MarginBottomMm}/L{MarginLeftMm}/R{MarginRightMm}mm | Gap H{GapHorizontalMm}/V{GapVerticalMm}mm."));

            var packed = GreedyRowPackingService.Pack(selected, SelectedTitleblock!, GapHorizontalMm, GapVerticalMm);
            foreach (var sheet in packed)
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
            Logs.Add(new LogEntry(LogLevel.Info,
                $"Packing complete: {selected.Count} view(s) across {SuggestedSheetCount} suggested sheet(s)."));

            PlaceViewsCommand.NotifyCanExecuteChanged();

            // Default the Sheet Preview dropdown to the first suggested sheet.
            SelectedPreviewSheet = SuggestedSheets.FirstOrDefault();
            RecomputePreviewGeometry();
        }

        partial void OnSelectedPreviewSheetChanged(SheetGroup? value)
        {
            RecomputePreviewGeometry();
        }

        /// <summary>Editing a Gap field in Stage 2 re-runs packing immediately so the
        /// Sheet Preview canvas reflects the new spacing without requiring "Next".</summary>
        partial void OnGapHorizontalMmChanged(double value) => RepackIfReady();

        /// <summary>See OnGapHorizontalMmChanged.</summary>
        partial void OnGapVerticalMmChanged(double value) => RepackIfReady();

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

            GreedyRowPackingService.RevalidateFit(placement.AssignedSheet, SelectedTitleblock, GapHorizontalMm, GapVerticalMm);
            RecomputePreviewGeometry();
        }

        [RelayCommand]
        private void BackToStage1()
        {
            Stage2Expanded = false;
            Stage1Expanded = true;
        }

        /// <summary>Advances from Stage 2 (Preview Placement) into Stage 3
        /// (Suggested Placement — badges + editable grid). Re-runs packing
        /// first so any Gap field edits made in Stage 2's Packing Algorithm
        /// card are reflected before the grid is shown.</summary>
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
