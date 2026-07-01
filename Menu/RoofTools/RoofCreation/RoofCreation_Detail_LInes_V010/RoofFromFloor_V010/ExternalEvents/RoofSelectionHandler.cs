using Autodesk.Revit.UI;
using Revit26_Plugin.RoofFromFloor.V010.Services;
using Revit26_Plugin.RoofFromFloor.V010.ViewModels;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoofFromFloor.V010.ExternalEvents
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
                else
                    ViewModel.ShowWindow();
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                ViewModel.LogFromExternal("Roof selection cancelled.", LogLevel.Warning);
                ViewModel.ShowWindow();
            }
            catch (System.Exception ex)
            {
                ViewModel.LogFromExternal($"Roof selection error: {ex.Message}", LogLevel.Error);
                ViewModel.ShowWindow();
            }
        }

        public string GetName() => "RoofFromFloor · Roof Selection";
    }
}
