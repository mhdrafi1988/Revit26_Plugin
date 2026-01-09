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
            DockablePane pane = commandData.Application
                .GetDockablePane(DockablePaneIds.SectionManagerPaneId);

            pane.Show();
            return Result.Succeeded;
        }
    }
}
