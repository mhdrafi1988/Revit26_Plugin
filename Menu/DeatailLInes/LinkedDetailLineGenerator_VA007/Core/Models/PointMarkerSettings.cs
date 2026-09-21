using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA007.Core.Models
{
    /// <summary>
    /// Circle marker sizing (Section 4a). Value represents diameter in mm.
    /// Persisted to settings.json.
    /// </summary>
    public partial class CircleMarkerSettings : ObservableObject
    {
        [ObservableProperty]
        private double _diameterMm = 300;
    }

    /// <summary>
    /// Section 4c. RepresentationMode.ActualProfile draws a column's real footprint;
    /// when neither its geometry nor its size parameters can be read, this picks
    /// which fixed-size marker (Circle → 4a, Rectangle → 4b) is drawn instead.
    /// Persisted to settings.json.
    /// </summary>
    public partial class ActualProfileSettings : ObservableObject
    {
        [ObservableProperty]
        private PointMarkerShape _fallbackShape = PointMarkerShape.Circle;
    }

    /// <summary>
    /// Rectangle marker sizing (Section 4b). Width/Height in mm, independent axes.
    /// Persisted to settings.json.
    /// </summary>
    public partial class RectangleMarkerSettings : ObservableObject
    {
        [ObservableProperty]
        private double _widthMm = 300;

        [ObservableProperty]
        private double _heightMm = 300;

        /// <summary>How the rectangle is rotated around its center point.
        /// Defaults to InstanceRotation — matching the linked element's own
        /// placement is the more accurate default; ProjectAxes (unrotated) is
        /// available for anyone who wants the original behavior back.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsManualAlignment))]
        private RectangleAlignmentMode _alignmentMode = RectangleAlignmentMode.InstanceRotation;

        /// <summary>Angle in degrees, only used when AlignmentMode == Manual.</summary>
        [ObservableProperty]
        private double _manualAngleDegrees = 0;

        /// <summary>UI helper — shows/hides the manual angle field.</summary>
        public bool IsManualAlignment => AlignmentMode == RectangleAlignmentMode.Manual;
    }
}
