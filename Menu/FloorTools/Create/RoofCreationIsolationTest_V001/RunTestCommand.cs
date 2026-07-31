using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.RoofCreationIsolationTest.V001.UI.ViewModels;
using Revit26_Plugin.RoofCreationIsolationTest.V001.UI.Views;
using System.Windows.Interop;

namespace Revit26_Plugin.RoofCreationIsolationTest.V001.Commands
{
    /// <summary>
    /// Entry point for the Roof Creation Isolation Test tool. Standalone diagnostic
    /// command per Rafi's request — isolates NewFootPrintRoof() failures away from
    /// FloorsAndRoofsFromLinkedRoomsViaPlanView's more complex call context.
    /// Opens one modeless window; the ViewModel owns the ExternalEvent/handler pair.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RunTestCommand : IExternalCommand
    {
        // Kept alive for the lifetime of the Revit session so the window can be
        // reopened without re-registering the ExternalEvent; matches the modeless
        // single-instance pattern used by other tools in this suite.
        private static RunTestWindow? _window;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (_window != null && _window.IsVisible)
            {
                _window.Activate();
                return Result.Succeeded;
            }

            var viewModel = new RunTestViewModel();
            _window = new RunTestWindow(viewModel);

            // WindowInteropHelper is the correct owner-assignment pattern in Revit's
            // process — Application.Current.MainWindow is unreliable here, per
            // project convention.
            new WindowInteropHelper(_window).Owner = commandData.Application.MainWindowHandle;

            _window.Show();

            return Result.Succeeded;
        }
    }
}
