using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.SmartViewToSheetPlacer.V220.Models
{
    /// <summary>
    /// Per-ViewType-group packing settings: horizontal/vertical gap values
    /// in mm. One instance exists per RevitViewType group present in the
    /// current run.
    ///
    /// V220 CHANGE: GapStyle enum (Fixed/EvenGap) and the IsFixedStyle/
    /// IsEvenGapStyle toggle wrapper properties REMOVED entirely, per
    /// explicit confirmation ("fixed gap only required") — none of the 7
    /// ported packing strategies (see RowFillStrategy) support EvenGap's
    /// stretch-to-fill row layout, only a constant per-item gap. Fixed
    /// tight-left packing is now the only behavior; H-Gap/V-Gap remain the
    /// only per-group inputs. The Stage 2 XAML Fixed/Even-Gap toggle
    /// buttons and their SetGapStyleFixed/SetGapStyleEvenGap ViewModel
    /// commands are removed along with this.
    /// </summary>
    public partial class ViewGroupGapSettings : ObservableObject
    {
        /// <summary>The ViewType this gap setting applies to.</summary>
        public ViewType RevitViewType { get; }

        /// <summary>Human-friendly label for display in the group header (e.g. "Floor Plan").</summary>
        public string ViewTypeLabel { get; }

        /// <summary>Horizontal gap in mm — constant spacing between items in a row (tight-left packing).</summary>
        [ObservableProperty]
        private double _horizontalGapMm = 5.0;

        /// <summary>Vertical gap in mm between rows.</summary>
        [ObservableProperty]
        private double _verticalGapMm = 5.0;

        public ViewGroupGapSettings(ViewType revitViewType, string viewTypeLabel)
        {
            RevitViewType = revitViewType;
            ViewTypeLabel = viewTypeLabel;
        }
    }
}
