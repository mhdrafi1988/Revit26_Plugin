using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.CreateSectionsFromDetailLines.V010.Models
{
    /// <summary>
    /// V009: new. One row in the "Sections To Create" preview grid —
    /// a resolved preview of what SectionCreationService would produce
    /// for a single detail line, WITHOUT creating anything (read-only scan).
    ///
    /// Populated by SectionPreviewScanHandler. IsIncluded drives whether
    /// Create acts on this line — per confirmed spec, the grid's checked
    /// rows are the source of truth for Create, not the raw PickObjects
    /// selection.
    /// </summary>
    public partial class SectionPreviewRow : ObservableObject
    {
        /// <summary>The source detail line and its Reference — needed by
        /// Create to re-run the actual creation pipeline for this row.</summary>
        public ElementId SourceLineId { get; init; }
        public Reference SourceReference { get; init; }

        public string Level { get; init; }
        public string SectionName { get; init; }
        public string HostTypeDisplay { get; init; }

        public SectionPreviewStatus Status { get; init; }
        public string StatusDisplay { get; init; }

        /// <summary>
        /// Whether this row is included when Create runs.
        /// Default true, EXCEPT for Duplicate Name rows — per confirmed
        /// spec, duplicate-flagged rows default unchecked so the user
        /// consciously re-includes them rather than silently getting
        /// auto-_dupN-suffixed views in bulk.
        /// </summary>
        [ObservableProperty] private bool isIncluded = true;
    }

    /// <summary>V009: new. Status of a single previewed row.</summary>
    public enum SectionPreviewStatus
    {
        Ready,
        NoHostFound,
        DuplicateName
    }
}
