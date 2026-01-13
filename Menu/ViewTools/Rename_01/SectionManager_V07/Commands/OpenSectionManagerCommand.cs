using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Revit26_Plugin.SectionManager_V07.Docking;

namespace Revit26_Plugin.SectionManager_V07.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class OpenSectionManagerCommand : IExternalCommand
    {
        public Result Execute(
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
