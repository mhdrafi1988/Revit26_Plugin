using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.Creaser_V03_03.Services
{
    public static class RoofSelectionService
    {
        public static RoofBase PickRoof(UIDocument uiDoc)
        {
            try
            {
                Reference r = uiDoc.Selection.PickObject(
                    ObjectType.Element,
                    new RoofFilter(),
                    "Select a roof");

                return uiDoc.Document.GetElement(r) as RoofBase;
            }
            catch
            {
                return null;
            }
        }
    }

    internal class RoofFilter : ISelectionFilter
    {
        public bool AllowElement(Element e) => e is RoofBase;
        public bool AllowReference(Reference r, XYZ p) => false;
    }
}
