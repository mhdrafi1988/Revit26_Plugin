using Autodesk.Revit.UI;
using Revit26_Plugin.RoofFromFloor.V02.Services;
using Revit26_Plugin.RoofFromFloor.V02.ViewModels;

namespace Revit26_Plugin.RoofFromFloor.V02.ExternalEvents
{
    public class RoofSelectionHandler : IExternalEventHandler
    {
        public RoofFromFloorViewModel ViewModel { get; set; }

        public void Execute(UIApplication app)
        {
            try
            {
                var roof = RoofSelectionService.PickFootprintRoof(app);
                if (roof != null)
                    ViewModel.SetSelectedRoof(roof);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                ViewModel.LogFromExternal("Roof selection cancelled.");
                ViewModel.ShowWindow();
            }
            catch (System.Exception ex)
            {
                ViewModel.LogFromExternal(ex.Message);
                ViewModel.ShowWindow();
            }
        }

        public string GetName() => "Roof Selection Handler";
    }
}
