using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.CreateSectionsFromDetailLines.V010.Filters
{
    /// <summary>Unchanged from V07.</summary>
    public class StraightDetailLineSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
            => elem is DetailLine dl && dl.GeometryCurve is Line;

        public bool AllowReference(Reference reference, XYZ position)
            => false;
    }
}
