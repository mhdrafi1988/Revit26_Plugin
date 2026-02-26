using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.RoofTag_V73.Helpers
{
    public static class SelectionHelper
    {
        public static RoofBase SelectRoof(UIDocument uiDoc)
        {
            try
            {
                Reference r = uiDoc.Selection.PickObject(
                    ObjectType.Element,
                    new RoofOnlyFilter(),
                    "Select a Roof");

                return uiDoc.Document.GetElement(r) as RoofBase;
            }
            catch
            {
                return null;
            }
        }

        private class RoofOnlyFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is RoofBase;
            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}
