using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.TaperedEpsPattern2D.V004.Revit
{
    /// <summary>
    /// Entry check 2 — uses a single preselected roof, otherwise prompts for one.
    /// Returns null (with a message) on Esc or when the element is not a roof.
    /// </summary>
    public static class RoofPicker
    {
        public static RoofBase? Pick(UIDocument uidoc, out string message)
        {
            Document doc = uidoc.Document;
            message = string.Empty;

            ICollection<ElementId> preselected = uidoc.Selection.GetElementIds();
            if (preselected.Count == 1)
            {
                foreach (ElementId id in preselected)
                {
                    if (doc.GetElement(id) is RoofBase preRoof)
                    {
                        return preRoof;
                    }
                }
            }

            Reference picked;
            try
            {
                picked = uidoc.Selection.PickObject(ObjectType.Element, new RoofSelectionFilter(),
                    "Tapered EPS 2D Pattern: select a footprint roof (Esc to cancel)");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                message = "No roof was selected, so the tool was cancelled.";
                return null;
            }

            if (doc.GetElement(picked) is RoofBase roof)
            {
                return roof;
            }

            message = "The selected element is not a roof. Select a footprint roof and run the tool again.";
            return null;
        }
    }
}
