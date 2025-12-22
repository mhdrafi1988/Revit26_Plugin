// ============================================================
// File: RoofSelectionFilter.cs
// Namespace: Revit26_Plugin.Creaser_V06.Commands
// ============================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.Creaser_V06.Commands
{
    internal class RoofSelectionFilter : ISelectionFilter
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
