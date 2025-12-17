using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace AutoLinerCommand_V02.Helpers
{
    public class SelectionFilterRoof : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem.Category != null &&
                   elem.Category.Id.Value == (int)BuiltInCategory.OST_Roofs;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
