// =======================================================
// File: MultiplePointsSettings.cs
// Location: Core/Models/
// Global point-placement rule, shared by every edge — three independent
// checkboxes (per the user's explicit request: midpoint, quarter points,
// and extra points on long edges each toggle separately) plus the
// length threshold that gates the third checkbox.
// =======================================================

using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.MultiplePoints.V001.Core.Models
{
    public partial class MultiplePointsSettings : ObservableObject
    {
        /// <summary>Add a point at the edge midpoint (t = 1/2 by arc length).</summary>
        [ObservableProperty]
        private bool addMidpoint = true;

        /// <summary>Add points at the quarter marks (t = 1/4 and 3/4 by arc length).</summary>
        [ObservableProperty]
        private bool addQuarterPoints = true;

        /// <summary>
        /// When on, any edge longer than <see cref="ThresholdMeters"/> gets extra
        /// equally-spaced points (on top of midpoint/quarter) so no gap between
        /// consecutive points on that edge exceeds the threshold.
        /// </summary>
        [ObservableProperty]
        private bool addExtraOnLongEdges;

        [ObservableProperty]
        private double thresholdMeters = 1.00;
    }
}
