using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.RoofFromFloor.ViewModels;

namespace Revit26_Plugin.RoofFromFloor.ExternalEvents
{
    public class RoofSelectionHandler : IExternalEventHandler
    {
        public RoofFromFloorViewModel ViewModel { get; set; }

        public void Execute(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                Reference r = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    new RoofFilter(),
                    "Select a roof");

                if (r == null) return;

                RoofBase roof = doc.GetElement(r) as RoofBase;
                if (roof != null)
                    ViewModel.SetSelectedRoof(roof);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                ViewModel.LogFromExternal("Roof selection cancelled.");
                ViewModel.ShowWindow();   // ?? CRITICAL
            }
        }

        public string GetName() => "Roof Selection Handler";
    }

    internal class RoofFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is RoofBase;
        public bool AllowReference(Reference reference, XYZ position) => true;
    }
}
