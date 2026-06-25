using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.RoofDetailLineIntersect.V004.Filters
{
    /// <summary>Restricts PickObject to FootPrintRoof elements only.</summary>
    public class RoofSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)        => elem is FootPrintRoof;
        public bool AllowReference(Reference r, XYZ p) => true;
    }

    /// <summary>Restricts PickObjects to DetailLine elements only.</summary>
    public class DetailLineSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)        => elem is DetailLine;
        public bool AllowReference(Reference r, XYZ p) => true;
    }
}
