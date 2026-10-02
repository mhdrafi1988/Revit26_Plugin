using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.RoofTypeCreator.V001.UI.ViewModels;
using Revit26_Plugin.RoofTypeCreator.V001.UI.Views;
using Revit26_Plugin.Shared.Services;

namespace Revit26_Plugin.RoofTypeCreator.V001.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RoofTypeManagerCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
            => ToolGuard.RunCommand(GetType(), commandData, ref message, elements, ExecuteUnguarded);

        private Result ExecuteUnguarded(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp  = commandData.Application;
            var vm     = new RoofTypeManagerViewModel(uiApp);
            var window = new RoofTypeManagerWindow { DataContext = vm };

            new WindowInteropHelper(window).Owner = uiApp.MainWindowHandle;
            window.Show();

            return Result.Succeeded;
        }
    }
}
