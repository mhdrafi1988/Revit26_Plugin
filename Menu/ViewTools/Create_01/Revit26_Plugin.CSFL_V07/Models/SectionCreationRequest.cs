using Autodesk.Revit.DB;
using Revit26_Plugin.CSFL_V07.Models;
using Revit26_Plugin.CSFL_V07.Services.Geometry;
using Revit26_Plugin.CSFL_V07.ViewModels;

namespace Revit26_Plugin.CSFL_V07.Models
{
    /// <summary>
    /// Immutable data bundle required to create a section.
    /// </summary>
    public class SectionCreationRequest
    {
        public DetailLine SourceLine { get; }
        public Line GeometryLine { get; }
        public OrientationResult Orientation { get; }
        public CandidateHostElement HostElement { get; }
        public SectionFromLineViewModel ViewModel { get; }

        public SectionCreationRequest(
            DetailLine sourceLine,
            Line geometryLine,
            OrientationResult orientation,
            CandidateHostElement hostElement,
            SectionFromLineViewModel viewModel)
        {
            SourceLine = sourceLine;
            GeometryLine = geometryLine;
            Orientation = orientation;
            HostElement = hostElement;
            ViewModel = viewModel;
        }
    }
}
