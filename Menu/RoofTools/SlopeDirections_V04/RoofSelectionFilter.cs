using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace Revit_26.CornertoDrainArrow_V05
{
    /// <summary>
    /// Restricts selection to Roof elements only.
    /// </summary>
    public sealed class RoofSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem?.Category?.Id != null && elem.Category.Id.Equals(new ElementId((int)BuiltInCategory.OST_Roofs));
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return true;
        }
    }
}
