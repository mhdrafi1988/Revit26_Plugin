using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Revit26_Plugin.SheetAutoRearrange.V002.Core.Models
{
    /// <summary>
    /// Gap mode for a single ViewType group — Fixed applies the same H/V gap
    /// between every pair of views in the group; Even distributes remaining
    /// space evenly. Mirrors the V213 gap-group convention.
    /// </summary>
    public enum GapMode
    {
        Fixed,
        Even
    }

    /// <summary>
    /// H/V gap setting (mm) for one ViewType group (e.g. "Floor Plan",
    /// "Section / Elevation", "3D View", "Schedule"). Observable so UI edits
    /// (Mode/H/V TextBoxes) raise PropertyChanged for the live preview.
    /// </summary>
    public partial class ViewTypeGapGroup : ObservableObject
    {
        public string GroupName { get; set; } = string.Empty;

        [ObservableProperty] private GapMode mode = GapMode.Fixed;
        [ObservableProperty] private double horizontalGapMm = 10;
        [ObservableProperty] private double verticalGapMm = 10;
    }

    /// <summary>
    /// Titleblock usable-area margins (mm) plus per-ViewType gap groups.
    /// Applies together on Run — margins define the packing boundary,
    /// each group's H/V gap defines spacing between views of that type.
    /// Observable so margin edits raise PropertyChanged for the live preview.
    /// </summary>
    public partial class GapSettings : ObservableObject
    {
        [ObservableProperty] private double marginTopMm = 15;
        [ObservableProperty] private double marginBottomMm = 15;
        [ObservableProperty] private double marginLeftMm = 15;
        [ObservableProperty] private double marginRightMm = 15;

        public ObservableCollection<ViewTypeGapGroup> Groups { get; set; } = new()
        {
            new ViewTypeGapGroup { GroupName = "Floor Plan",           HorizontalGapMm = 10, VerticalGapMm = 10 },
            new ViewTypeGapGroup { GroupName = "Section / Elevation",  HorizontalGapMm = 10, VerticalGapMm = 10 },
            new ViewTypeGapGroup { GroupName = "3D View",              HorizontalGapMm = 10, VerticalGapMm = 10 },
            new ViewTypeGapGroup { GroupName = "Schedule",             HorizontalGapMm = 10, VerticalGapMm = 10 },
        };
    }
}
