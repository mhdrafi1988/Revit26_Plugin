using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.DwgSymbolicConverter_V02.Services
{
    public static class CadManualPickService
    {
        public static ImportInstance Pick(UIDocument uiDoc)
        {
            Reference r = uiDoc.Selection.PickObject(
                ObjectType.Element,
                new ImportInstanceFilter(),
                "Select a DWG Import");

            return uiDoc.Document.GetElement(r) as ImportInstance;
        }

        private class ImportInstanceFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
                => elem is ImportInstance;

            public bool AllowReference(Reference reference, XYZ position)
                => false;
        }
    }
}
