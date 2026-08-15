using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.RefSectionHeadPlacer.V013.Core.Models
{
    /// <summary>
    /// Bindable row for the Roof Type filter popover (row 3, next to Boundary buffer).
    /// One row per distinct roof type name found among roofs in the selected plan
    /// views. Only ticked roof types anchor the boundary buffer's bounding-box
    /// expansion in ElementCollectorService.CollectElementTypes — unticked roof
    /// types are excluded from Grid 2's Roofs rows too, since they are out of scope
    /// either way.
    /// </summary>
    public partial class RoofTypeOption : ObservableObject
    {
        /// <summary>Roof type name (e.g. "Metal Deck") — matches Element type Name via GetTypeName().</summary>
        public string RawTypeName { get; }

        /// <summary>Same as RawTypeName — kept as a separate property for XAML binding
        /// consistency with PlanTypeOption.DisplayName (no friendly-name mapping needed here).</summary>
        public string DisplayName => RawTypeName;

        [ObservableProperty]
        private bool isChecked;

        public RoofTypeOption(string rawTypeName, bool isChecked = true)
        {
            RawTypeName = rawTypeName;
            IsChecked = isChecked;
        }
    }
}
