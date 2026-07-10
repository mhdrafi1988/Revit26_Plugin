using Autodesk.Revit.UI;
using Revit26_Plugin.RoomToRoofOrFloor.V001.UI.ViewModels;

namespace Revit26_Plugin.RoomToRoofOrFloor.V001.Infrastructure.ExternalEvents
{
    /// <summary>
    /// Runs the room-processing loop on the valid Revit API thread.
    /// Raised from MainViewModel.Run(); never call Revit API from the
    /// UI thread directly.
    /// </summary>
    public class RunExternalEventHandler : IExternalEventHandler
    {
        private readonly MainViewModel _viewModel;

        public RunExternalEventHandler(MainViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public void Execute(UIApplication app)
        {
            _viewModel.RunOnRevitThread();
        }

        public string GetName() => "RoomToRoofOrFloor.V001.Run";
    }
}
