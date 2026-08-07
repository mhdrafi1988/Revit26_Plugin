using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.SmartViewToSheetPlacer.V213.Models
{
    /// <summary>
    /// Which gap-distribution style a row layout uses. Confirmed per Rafi:
    /// Fixed = tight-left packing with a constant gap between items
    /// (original V213 SmartView behavior). EvenGap = gap is stretched to
    /// fill leftover row width, first item flush-left, last item
    /// flush-right, single-item rows centered (ported from APUS V321
    /// EvenGapPlacementService).
    /// </summary>
    public enum GapStyle
    {
        Fixed,
        EvenGap
    }

    /// <summary>
    /// Per-ViewType-group packing settings: gap style (Fixed/EvenGap) plus
    /// horizontal/vertical gap values in mm. One instance exists per
    /// RevitViewType group present in the current run (confirmed: gap style
    /// is chosen per-group by Rafi in the Stage 2 grid, NOT globally —
    /// replaces the old single global GapHorizontalMm/GapVerticalMm pair
    /// that lived directly on the ViewModel in V213).
    /// </summary>
    public partial class ViewGroupGapSettings : ObservableObject
    {
        /// <summary>The ViewType this gap setting applies to.</summary>
        public ViewType RevitViewType { get; }

        /// <summary>Human-friendly label for display in the group header (e.g. "Floor Plan").</summary>
        public string ViewTypeLabel { get; }

        /// <summary>Fixed (tight-left) or EvenGap (stretch-to-fill) row layout for this group.</summary>
        [ObservableProperty]
        private GapStyle _gapStyle = GapStyle.Fixed;

        /// <summary>Bool wrapper over GapStyle for the Stage 2 XAML RadioButton pair
        /// (Fixed/Even-Gap segmented toggle) — avoids introducing a new converter
        /// just to bind two mutually-exclusive RadioButtons against a single enum.</summary>
        public bool IsFixedStyle
        {
            get => GapStyle == GapStyle.Fixed;
            set { if (value) GapStyle = GapStyle.Fixed; }
        }

        /// <summary>See IsFixedStyle.</summary>
        public bool IsEvenGapStyle
        {
            get => GapStyle == GapStyle.EvenGap;
            set { if (value) GapStyle = GapStyle.EvenGap; }
        }

        partial void OnGapStyleChanged(GapStyle value)
        {
            OnPropertyChanged(nameof(IsFixedStyle));
            OnPropertyChanged(nameof(IsEvenGapStyle));
        }

        /// <summary>Horizontal gap in mm — for Fixed style, the constant spacing between
        /// items in a row; for EvenGap style, the MINIMUM gap (actual gap stretches to
        /// fill leftover width, never going below this floor).</summary>
        [ObservableProperty]
        private double _horizontalGapMm = 5.0;

        /// <summary>Vertical gap in mm between rows. Always fixed regardless of GapStyle
        /// (EvenGap only redistributes horizontal space within a row, not row spacing).</summary>
        [ObservableProperty]
        private double _verticalGapMm = 5.0;

        public ViewGroupGapSettings(ViewType revitViewType, string viewTypeLabel)
        {
            RevitViewType = revitViewType;
            ViewTypeLabel = viewTypeLabel;
        }
    }
}
