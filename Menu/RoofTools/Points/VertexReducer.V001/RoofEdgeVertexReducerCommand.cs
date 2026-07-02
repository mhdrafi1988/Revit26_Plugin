using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.RoofEdgeVertexReducer.V001.ViewModels;
using Revit26_Plugin.RoofEdgeVertexReducer.V001.Views;

namespace Revit26_Plugin.RoofEdgeVertexReducer.V001.Commands
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
            _window.Show();

            return Result.Succeeded;
        }
    }
}
