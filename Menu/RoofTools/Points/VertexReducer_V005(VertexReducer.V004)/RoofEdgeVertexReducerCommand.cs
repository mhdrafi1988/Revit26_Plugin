using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.VertexReducer.V005.ViewModels;
using Revit26_Plugin.VertexReducer.V005.Views;

namespace Revit26_Plugin.VertexReducer.V005.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RoofEdgeVertexReducerCommand : IExternalCommand
    {
        private static MainWindow _window;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (_window != null && _window.IsLoaded)
            {
                _window.Activate();
                return Result.Succeeded;
            }

            var viewModel = new MainViewModel();
            var handler = new RoofEdgeVertexReducerEventHandler(viewModel);
            var externalEvent = ExternalEvent.Create(handler);
            viewModel.Initialize(handler, externalEvent);

            _window = new MainWindow(viewModel);
            _window.Closed += (s, e) => _window = null;

            // Owner must be set before Show() — this is what keeps the tool window
            // above Revit's main window after PickObject() returns focus to Revit.
            new WindowInteropHelper(_window).Owner = commandData.Application.MainWindowHandle;

            _window.Show();

            return Result.Succeeded;
        }
    }
}
