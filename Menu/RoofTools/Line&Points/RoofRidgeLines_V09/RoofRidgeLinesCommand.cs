using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V08.Services;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V08.Services;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V08.ViewModels;
using System;
using System.Windows;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V09
{
    [Transaction(TransactionMode.Manual)]
    public class RoofRidgeLinesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                if (uidoc == null)
                {
                    TaskDialog.Show("Error", "No active document.");
                    return Result.Failed;
                }

                // Create and show main window
                var vm = new MainViewModel();
                var window = new Window1
                {
                    DataContext = vm,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                // Wire up events
                vm.RequestStart += () =>
                {
                    window.Close();
                    ProcessRoof(uidoc);
                };
                vm.RequestClose += () => window.Close();

                window.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                LoggerService.LogException(ex, "RoofRidgeLinesCommand.Execute");
                return Result.Failed;
            }
        }

        private void ProcessRoof(UIDocument uidoc)
        {
            try
            {
                var wizard = new WizardService(uidoc);
                var result = wizard.ExecuteRoofProcessing();

                // Show simple summary dialog
                string summary = $"Processing Complete!\n\n" +
                               $"• Status: {(result.IsSuccess ? "Success" : "Failed")}\n" +
                               $"• Main Lines: {result.DetailLinesCreated}\n" +
                               $"• Perpendicular Lines: {result.PerpendicularLinesCreated}\n" +
                               $"• Shape Points: {result.ShapePointsAdded}\n" +
                               $"• Duration: {result.Duration:mm\\:ss}";

                if (result.IsSuccess)
                {
                    TaskDialog.Show("Success", summary);
                }
                else
                {
                    TaskDialog.Show("Completed with Issues", summary);
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogException(ex, "ProcessRoof");
                TaskDialog.Show("Error", $"Processing failed:\n{ex.Message}");
            }
        }
    }
}