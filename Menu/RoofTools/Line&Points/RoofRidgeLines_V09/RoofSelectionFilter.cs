using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V08
{
    public class RoofSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            // Allow only RoofBase elements (including FootPrintRoof and ExtrusionRoof)
            return elem is RoofBase;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false; // Only allow element selection, not references
        }
    }
}