// RoofRidgeCommand_V12.cs
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V12.Views;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V12.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class RoofRidgeCommand_V12 : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc?.Document == null)
            {
                TaskDialog.Show("Roof Ridge Lines", "No active document.");
                return Result.Failed;
            }

            var window = new MainWindow(uiDoc);
            window.Show();

            return Result.Succeeded;
        }
    }
}
