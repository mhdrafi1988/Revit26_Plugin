using Autodesk.Revit.UI;
using Revit26_Plugin.RoofFromFloor.Services;
using Revit26_Plugin.RoofFromFloor.ViewModels;

namespace Revit26_Plugin.RoofFromFloor.ExternalEvents
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
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                ViewModel.LogFromExternal("Link selection cancelled.");
                ViewModel.ShowWindow();
            }
            catch (System.Exception ex)
            {
                ViewModel.LogFromExternal(ex.Message);
                ViewModel.ShowWindow();
            }
        }

        public string GetName() => "Link Selection Handler";
    }
}
