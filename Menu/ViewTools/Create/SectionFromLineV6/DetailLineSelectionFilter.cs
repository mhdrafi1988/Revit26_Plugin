using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace Revit22_Plugin.PlanSections.Filters
{
    /// <summary>
    /// Allows ONLY straight DetailLines to be selected.
    /// </summary>
    public class DetailLineSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            if (elem is DetailLine dl)
            {
                Curve c = dl.GeometryCurve;
                return (c is Line);   // only STRAIGHT lines allowed
            }

            return false;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
