// ==============================================
// File: DwgToLinesCommand.cs
// Layer: Commands
// Namespace: Revit26_Plugin.DwgToLines.V005.Commands
// ==============================================

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.DwgToLines.V005.Infrastructure.Helpers;
using Revit26_Plugin.DwgToLines.V005.UI.Views;

namespace Revit26_Plugin.DwgToLines.V005.Commands
{
    /// <summary>
    /// Entry point for the DWG to Lines tool.
    /// Enforces Family Editor context and launches the UI.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class DwgToLinesCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                return ExecuteInternal(commandData, ref message, elements);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("DWG to Lines", $"An unexpected error occurred: {ex.Message}");
                return Result.Failed;
            }
        }

        private Result ExecuteInternal(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;

            if (!RevitContextValidator.IsFamilyEditor(uiApp, out message))
            {
                TaskDialog.Show("DWG to Lines", message);
                return Result.Cancelled;
            }

            var view = new DwgToLinesWindow(uiApp);
            view.ShowDialog();

            return Result.Succeeded;
        }
    }
}
