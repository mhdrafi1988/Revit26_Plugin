using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Revit26_Plugin.SectionManager.V008.Docking;

namespace Revit26_Plugin.SectionManager.V008.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class OpenSectionManagerCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            Autodesk.Revit.DB.ElementSet elements)
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
                TaskDialog.Show("Section Manager", $"An unexpected error occurred: {ex.Message}");
                return Result.Failed;
            }
        }

        private Result ExecuteInternal(
            ExternalCommandData commandData,
            ref string message,
            Autodesk.Revit.DB.ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;

            var pane = uiApp.GetDockablePane(
                DockablePaneIds.SectionManagerPaneId);

            // ?? THIS IS THE MISSING STEP
            var provider = SectionManagerDockablePane.Instance;
            provider.Initialize(uiApp);

            pane.Show();
            return Result.Succeeded;
        }
    }
}
