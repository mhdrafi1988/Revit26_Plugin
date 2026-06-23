using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;

namespace Revit26_Plugin.RoofFromFloor.Services
{
    public static class LinkSelectionService
    {
        public static RevitLinkInstance PickLinkInstance(UIApplication uiApp)
        {
            UIDocument uidoc = uiApp.ActiveUIDocument;
            Document doc = uidoc.Document;

            Reference reference = uidoc.Selection.PickObject(
                ObjectType.Element,
                new LinkSelectionFilter(),
                "Select a linked Revit model");

            if (reference == null)
                return null;

            RevitLinkInstance link =
                doc.GetElement(reference) as RevitLinkInstance;

            if (link == null)
                throw new System.InvalidOperationException(
                    "Selected element is not a RevitLinkInstance.");

            return link;
        }

        private class LinkSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                return elem is RevitLinkInstance;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return true;
            }
        }
    }
}
