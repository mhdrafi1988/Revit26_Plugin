using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.SheetAutoRearrange.V009.Core.Models
{
    /// <summary>
    /// One axis's detection settings — used twice (Tall = height axis,
    /// Wide = width axis), each fully independent per confirmed design:
    /// separate Enable, Multiplier, Tolerance, and OverflowGrouping per axis.
    /// Persisted as part of the tool's settings.json.
    /// </summary>
    public partial class TallWideDetectionSettings : ObservableObject
    {
        [ObservableProperty] private bool isEnabled = true;

        /// <summary>View is flagged when its size ≥ (mode × Multiplier), within ± TolerancePercent. Default 2.0 per confirmed spec.</summary>
        [ObservableProperty] private double multiplier = 2.0;

        /// <summary>Tolerance band around the multiplier threshold, as a percentage. Default 10.</summary>
        [ObservableProperty] private double tolerancePercent = 10.0;

        [ObservableProperty] private ShelfOverflowGrouping overflowGrouping = ShelfOverflowGrouping.KeepShelfTogether;
    }
}
