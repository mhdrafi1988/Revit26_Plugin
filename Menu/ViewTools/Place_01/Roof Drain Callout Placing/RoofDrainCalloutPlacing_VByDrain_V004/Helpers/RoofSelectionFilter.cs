using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.RoofDrainCalloutPlacing.VByDrain.V004.Helpers
{
    /// <summary>
    /// Restricts element selection to FootPrintRoof only. Detection (see
    /// RoofOpeningDetectionService) is now Sketch-based, which requires a
    /// FootPrintRoof — a bare RoofBase/ExtrusionRoof has no Sketch to read,
    /// so allowing them here would only lead to a "no openings detected"
    /// dead end after picking. Confirmed with Rafi.
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
