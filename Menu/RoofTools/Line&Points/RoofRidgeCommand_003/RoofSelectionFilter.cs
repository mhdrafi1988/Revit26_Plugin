using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace Revit22_Plugin.RRLPV3.Commands
{
    public class RoofSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is RoofBase;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}