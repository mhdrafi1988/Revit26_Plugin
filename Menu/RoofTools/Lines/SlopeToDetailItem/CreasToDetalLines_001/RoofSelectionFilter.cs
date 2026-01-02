using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.Creaser_adv_V001.Helpers
{
    /// <summary>
    /// Allows selection of exactly one FootPrintRoof only.
    /// </summary>
    public class RoofSelectionFilter : ISelectionFilter
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
}
