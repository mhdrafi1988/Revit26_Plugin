using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.RoofPointComparison.V002.Infrastructure.Helpers
{
    /// <summary>Restricts element picking to roof elements only.</summary>
    public class RoofSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is RoofBase;

        public bool AllowReference(Reference reference, XYZ position) => true;
    }
}
