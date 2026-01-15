using Autodesk.Revit.DB;
using Revit26_Plugin.CreaserAdv_V002.Models;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    public class DetailItemPlacementOrchestrator
    {
        public void Execute(
            Document doc,
            ViewPlan view,
            SimplePipelineResult result,
            FamilySymbol symbol,
            LoggingService log)
        {
            if (result == null || result.CreaseLines == null || result.CreaseLines.Count == 0)
            {
                log.Warning("No crease lines found for placement.");
                return;
            }

            var placer =
                new DetailItemPlacementService(doc, view);

            placer.PlaceAlongLines(
                result.CreaseLines,
                symbol,
                log);
        }
    }
}
