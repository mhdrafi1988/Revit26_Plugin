using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.V5_00.Infrastructure.Revit
{
    public class RoofSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is RoofBase;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return true;
        }
    }
}
