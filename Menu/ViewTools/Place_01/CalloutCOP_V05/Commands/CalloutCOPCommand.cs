using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.CalloutCOP_V06.Views;

namespace Revit26_Plugin.CalloutCOP_V06.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CalloutCOPCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            var window = new CalloutCOPWindow(commandData);
            window.Show();
            return Result.Succeeded;
        }
    }
}
