using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.Creaser_adv_V001.Views;
using Revit26_Plugin.Creaser_adv_V001.ViewModels;

namespace Revit26_Plugin.Creaser_adv_V001.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class RunCreaserAdvCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc?.Document;

            if (doc == null)
                return Result.Cancelled;

            // 1️⃣ Validate plan view
            if (doc.ActiveView is not ViewPlan)
            {
                TaskDialog.Show("Creaser Advanced", "Run from a plan view.");
                return Result.Cancelled;
            }

            // 2️⃣ Select roof FIRST (Revit-safe)
            Reference roofRef;
            try
            {
                roofRef = uiDoc.Selection.PickObject(
                    ObjectType.Element,
                    new RoofSelectionFilter(),
                    "Select a footprint roof");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            if (roofRef == null)
                return Result.Cancelled;

            if (doc.GetElement(roofRef) is not FootPrintRoof roof)
            {
                TaskDialog.Show("Creaser Advanced", "Invalid roof selection.");
                return Result.Failed;
            }

            // 3️⃣ Launch UI AFTER selection
            var vm = new CreaserAdvViewModel(uiApp, roof);
            var window = new CreaserAdvWindow(vm)
            {
                DataContext = vm,
                Topmost = true
            };

            window.ShowDialog();
            return Result.Succeeded;
        }
    }

    /// <summary>
    /// Roof-only selection filter
    /// </summary>
    public class RoofSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is FootPrintRoof;
        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
