using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoLiner_V01.Services;
using Revit26_Plugin.AutoLiner_V01.ViewModels;
using Revit26_Plugin.AutoLiner_V01.Views;

namespace Revit26_Plugin.AutoLiner_V01.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class AutoLinerCommand_V01 : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;

            // 🔒 Validate view first
            if (!ViewValidationService.IsValidPlanView(
                uiDoc.ActiveView, out string reason))
            {
                TaskDialog.Show("AutoLiner", reason);
                return Result.Cancelled;
            }

            // 🔥 Pick roof BEFORE UI
            var roofService = new RoofSelectionService();
            RoofBase roof = roofService.PickRoof(uiApp);

            if (roof == null)
                return Result.Cancelled;

            // 🚀 Launch UI with pre-selected roof
            var vm = new AutoLinerViewModel(uiApp, roof);
            var win = new AutoLinerWindow(vm);

            win.Show();
            return Result.Succeeded;
        }
    }
}

