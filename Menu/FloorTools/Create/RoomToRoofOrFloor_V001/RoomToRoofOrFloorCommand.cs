using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.RoomToRoofOrFloor.V001.UI.ViewModels;
using Revit26_Plugin.RoomToRoofOrFloor.V001.UI.Views;

namespace Revit26_Plugin.RoomToRoofOrFloor.V001.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RoomToRoofOrFloor : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiDoc = commandData.Application.ActiveUIDocument;

            var viewModel = new MainViewModel(uiDoc);
            var window = new MainWindow(viewModel);

            new WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;

            window.Show(); // modeless — window stays open after the run completes

            return Result.Succeeded;
        }
    }
}
