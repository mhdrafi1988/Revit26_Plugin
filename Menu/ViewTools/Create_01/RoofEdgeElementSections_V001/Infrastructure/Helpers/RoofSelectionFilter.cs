using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.RoofEdgeElementSections.V001
{
    /// <summary>Restricts PickObjects (the "Pick Roofs" button) to RoofBase elements only.</summary>
    public class RoofSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is RoofBase;
        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
