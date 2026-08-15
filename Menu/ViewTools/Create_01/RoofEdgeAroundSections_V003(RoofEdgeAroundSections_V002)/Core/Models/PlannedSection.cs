using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.RoofEdgeAroundSections.V003
{
    /// <summary>
    /// One row of the Planned Sections grid: a single roof edge (one of up to 4 per roof)
    /// with its computed geometry and creation status.
    /// IsIncluded drives whether Run() attempts to create it; disabled in UI for
    /// MergedOut / NoEdgeFound rows (V002 does not support force-recreate/overwrite,
    /// and merged-out rows are shown for transparency only — never created).
    /// </summary>
    public partial class PlannedSection : ObservableObject
    {
        [ObservableProperty]
        private bool isIncluded;

        /// <summary>The source roof element id.</summary>
        public ElementId RoofId { get; set; }

        /// <summary>
        /// The source roof element itself, kept so the naming pass (which now runs after
        /// the proximity-merge pass, on only the surviving rows) can resolve Zone/Level/Area
        /// tokens without re-collecting the roof from RoofId.
        /// </summary>
        public RoofBase RoofElement { get; set; }

        /// <summary>Display name of the roof (Name parameter, or "Roof_{ElementId}" fallback).</summary>
        public string RoofDisplayName { get; set; }

        /// <summary>View-aligned bucket this edge represents.</summary>
        public EdgeDirection Direction { get; set; }

        /// <summary>Proposed section view name, built from the configured naming pattern.</summary>
        public string SectionViewName { get; set; }

        /// <summary>Length of the source boundary edge, in millimeters, for display.</summary>
        public double EdgeLengthMm { get; set; }

        /// <summary>The actual boundary curve selected for this direction (null if NoEdgeFound).</summary>
        public Curve EdgeCurve { get; set; }

        /// <summary>Midpoint of EdgeCurve (arc-aware — curve midpoint, not chord midpoint).</summary>
        public XYZ EdgeMidpoint { get; set; }

        /// <summary>Inward-facing normal at EdgeMidpoint, used as the section's cut/search axis.</summary>
        public XYZ InwardNormal { get; set; }

        /// <summary>Roof's own bounding box, used for crop sizing.</summary>
        public BoundingBoxXYZ RoofBoundingBox { get; set; }

        /// <summary>Result of the plan-build pass: Ready / NoEdgeFound / MergedOut.</summary>
        public PlannedSectionStatus Status { get; set; }

        /// <summary>
        /// When Status == MergedOut, identifies which surviving row absorbed this one
        /// (for the status column / log traceability). Null otherwise.
        /// </summary>
        public string MergedIntoDescription { get; set; }

        /// <summary>Human-readable status text bound to the grid's Status column.</summary>
        public string StatusText => Status switch
        {
            PlannedSectionStatus.Ready => "Ready",
            PlannedSectionStatus.NoEdgeFound => "No Edge Found",
            PlannedSectionStatus.MergedOut => string.IsNullOrEmpty(MergedIntoDescription)
                ? "Merged"
                : $"Merged — matches {MergedIntoDescription}",
            _ => "Unknown"
        };

        public PlannedSection()
        {
            isIncluded = true;
        }
    }
}
