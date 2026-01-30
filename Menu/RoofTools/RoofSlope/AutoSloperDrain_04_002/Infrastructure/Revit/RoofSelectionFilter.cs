using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace Revit22_Plugin.V4_02.Infrastructure.Revit
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
