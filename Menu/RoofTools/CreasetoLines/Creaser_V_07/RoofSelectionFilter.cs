using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.Creaser_V07.Commands
{
    internal class RoofSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element e) => e is RoofBase;
        public bool AllowReference(Reference r, XYZ p) => false;
    }
}
