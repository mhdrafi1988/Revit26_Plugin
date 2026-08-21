using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA003.Infrastructure.Helpers
{
    /// <summary>Restricts PickObject (RectangleMarker's "Pick Line" alignment button)
    /// to Detail Line elements only, per suite convention (see
    /// CreateSections_V011's StraightDetailLineSelectionFilter / RoofDetailLineIntersect_V011's
    /// DetailLineSelectionFilter for the same pattern elsewhere in the suite).</summary>
    public class DetailLineSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is DetailLine;
        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
