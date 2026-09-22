using System.Collections.Generic;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.AutomatedSectionPlacer.V001.Models
{
    /// <summary>
    /// Represents a single Revit view available for placement, along with its
    /// pre-computed size on sheet (mm) at its current scale. WidthMm/HeightMm
    /// are calculated once when views are loaded (Stage 1) and consumed
    /// directly by the packing algorithm in Stage 2 — never recomputed
    /// mid-packing.
    ///
    /// V213: added RightDirection/UpDirection/CropCenterModel/IsMarkerResolved —
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

        /// <summary>View scale as an integer denominator (e.g. 100 for 1:100) at load time. Zero for scale-less views (3D, Legend).</summary>
        public int Scale { get; }

        /// <summary>Display string for scale, e.g. "1:100" or "—" if not applicable. Reflects ScaleOverride once set.</summary>
        public string ScaleLabel => EffectiveScale > 0 ? $"1:{EffectiveScale}" : "—";

        /// <summary>
        /// AutomatedSectionPlacer V001: optional user override for this view's
        /// placement scale, applied before packing/placement. Null (default)
        /// means "use the view's current Scale as-is" — same behavior as
        /// V222. When set, WidthMm/HeightMm are recomputed immediately from
        /// the unscaled crop box size (CropWidthFeet/CropHeightFeet) so the
        /// packing algorithm — which reads WidthMm/HeightMm directly — sees
        /// the overridden size. The actual Revit View.Scale property is only
        /// changed later, in the handler's placement transaction.
        /// </summary>
        [ObservableProperty]
        private int? _scaleOverride;

        /// <summary>Scale actually used for sizing/placement: ScaleOverride if set, otherwise the view's loaded Scale.</summary>
        public int EffectiveScale => ScaleOverride ?? Scale;

        partial void OnScaleOverrideChanged(int? value)
        {
            RecomputeSize();
            OnPropertyChanged(nameof(ScaleLabel));
        }

        /// <summary>Unscaled crop box width/height in feet (model space), captured once at
        /// load time — the basis for recomputing WidthMm/HeightMm whenever ScaleOverride changes.</summary>
        public double CropWidthFeet { get; }
        public double CropHeightFeet { get; }

        private const double MmPerFoot = 304.8;

        private void RecomputeSize()
        {
            double scaleFactor = EffectiveScale > 0 ? EffectiveScale : 1;
            WidthMm = CropWidthFeet * MmPerFoot / scaleFactor;
            HeightMm = CropHeightFeet * MmPerFoot / scaleFactor;
        }

        /// <summary>Computed width on sheet in millimeters (crop box width × scale factor, converted from feet). Mutable — recomputed whenever ScaleOverride changes.</summary>
        [ObservableProperty]
        private double _widthMm;

        /// <summary>Computed height on sheet in millimeters (crop box height × scale factor, converted from feet). Mutable — recomputed whenever ScaleOverride changes.</summary>
        [ObservableProperty]
        private double _heightMm;

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
        /// V213: true once the user has manually toggled THIS row's checkbox
        /// by hand (via the grid), as opposed to IsSelected being set by the
        /// View Type popover's bulk check/uncheck. Confirmed with Rafi:
        /// once a row is manually touched, future popover toggles for that
        /// row's ViewType leave it alone — this flag is how
        /// ApplyViewTypeSelectionFromPopover() (Stage1 partial) knows which
        /// rows to skip. Never reset automatically — only a fresh LoadViews
        /// (new ViewInfo instances entirely) clears it.
        /// </summary>
        public bool WasManuallySetByUser { get; set; }

        /// <summary>
        /// V213: true if this view already has a real Revit sheet placement
        /// (an existing Viewport on some ViewSheet in the model), detected via
        /// View.GetPlacementOnSheetStatus() when views are loaded in the
        /// handler's ExecuteLoadViews. Independent of this tool session's own
        /// packing — reflects the model's actual current state. Drives Stage
        /// 1's All/Placed/Not-Placed filter toggle.
        /// </summary>
        public bool IsAlreadyPlaced { get; }

        /// <summary>
        /// Every parameter this View element carries — built-in (exposed as an
        /// instance Parameter), project, and shared parameters bound to the
        /// Views category alike — keyed by Definition.Name, value as its
        /// display string (AsValueString/AsString). Populated once in the
        /// handler's ExecuteLoadViews via View.Parameters. Feeds Stage 1's
        /// generic Parameter/Value filter (FilterParameterOptions is the union
        /// of these keys across all loaded views).
        /// </summary>
        public IReadOnlyDictionary<string, string> ParameterValues { get; }

        public ViewInfo(
            ElementId viewId,
            string name,
            ViewType revitViewType,
            string viewTypeLabel,
            int scale,
            double widthMm,
            double heightMm,
            double cropWidthFeet = 0,
            double cropHeightFeet = 0,
            XYZ? rightDirection = null,
            XYZ? upDirection = null,
            XYZ? cropCenterModel = null,
            bool isMarkerResolved = false,
            bool isAlreadyPlaced = false,
            bool isSelected = false,
            IReadOnlyDictionary<string, string>? parameterValues = null)
        {
            ViewId = viewId;
            Name = name;
            RevitViewType = revitViewType;
            ViewTypeLabel = viewTypeLabel;
            Scale = scale;
            _widthMm = widthMm;
            _heightMm = heightMm;
            // Fall back to deriving unscaled crop size from the pre-computed
            // widthMm/heightMm at the loaded scale, for any call site that
            // doesn't pass the raw feet explicitly (keeps RecomputeSize
            // correct even if cropWidthFeet/cropHeightFeet are left at 0).
            double loadScaleFactor = scale > 0 ? scale : 1;
            CropWidthFeet = cropWidthFeet > 0 ? cropWidthFeet : widthMm / MmPerFoot * loadScaleFactor;
            CropHeightFeet = cropHeightFeet > 0 ? cropHeightFeet : heightMm / MmPerFoot * loadScaleFactor;
            RightDirection = rightDirection;
            UpDirection = upDirection;
            CropCenterModel = cropCenterModel;
            IsMarkerResolved = isMarkerResolved;
            IsAlreadyPlaced = isAlreadyPlaced;
            _isSelected = isSelected;
            ParameterValues = parameterValues ?? new Dictionary<string, string>();
        }
    }
}
