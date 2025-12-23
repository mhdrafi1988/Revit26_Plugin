using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.Creaser_V08.Commands.Helpers
{
    public class RoofSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem != null && elem.Category != null &&
                   elem.Category.Id != null &&
                   elem.Category.Id.Equals(new ElementId((int)BuiltInCategory.OST_Roofs));
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
