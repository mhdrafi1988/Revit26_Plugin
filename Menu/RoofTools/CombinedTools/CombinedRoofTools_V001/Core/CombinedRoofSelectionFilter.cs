using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.CombinedRoofTools.V001.Core
{
    /// <summary>
    /// Shared pick filter for the combined window's roof selection — accepts
    /// any RoofBase (FootPrintRoof, ExtrusionRoof, ...). Individual tools
    /// that need a narrower type (e.g. Auto Slope By Drain needs
    /// FootPrintRoof) check that themselves in CombinedRoofToolsInitializer
    /// and show a friendly "unavailable" message in that tab instead of
    /// restricting what can be picked for the other four tools.
    /// </summary>
    public class CombinedRoofSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is RoofBase;
        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
