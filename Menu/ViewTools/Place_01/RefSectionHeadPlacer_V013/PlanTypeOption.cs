using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.RefSectionHeadPlacer.V013.Core.Models
{
    /// <summary>
    /// Bindable row for the Plan Type filter popover above Grid 1.
    /// One row per distinct Revit ViewType found among the loaded plan views
    /// (e.g. FloorPlan, CeilingPlan, AreaPlan, EngineeringPlan).
    /// </summary>
    public partial class PlanTypeOption : ObservableObject
    {
        /// <summary>Raw Revit ViewType.ToString() value (e.g. "FloorPlan") — used to match PlanViewRow.ViewType.</summary>
        public string RawViewType { get; }

        /// <summary>Friendlier label shown in the popover (e.g. "Floor Plan").</summary>
        public string DisplayName { get; }

        [ObservableProperty]
        private bool isChecked;

        public PlanTypeOption(string rawViewType, string displayName, bool isChecked = true)
        {
            RawViewType = rawViewType;
            DisplayName = displayName;
            IsChecked = isChecked;
        }
    }
}
