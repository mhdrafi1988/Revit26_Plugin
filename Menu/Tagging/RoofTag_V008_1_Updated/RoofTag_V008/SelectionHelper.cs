using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.RoofTag_V008
{
    /// <summary>
    /// Helper for user selection of roof elements in the model.
    /// </summary>
    public static class SelectionHelper
    {
        /// <summary>
        /// Prompt user to select a single roof element.
        /// Returns the selected RoofBase or null if cancelled.
        /// </summary>
        public static RoofBase SelectRoof(UIDocument uiDoc)
        {
            try
            {
                Reference ref_roof = uiDoc.Selection.PickObject(
                    ObjectType.Element,
                    new RoofSelectionFilter(),
                    "Select a roof element");

                if (ref_roof == null)
                    return null;

                Document doc = uiDoc.Document;
                Element element = doc.GetElement(ref_roof.ElementId);

                return element as RoofBase;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Selection filter: only allow RoofBase elements.
        /// </summary>
        private class RoofSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element e) => e is RoofBase;
            public bool AllowReference(Reference r) => true;
            public bool AllowReference(Reference reference, XYZ position) => true;
        }
    }
}
