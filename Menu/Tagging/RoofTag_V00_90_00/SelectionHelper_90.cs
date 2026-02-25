using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace Revit22_Plugin.RoofTag_V90
{
    public static class SelectionHelperV3
    {
        /// <summary>
        /// Lets the user select ONE roof element (RoofBase).
        /// </summary>
        public static RoofBase SelectRoof(UIDocument uiDoc)
        {
            try
            {
                Reference r = uiDoc.Selection.PickObject(
                    ObjectType.Element,
                    new RoofOnlyFilter(),
                    "Select a Roof");

                if (r != null)
                    return uiDoc.Document.GetElement(r) as RoofBase;
            }
            catch
            {
                // user cancelled or invalid selection
            }
            return null;
        }

        // -----------------------------------------------------------
        // FILTER: RoofBase only
        // -----------------------------------------------------------
        private class RoofOnlyFilter : ISelectionFilter
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
}
