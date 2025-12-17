using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.AutoLiner_V02.ExternalEvents;
using Revit26_Plugin.AutoLiner_V02.ViewModels;
using Revit26_Plugin.AutoLiner_V02.Views;

namespace Revit26_Plugin.AutoLiner_V02.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class AutoLinerCommand_V02 : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc == null) return Result.Cancelled;

            Document doc = uiDoc.Document;

            if (doc.ActiveView is not ViewPlan)
            {
                TaskDialog.Show("AutoLiner", "Run from a Plan View only.");
                return Result.Cancelled;
            }

            Reference roofRef;
            try
            {
                roofRef = uiDoc.Selection.PickObject(
                    ObjectType.Element,
                    new RoofSelectionFilter(),
                    "Select a Roof");
            }
            catch
            {
                return Result.Cancelled;
            }

            Element roof = doc.GetElement(roofRef);
            if (roof == null) return Result.Cancelled;

            // ✅ ExternalEvent setup
            var handler = new AutoLinerExternalEventHandler();
            ExternalEvent externalEvent = ExternalEvent.Create(handler);

            var vm = new MainViewModel(
                doc,
                roof,
                handler,
                externalEvent);

            var window = new AutoLinerWindow(vm, commandData.Application);
            window.Show();

            return Result.Succeeded;
        }
    }

    internal class RoofSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) =>
            elem.Category?.Id.Value == (int)BuiltInCategory.OST_Roofs;

        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
