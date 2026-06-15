using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
//using Revit26_Plugin.Asd.ViewModels;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V11.Services;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V11.Utils;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V11.ViewModels;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V11.Views;
using System;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V11.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class RoofRidgeCommand_V11 : IExternalCommand
    {
        public Result Execute(ExternalCommandData c, ref string message, ElementSet elements)
        {
            UIDocument uidoc = c.Application.ActiveUIDocument;
            if (uidoc == null)
            {
                TaskDialog.Show("Error", "No active document.");
                return Result.Failed;
            }

            var vm = new MainViewModel();
            var win = new MainWindow { DataContext = vm };

            vm.RequestStart += () =>
            {
                win.Close();
                Run(uidoc, vm);
            };

            vm.RequestClose += () => win.Close();

            win.ShowDialog();
            return Result.Succeeded;
        }

        private void Run(UIDocument uidoc, MainViewModel vm)
        {
            try
            {
                var data = RoofService.Execute(uidoc, s => vm.StatusMessage = s);
                var summaryVM = new SummaryViewModel(data);
                new SummaryWindow { DataContext = summaryVM }.ShowDialog();
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Command Run");
                TaskDialog.Show("Error", ex.Message);
            }
        }
    }
}
