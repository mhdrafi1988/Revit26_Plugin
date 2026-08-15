using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.SheetAutoRearrange.V011.Core.Models
{
    /// <summary>
    /// Titleblock usable-area margins (mm) plus ONE global gap between views.
    ///
    /// V009 CHANGE: removed the per-ViewType gap-group system entirely
    /// (Groups / ViewTypeGapGroup / GapMode, and ViewTypeGroupResolver which
    /// only existed to resolve which group applied to a given view). Gap
    /// between views is now a single flat H/V value applied globally, per
    /// explicit request ("gap between views are now based on groups, it can
    /// be global for all").
    /// </summary>
    public partial class GapSettings : ObservableObject
    {
        [ObservableProperty] private double marginTopMm = 15;
        [ObservableProperty] private double marginBottomMm = 15;
        [ObservableProperty] private double marginLeftMm = 15;
        [ObservableProperty] private double marginRightMm = 15;

        [ObservableProperty] private double globalHorizontalGapMm = 10;
        [ObservableProperty] private double globalVerticalGapMm = 10;
    }
}
