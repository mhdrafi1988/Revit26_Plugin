using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Windows.Interop;
using Revit26_Plugin.CalloutCOP.V015.Views;

namespace Revit26_Plugin.CalloutCOP.V015.Commands
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
            new WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;
            window.Show();
            return Result.Succeeded;
        }
    }
}
