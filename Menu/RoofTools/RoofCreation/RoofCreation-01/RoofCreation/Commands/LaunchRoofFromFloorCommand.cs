using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Revit26_Plugin.RoofFromFloor.V005.Views;

namespace Revit26_Plugin.RoofFromFloor.V005.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class LaunchRoofFromFloorCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            Autodesk.Revit.DB.ElementSet elements)
        {
            var window = new RoofFromFloorWindow(commandData.Application);
            window.ShowDialog();
            return Result.Succeeded;
        }
    }
}
