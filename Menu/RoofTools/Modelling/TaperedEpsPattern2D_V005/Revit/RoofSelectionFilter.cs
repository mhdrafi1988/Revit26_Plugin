using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin_Revised.TaperedEpsPattern2D.V005.Revit
{
    /// <summary>Only roofs can be picked (footprint and extrusion — extrusion is rejected later with a message).</summary>
    public sealed class RoofSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is RoofBase;

        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
