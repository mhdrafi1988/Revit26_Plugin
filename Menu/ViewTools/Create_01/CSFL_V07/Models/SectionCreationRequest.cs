using Autodesk.Revit.DB;

namespace Revit26_Plugin.CSFL_V07.Models
{
    public class SectionCreationRequest
    {
        public DetailLine SourceLine { get; }
        public Line GeometryLine { get; }
        public OrientationResult Orientation { get; }
        public CandidateHostElement HostElement { get; }
        public SectionCreationOptions Options { get; }

        public SectionCreationRequest(
            DetailLine sourceLine,
            Line geometryLine,
            OrientationResult orientation,
            CandidateHostElement hostElement,
            SectionCreationOptions options)
        {
            SourceLine = sourceLine;
            GeometryLine = geometryLine;
            Orientation = orientation;
            HostElement = hostElement;
            Options = options;
        }
    }
}
