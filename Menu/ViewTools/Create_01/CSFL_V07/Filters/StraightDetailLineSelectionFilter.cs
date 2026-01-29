using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.CSFL_V07.Filters
{
    public class StraightDetailLineSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
            => elem is DetailLine dl && dl.GeometryCurve is Line;

        public bool AllowReference(Reference reference, XYZ position)
            => false;
    }
}
