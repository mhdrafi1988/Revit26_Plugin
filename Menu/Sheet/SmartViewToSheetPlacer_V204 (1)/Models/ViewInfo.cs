using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.SmartViewToSheetPlacer.V204.Models
{
    /// <summary>
    /// Represents a single Revit view available for placement, along with its
    /// pre-computed size on sheet (mm) at its current scale. WidthMm/HeightMm
    /// are calculated once when views are loaded (Stage 1) and consumed
    /// directly by the packing algorithm in Stage 2 — never recomputed
    /// mid-packing.
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

        /// <summary>Whether this view is checked for inclusion in the placement run.</summary>
        [ObservableProperty]
        private bool _isSelected;

        public ViewInfo(
            ElementId viewId,
            string name,
            ViewType revitViewType,
            string viewTypeLabel,
            int scale,
            double widthMm,
            double heightMm,
            bool isSelected = false)
        {
            ViewId = viewId;
            Name = name;
            RevitViewType = revitViewType;
            ViewTypeLabel = viewTypeLabel;
            Scale = scale;
            WidthMm = widthMm;
            HeightMm = heightMm;
            _isSelected = isSelected;
        }
    }
}
