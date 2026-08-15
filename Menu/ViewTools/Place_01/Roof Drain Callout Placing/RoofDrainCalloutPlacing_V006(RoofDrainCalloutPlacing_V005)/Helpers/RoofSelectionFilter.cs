using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.RoofDrainCalloutPlacing.V006.Helpers
{
    /// <summary>
    /// Restricts Selection.PickObject to RoofBase elements only. Revit rejects
    /// clicks on anything else with its own "not a valid pick" cursor feedback —
    /// no custom validation/dialog needed on our side.
    /// </summary>
    public class RoofSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element element) => element is RoofBase;

        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
