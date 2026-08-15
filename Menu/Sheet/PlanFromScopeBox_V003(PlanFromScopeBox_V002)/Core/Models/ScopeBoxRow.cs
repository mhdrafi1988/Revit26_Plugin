using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.PlanFromScopeBox.V003.Core.Models
{
    /// <summary>
    /// One row in the scope-box selection grid. Carries the scope box element id,
    /// display metadata (name, level, size), a "fits on A0 at current scale" flag,
    /// and the user's checkbox selection state.
    /// </summary>
    public partial class ScopeBoxRow : ObservableObject
    {
        /// <summary>Revit ElementId value (long) of the scope box element.</summary>
        public long ScopeBoxId { get; init; }

        /// <summary>Scope box name as shown in Revit.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Associated level name (from the active view's level).</summary>
        public string Level { get; init; } = string.Empty;

        /// <summary>Width of the scope box in metres (model extents).</summary>
        public double WidthM { get; init; }

        /// <summary>Height (depth) of the scope box in metres (model extents).</summary>
        public double HeightM { get; init; }

        /// <summary>Formatted "W × H" size string for display (e.g. "42 × 30").</summary>
        public string SizeDisplay => $"{WidthM:0} × {HeightM:0}";

        /// <summary>
        /// Comma-separated list of sheet numbers this scope box is already placed on via an
        /// existing cropped view (e.g. "A-101, A-103"), with "(unplaced)" for any cropped view
        /// not yet on a sheet. Empty string if the scope box has no existing cropped views.
        /// Read-only, informational — set once when the row is built; not used to gate the
        /// checkbox or Fits column.
        /// </summary>
        public string ExistingUsage { get; init; } = string.Empty;

        /// <summary>
        /// True if the view footprint fits inside the printable A0 area at the
        /// currently chosen scale. False → oversized (placed on its own sheet, Warning).
        /// Recomputed when scale/margin changes.
        /// </summary>
        [ObservableProperty]
        private bool _fits = true;

        /// <summary>User checkbox selection in the grid.</summary>
        [ObservableProperty]
        private bool _isSelected;
    }
}
