using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Revit26_Plugin.AutoLiner_V01.ViewModels;
using Revit26_Plugin.AutoLiner_V01.Views;

namespace Revit26_Plugin.AutoLiner_V01.Commands
{
    public class AutoLinerCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;

            // You must obtain or select the RoofBase instance here.
            // For example, you could prompt the user to select a roof, or get it from the active selection.
            RoofBase selectedRoof = null; // Replace with actual logic to obtain a RoofBase instance.

            AutoLinerViewModel vm = new AutoLinerViewModel(uiApp, selectedRoof);
            var win = new AutoLinerWindow(vm);

            win.Owner = uiApp.MainWindowHandle != null
                ? System.Windows.Interop.HwndSource
                    .FromHwnd(uiApp.MainWindowHandle)?.RootVisual as System.Windows.Window
                : null;

            win.Show();

            return Result.Succeeded;
        }
    }
}
