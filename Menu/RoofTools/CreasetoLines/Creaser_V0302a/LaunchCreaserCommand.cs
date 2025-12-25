using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.Creaser_V03_03.Helpers;
using Revit26_Plugin.Creaser_V03_03.Services;
using Revit26_Plugin.Creaser_V03_03.ViewModels;
using Revit26_Plugin.Creaser_V03_03.Views;

namespace Revit26_Plugin.Creaser_V03_03.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class LaunchCreaserCommand03 : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc?.Document;
            View activeView = doc?.ActiveView;

            // ===================== VALIDATION =====================
            if (doc == null || activeView == null)
            {
                message = "No active document or view.";
                return Result.Failed;
            }

            if (activeView.ViewType != ViewType.FloorPlan)
            {
                message = "Please run this command from a Floor Plan view.";
                return Result.Failed;
            }

            // ===================== ROOF PICK =====================
            RoofBase roof = RoofSelectionService.PickRoof(uiDoc);
            if (roof == null)
            {
                // User cancelled selection → no error
                return Result.Cancelled;
            }

            // ===================== CREATE VIEWMODEL =====================
            CreaserViewModel viewModel;
            try
            {
                viewModel = new CreaserViewModel(uiDoc, roof);
            }
            catch
            {
                message = "Failed to initialize Creaser ViewModel.";
                return Result.Failed;
            }

            // ===================== CREATE WINDOW =====================
            CreaserWindow window = new CreaserWindow
            {
                DataContext = viewModel
            };

            // 🔴 CRITICAL: set Revit as owner or window may not appear
            RevitWindowHelper.SetOwner(window, uiApp);

            // ===================== SHOW UI =====================
            // Use Show() (modeless), NOT ShowDialog()
            window.Show();

            return Result.Succeeded;
        }
    }
}
