using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace Revit26.RoofTagV42.Services
{
    public static class SelectionService
    {
        public static RoofBase SelectRoof(UIDocument uiDocument)
        {
            try
            {
                Reference reference = uiDocument.Selection.PickObject(
                    ObjectType.Element,
                    new RoofSelectionFilter(),
                    "Select a roof element");

                return uiDocument.Document.GetElement(reference) as RoofBase;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return null;
            }
        }

        private class RoofSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element element) => element is RoofBase;

            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}