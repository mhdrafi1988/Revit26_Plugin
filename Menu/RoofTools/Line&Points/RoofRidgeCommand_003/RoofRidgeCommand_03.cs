using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit22_Plugin.RRLPV3.Models;
using Revit22_Plugin.RRLPV3.Services;
using Revit22_Plugin.RRLPV3.Utils;
using Revit22_Plugin.RRLPV3.ViewModels;
using Revit22_Plugin.RRLPV3.Views;
using Revit22_Plugin.RRLPV3.Services;
using System;

namespace Revit22_Plugin.RRLPV3.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class RoofRidgeCommand_03 : IExternalCommand
    {
        public Result Execute(ExternalCommandData c, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = c.Application.ActiveUIDocument;
                if (uidoc == null)
                {
                    TaskDialog.Show("Error", "No active document.");
                    return Result.Failed;
                }

                // ----------------------------
                // 1) Create ViewModel + Window
                // ----------------------------

                var vm = new MainViewModel();
                var mainWindow = new MainWindow
                {
                    DataContext = vm
                };

                // ----------------------------
                // 2) Hook Start + Close events
                // ----------------------------

                vm.RequestStart += () =>
                {
                    mainWindow.Close();       // Close Start Window
                    RunProcessing(uidoc, vm); // Then run Revit API
                };

                vm.RequestClose += () =>
                {
                    mainWindow.Close();
                };

                // ----------------------------
                // 3) Show Start Window
                // ----------------------------

                mainWindow.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                Logger.LogException(ex, "RoofRidgeCommand_03.Execute");
                return Result.Failed;
            }
        }

        // ----------------------------------------------------
        // After Start Window closes, we process the roof safely
        // ----------------------------------------------------

        private void RunProcessing(UIDocument uidoc, MainViewModel vm)
        {
            try
            {
                vm.StatusMessage = "Processing...";

                RoofData data = RoofService.ExecuteRoofProcessing(uidoc, (status) =>
                {
                    vm.StatusMessage = status;
                });

                // ----------------------------
                // Show Summary Window
                // ----------------------------

                var summaryVM = new SummaryViewModel(data);
                var summaryWindow = new SummaryWindow
                {
                    DataContext = summaryVM
                };

                summaryWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "RunProcessing");
                TaskDialog.Show("Error", $"Processing failed:\n{ex.Message}");
            }
        }
    }
}
