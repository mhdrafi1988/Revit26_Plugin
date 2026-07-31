using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.SmartViewToSheetPlacer.V204.Models
{
    /// <summary>
    /// One entry in Stage 1's "View Type" column filter popover checkbox list.
    /// Built from the distinct RevitViewType values present in the loaded
    /// view collection (Stage 1's own ViewType enum grouping — same grouping
    /// used later for non-mixed sheet placement in Stage 2).
    /// </summary>
    public partial class ViewTypeFilterOption : ObservableObject
    {
        public ViewType RevitViewType { get; }

        /// <summary>Human-friendly label, e.g. "Floor Plan", "Section".</summary>
        public string Label { get; }

        /// <summary>Whether this type is currently included in the filter (checked).</summary>
        [ObservableProperty]
        private bool _isChecked = true;

        public ViewTypeFilterOption(ViewType revitViewType, string label)
        {
            RevitViewType = revitViewType;
            Label = label;
        }
    }
}
