using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.RoofTag_V006
{
    public static class SelectionHelper
    {
        /// <summary>
        /// Prompts the user to select a single RoofBase element.
        /// Returns null if the user cancels or selects a non-roof element.
        /// </summary>
        public static RoofBase SelectRoof(UIDocument uiDoc)
        {
            try
            {
                Reference r = uiDoc.Selection.PickObject(
                    ObjectType.Element,
                    new RoofOnlyFilter(),
                    "Select a Roof");

                return r != null
                    ? uiDoc.Document.GetElement(r) as RoofBase
                    : null;
            }
            catch
            {
                // User cancelled or pressed Escape
                return null;
            }
        }

        // ── Selection filter: RoofBase only ──────────────────────────────
        private class RoofOnlyFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)   => elem is RoofBase;
            public bool AllowReference(Reference r, XYZ pos) => false;
        }
    }
}
