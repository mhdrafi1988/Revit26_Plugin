using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26.RoofTagV42.Services;
using Revit26.RoofTagV42.ViewModels;
using Revit26.RoofTagV42.Views;
using System;
using System.ComponentModel.Design;
using System.Linq;
using System.Windows;

namespace Revit26.RoofTagV42.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RoofTagCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                UIDocument uiDoc = uiApp.ActiveUIDocument;
                Document doc = uiDoc.Document;

                // Validate active view
                View currentView = doc.ActiveView;
                if (!IsValidPlanView(currentView))
                {
                    TaskDialog.Show("Invalid View",
                        "Please run this command in a plan view (Floor Plan or Reflected Ceiling Plan).\n" +
                        $"Current view: {currentView.ViewType}");
                    return Result.Cancelled;
                }

                // Select roof element
                TaskDialog.Show("Roof Selection",
                    "Please select a roof element to tag.\nClick OK to continue.");

                RoofBase selectedRoof = SelectionService.SelectRoof(uiDoc);
                if (selectedRoof == null)
                {
                    TaskDialog.Show("Selection Cancelled", "No roof selected. Command cancelled.");
                    return Result.Cancelled;
                }

                // Create and show UI window
                var window = new RoofTagWindow(uiApp, selectedRoof);
                window.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Error", $"Failed to execute command:\n{ex.Message}");
                return Result.Failed;
            }
        }

        private bool IsValidPlanView(View view)
        {
            if (view == null) return false;

            return view.ViewType == ViewType.FloorPlan ||
                   view.ViewType == ViewType.CeilingPlan ||
                   view.ViewType == ViewType.EngineeringPlan ||
                   view.ViewType == ViewType.Detail;
        }
    }
}