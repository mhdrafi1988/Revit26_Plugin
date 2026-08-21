using System.Collections.Generic;

namespace Revit26_Plugin.PlanFromScopeBox.V003.Core.Models
{
    /// <summary>
    /// Existing view/sheet placements found for one scope box, prior to this run.
    /// A scope box can be the crop source for multiple views (VIEWER_VOLUME_OF_INTEREST_CROP
    /// is a many-to-one relationship in the other direction: many views -> one scope box), and
    /// each of those views may or may not yet be placed on a sheet.
    /// </summary>
    public sealed class ScopeBoxUsage
    {
        /// <summary>
        /// One entry per cropped view: the sheet number it's placed on, or the literal
        /// string "(unplaced)" if the view exists but isn't on any sheet yet.
        /// </summary>
        public List<string> Entries { get; } = new();

        public bool IsUsed => Entries.Count > 0;

        /// <summary>Comma-joined display string for the grid cell. Empty if unused.</summary>
        public string Display => string.Join(", ", Entries);
    }
}
