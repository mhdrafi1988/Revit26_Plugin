using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.CreaserAdv_V00.Services.Selection
{
    public class RoofSelectionService
    {
        public Element SelectSingleRoof(UIDocument uiDoc)
        {
            var r = uiDoc.Selection.PickObject(
                ObjectType.Element,
                new RoofFilter(),
                "Select a roof");

            return uiDoc.Document.GetElement(r);
        }

        private class RoofFilter : ISelectionFilter
        {
            public bool AllowElement(Element e)
                => e.Category?.Id.Value ==
                   (int)BuiltInCategory.OST_Roofs;

            public bool AllowReference(Reference r, XYZ p) => false;
        }
    }
}
