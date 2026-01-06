using Autodesk.Revit.DB;
using Revit26_Plugin.CreaserAdv_V003.Models;
using System.Linq;

namespace Revit26_Plugin.CreaserAdv_V003.Services
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
            var elevation = view.GenLevel.Elevation;

            var lines =
                result.Edges
                    .Select(e => e.ToLine(elevation))
                    .ToList();

            new DetailItemPlacementService(doc, view)
                .Place(lines, symbol, log);
        }
    }
}
