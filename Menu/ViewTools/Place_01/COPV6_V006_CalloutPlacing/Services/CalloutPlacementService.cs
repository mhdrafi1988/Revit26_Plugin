using Autodesk.Revit.DB;
using System.Collections.ObjectModel;

namespace Revit26_Plugin.CalloutCOP_V06.Services
{
    public static class CalloutPlacementService
    {
        public static void PlaceCallout(
            Document doc,
            View sourceView,
            double size,
            ObservableCollection<string> logs)
        {
            var half = size / 2;

            var point1 = new XYZ(-half, -half, 0);
            var point2 = new XYZ(half, half, 0);

            try
            {
                ViewSection.CreateCallout(
                    doc,
                    sourceView.Id,
                    sourceView.GetTypeId(),
                    point1,
                    point2);

                logs.Add($"? Callout placed in {sourceView.Name}");
            }
            catch (System.Exception ex)
            {
                logs.Add($"? Failed in {sourceView.Name}: {ex.Message}");
            }
        }
    }
}
