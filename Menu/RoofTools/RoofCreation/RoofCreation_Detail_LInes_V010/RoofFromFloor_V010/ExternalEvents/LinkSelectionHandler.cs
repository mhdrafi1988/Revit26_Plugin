using Autodesk.Revit.UI;
using Revit26_Plugin.RoofFromFloor.V010.Services;
using Revit26_Plugin.RoofFromFloor.V010.ViewModels;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoofFromFloor.V010.ExternalEvents
{
    public class LinkSelectionHandler : IExternalEventHandler
    {
        public RoofFromFloorViewModel ViewModel { get; set; }

        public void Execute(UIApplication app)
        {
            try
            {
                var link = LinkSelectionService.PickLinkInstance(app);
                if (link != null)
                    ViewModel.SetSelectedLink(link);
                else
                    ViewModel.ShowWindow();
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                ViewModel.LogFromExternal("Link selection cancelled.", LogLevel.Warning);
                ViewModel.ShowWindow();
            }
            catch (System.Exception ex)
            {
                ViewModel.LogFromExternal($"Link selection error: {ex.Message}", LogLevel.Error);
                ViewModel.ShowWindow();
            }
        }

        public string GetName() => "RoofFromFloor · Link Selection";
    }
}
