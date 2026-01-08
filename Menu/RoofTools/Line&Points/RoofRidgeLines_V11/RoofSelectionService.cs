using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V11.Services
{
    public static class RoofSelectionService
    {
        public static RoofBase SelectRoof(UIDocument uidoc)
        {
            var r = uidoc.Selection.PickObject(
                ObjectType.Element, new RoofFilter(), "Select roof");
            return uidoc.Document.GetElement(r) as RoofBase;
        }

        private class RoofFilter : ISelectionFilter
        {
            public bool AllowElement(Element e) => e is RoofBase;
            public bool AllowReference(Reference r, XYZ p) => true;
        }
    }
}
