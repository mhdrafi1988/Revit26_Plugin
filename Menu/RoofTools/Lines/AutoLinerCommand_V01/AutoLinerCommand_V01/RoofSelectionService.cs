using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;

namespace Revit26_Plugin.AutoLiner_V01.Services
{
    public class RoofSelectionService
    {
        public RoofBase PickRoof(UIApplication uiApp)
        {
            if (uiApp == null)
                throw new ArgumentNullException(nameof(uiApp));

            UIDocument uidoc = uiApp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                Reference r = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    new RoofSelectionFilter(),
                    "Select a roof");

                if (r == null)
                    return null;

                Element e = doc.GetElement(r);

                if (e is RoofBase roof)
                    return roof;

                throw new InvalidOperationException("Selected element is not a roof.");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // user pressed ESC
                return null;
            }
        }
    }
}
