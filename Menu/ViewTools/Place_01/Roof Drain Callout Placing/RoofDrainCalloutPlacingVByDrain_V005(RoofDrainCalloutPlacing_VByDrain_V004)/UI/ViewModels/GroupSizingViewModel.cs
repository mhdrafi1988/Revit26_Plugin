using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.RoofDrainCalloutPlacingVByDrain.V005.ViewModels
{
    /// <summary>
    /// UI state + text-input binding for one shape group's callout sizing controls
    /// (the Auto/Fixed toggle row shown under each group's DataGrid).
    /// Mirrors GroupSizingSettings but keeps text fields for live TextBox binding,
    /// same pattern as the old CalloutOffsetMmText/CalloutMarginMmText fields.
    /// </summary>
    public partial class GroupSizingViewModel : ObservableObject
    {
        /// <summary>Group key: "Circle", "Rectangle", or "Other".</summary>
        public string GroupKey { get; }

        [ObservableProperty]
        private bool isAutoMode = true;

        [ObservableProperty]
        private string marginMmText = "100";

        [ObservableProperty]
        private string fixedSizeMmText = "500";

        public double MarginMm => double.TryParse(MarginMmText, out var v) ? v : 100;
        public double FixedSizeMm => double.TryParse(FixedSizeMmText, out var v) ? v : 500;

        public GroupSizingViewModel(string groupKey)
        {
            GroupKey = groupKey;
        }

        public void LoadFrom(Models.GroupSizingSettings settings)
        {
            IsAutoMode = settings.Mode != "fixed";
            MarginMmText = settings.Margin.ToString("F0");
            FixedSizeMmText = settings.FixedSize.ToString("F0");
        }

        public Models.GroupSizingSettings ToSettings() => new Models.GroupSizingSettings
        {
            Mode = IsAutoMode ? "auto" : "fixed",
            Margin = MarginMm,
            FixedSize = FixedSizeMm
        };
    }
}
