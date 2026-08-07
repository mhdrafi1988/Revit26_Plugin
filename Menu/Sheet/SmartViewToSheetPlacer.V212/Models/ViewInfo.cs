using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.SmartViewToSheetPlacer.V212.Models
{
    /// <summary>
    /// Represents a single Revit view available for placement, along with its
    /// pre-computed size on sheet (mm) at its current scale. WidthMm/HeightMm
    /// are calculated once when views are loaded (Stage 1) and consumed
    /// directly by the packing algorithm in Stage 2 — never recomputed
    /// mid-packing.
    ///
    /// V212: added RightDirection/UpDirection/CropCenterModel/IsMarkerResolved —
    /// these feed the new reading-order sort (ReadingOrderPackingService),
    /// ported conceptually from APUS's section-marker projection but built
    /// from each View's own crop box center instead of a marker point.
    /// Populated once in the handler's ExecuteLoadViews, same pass as
    /// WidthMm/HeightMm — never recomputed mid-packing, same rule as above.
    /// </summary>
    public partial class ViewInfo : ObservableObject
    {
        /// <summary>The underlying Revit View ElementId.</summary>
        public ElementId ViewId { get; }

        /// <summary>View name as shown in the Project Browser.</summary>
        public string Name { get; }

        /// <summary>Raw Revit ViewType enum value (FloorPlan, Section, Elevation, Drafting, Detail, etc.).</summary>
        public ViewType RevitViewType { get; }

        /// <summary>Human-friendly label for RevitViewType (e.g. "Floor Plan", "Structural Plan").</summary>
        public string ViewTypeLabel { get; }

        /// <summary>View scale as an integer denominator (e.g. 100 for 1:100). Zero for scale-less views (3D, Legend).</summary>
        public int Scale { get; }

        /// <summary>Display string for scale, e.g. "1:100" or "—" if not applicable.</summary>
        public string ScaleLabel => Scale > 0 ? $"1:{Scale}" : "—";

        /// <summary>Computed width on sheet in millimeters (crop box width × scale factor, converted from feet).</summary>
        public double WidthMm { get; }

        /// <summary>Computed height on sheet in millimeters (crop box height × scale factor, converted from feet).</summary>
        public double HeightMm { get; }

        /// <summary>View's Right direction vector (model space), used to project the
        /// crop box center onto the U axis for reading-order sort. Null if the
        /// view type does not expose a meaningful Right direction (falls back
        /// to IsMarkerResolved = false).</summary>
        public XYZ? RightDirection { get; }

        /// <summary>View's Up direction vector (model space), used to project the
        /// crop box center onto the V axis (row-banding) for reading-order sort.</summary>
        public XYZ? UpDirection { get; }

        /// <summary>Crop box center in model space (X,Y), used as the reading-order
        /// sort anchor in place of APUS's section marker point. Null if the view
        /// has no crop box or CropBox read failed — see IsMarkerResolved.</summary>
        public XYZ? CropCenterModel { get; }

        /// <summary>Whether CropCenterModel/RightDirection/UpDirection were
        /// successfully resolved for this view. False views are logged as a
        /// Warning at load time and pushed to the end of the reading-order sort
        /// (SortFallback.PushToEnd) — they are still placed, never skipped or
        /// failed, only sorted last.</summary>
        public bool IsMarkerResolved { get; }

        /// <summary>Whether this view is checked for inclusion in the placement run.</summary>
        [ObservableProperty]
        private bool _isSelected;

        /// <summary>
        /// V212: true once the user has manually toggled THIS row's checkbox
        /// by hand (via the grid), as opposed to IsSelected being set by the
        /// View Type popover's bulk check/uncheck. Confirmed with Rafi:
        /// once a row is manually touched, future popover toggles for that
        /// row's ViewType leave it alone — this flag is how
        /// ApplyViewTypeSelectionFromPopover() (Stage1 partial) knows which
        /// rows to skip. Never reset automatically — only a fresh LoadViews
        /// (new ViewInfo instances entirely) clears it.
        /// </summary>
        public bool WasManuallySetByUser { get; set; }

        public ViewInfo(
            ElementId viewId,
            string name,
            ViewType revitViewType,
            string viewTypeLabel,
            int scale,
            double widthMm,
            double heightMm,
            XYZ? rightDirection = null,
            XYZ? upDirection = null,
            XYZ? cropCenterModel = null,
            bool isMarkerResolved = false,
            bool isSelected = false)
        {
            ViewId = viewId;
            Name = name;
            RevitViewType = revitViewType;
            ViewTypeLabel = viewTypeLabel;
            Scale = scale;
            WidthMm = widthMm;
            HeightMm = heightMm;
            RightDirection = rightDirection;
            UpDirection = upDirection;
            CropCenterModel = cropCenterModel;
            IsMarkerResolved = isMarkerResolved;
            _isSelected = isSelected;
        }
    }
}
