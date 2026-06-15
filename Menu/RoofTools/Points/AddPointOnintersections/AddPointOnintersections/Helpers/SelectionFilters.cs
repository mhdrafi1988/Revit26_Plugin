using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.AddPointOnintersections.Helpers
{
    public class FootPrintRoofSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is FootPrintRoof;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }

    public class DetailLineSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is DetailLine;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}