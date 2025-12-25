using System.Collections.ObjectModel;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.Creaser_V100.Models;
using Revit26_Plugin.Creaser_V100.Services;
using Revit26_Plugin.Creaser_V100.ViewModels;
using Revit26_Plugin.Creaser_V100.Views;

namespace Revit26_Plugin.Creaser_V100.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class RoofCreaserCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;

            if (uiDoc?.Document?.ActiveView is not ViewPlan)
            {
                TaskDialog.Show("Creaser", "Run from a Plan View.");
                return Result.Cancelled;
            }

            // ------------------------------------------------------------
            // 1. Select roof FIRST (CRITICAL)
            // ------------------------------------------------------------
            var tempLog =
                new UiLogService(
                    new ObservableCollection<LogEntry>());

            var selectionService =
                new SelectionService(uiDoc, tempLog);

            RoofBase roof = selectionService.PickSingleRoof();
            if (roof == null)
                return Result.Cancelled;

            // ------------------------------------------------------------
            // 2. Launch UI AFTER selection
            // ------------------------------------------------------------
            var viewModel =
                new RoofCreaserViewModel(uiApp, roof);

            var window =
                new RoofCreaserWindow
                {
                    DataContext = viewModel
                };

            window.ShowDialog();
            return Result.Succeeded;
        }
    }
}
