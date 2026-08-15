using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.RoofDrainCalloutPlacing.V005.Models
{
    /// <summary>
    /// One picked drain point row, shown in the Drain Points list. Position is
    /// the actual shape vertex position when snapped; SnapDeltaFeet is null for
    /// an unsnapped pick (raw point kept as-is, no vertex within tolerance).
    ///
    /// V004: RoofId is retained even though only one roof is ever picked per
    /// run (single-roof workflow) — kept for traceability in the grid/log and
    /// because CalloutPlacementService's grouping-by-roof code path is
    /// unchanged and still expects it.
    /// </summary>
    public partial class CandidatePoint : ObservableObject
    {
        public ElementId RoofId { get; set; }

        public XYZ Position { get; set; }

        /// <summary>Distance (feet) from the raw pick to the vertex it snapped to. Null if unsnapped.</summary>
        public double? SnapDeltaFeet { get; set; }

        /// <summary>Grid/list checkbox state — bound TwoWay from the DataGrid row. Unused for selection logic in V004 (no review grid), kept as-is for possible future re-use.</summary>
        [ObservableProperty] private bool isSelected;
    }
}
