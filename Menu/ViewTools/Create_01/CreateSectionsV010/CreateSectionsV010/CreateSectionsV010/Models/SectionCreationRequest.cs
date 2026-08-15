using Autodesk.Revit.DB;

namespace Revit26_Plugin.CreateSectionsFromDetailLines.V010.Models
{
    /// <summary>
    /// V008: converted from V07's 5-parameter positional constructor to
    /// named `required` init properties (same rationale as SectionCreationOptions).
    /// HostElement is nullable to support the threshold-fallback path,
    /// where no host was found but the section is still created.
    /// </summary>
    public class SectionCreationRequest
    {
        public required DetailLine SourceLine { get; init; }
        public required Line GeometryLine { get; init; }
        public required OrientationResult Orientation { get; init; }

        /// <summary>Null when using the threshold-fallback path (no host found).</summary>
        public CandidateHostElement HostElement { get; init; }

        public required SectionCreationOptions Options { get; init; }
    }
}
