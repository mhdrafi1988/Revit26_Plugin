using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.RoofDrainCalloutPlacing.V001.ViewModels;
using Revit26_Plugin.RoofDrainCalloutPlacing.V001.Views;

namespace Revit26_Plugin.RoofDrainCalloutPlacing.V001.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RoofDrainCalloutPlacingCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;

            // ViewModel constructor calls ExternalEvent.Create() — must happen here,
            // inside the valid API execution context, not lazily from UI interaction.
            var viewModel = new RoofDrainCalloutPlacingViewModel(uiApp);
            var window = new RoofDrainCalloutPlacingWindow(viewModel);

            new WindowInteropHelper(window).Owner = uiApp.MainWindowHandle;

            // Show(), not ShowDialog() — required because IExternalEventHandler needs
            // the Revit message loop to keep pumping while this window is open.
            window.Show();

            return Result.Succeeded;
        }
    }
}
