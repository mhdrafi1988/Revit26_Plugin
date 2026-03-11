using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;

namespace Revit26_Plugin.RoofFromFloor.Services
{
    public static class RoofSelectionService
    {
        public static FootPrintRoof PickFootprintRoof(UIApplication uiApp)
        {
            UIDocument uidoc = uiApp.ActiveUIDocument;
            Document doc = uidoc.Document;

            if (doc.ActiveView is not ViewPlan)
                throw new System.InvalidOperationException(
                    "Active view is not a Plan View.");

            Reference reference = uidoc.Selection.PickObject(
                ObjectType.Element,
                new RoofSelectionFilter(),
                "Select a footprint roof");

            if (reference == null)
                return null;

            RoofBase roof = doc.GetElement(reference) as RoofBase;

            if (roof is not FootPrintRoof fpRoof)
                throw new System.InvalidOperationException(
                    "Selected roof is not a FootPrintRoof.");

            return fpRoof;
        }

        private class RoofSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                return elem is RoofBase;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return true;
            }
        }
    }
}
