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
    public class LaunchCreaserCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc?.Document;
            View view = doc?.ActiveView;

            if (doc == null || view == null || view.ViewType != ViewType.FloorPlan)
            {
                message = "Run from a Floor Plan view.";
                return Result.Failed;
            }

            RoofBase roof = RoofSelectionService.PickRoof(uiDoc);
            if (roof == null)
                return Result.Cancelled;

            CreaserViewModel vm = new CreaserViewModel(uiDoc, roof);

            CreaserWindow window = new CreaserWindow
            {
                DataContext = vm
            };

            RevitWindowHelper.SetOwner(window, uiApp);
            window.Show(); // modeless

            return Result.Succeeded;
        }
    }
}
