// File: Models/PlacementRunMetrics.cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.APUS.V322.Models
{
    /// <summary>
    /// Backing data for the 3-metric bar (Placed / Skipped / Failed) shown
    /// under Place/Cancel in the UI. "Skipped" covers already-placed
    /// sections and oversized views; "Failed" covers per-viewport
    /// SubTransaction failures during actual placement.
    /// </summary>
    public partial class PlacementRunMetrics : ObservableObject
    {
        [ObservableProperty]
        private int _placed;

        [ObservableProperty]
        private int _skipped;

        [ObservableProperty]
        private int _failed;

        public void Reset()
        {
            Placed = 0;
            Skipped = 0;
            Failed = 0;
        }
    }
}
