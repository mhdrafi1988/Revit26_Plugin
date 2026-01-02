using Autodesk.Revit.DB;
using Revit26_Plugin.CreaserAdv_V002.Models;
using Revit26_Plugin.Menu.RoofTools.Lines.SlopeToDetailItem.CreasToDetalLines_002.DeatailItems;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    public class DetailItemPlacementOrchestrator
    {
        public void Execute(
            Document doc,
            ViewPlan view,
            PipelineResult result,
            FamilySymbol symbol,
            LoggingService log)
        {
            if (result == null ||
                result.Corners == null || result.Corners.Count == 0 ||
                result.Drains == null || result.Drains.Count == 0 ||
                result.AllEdges == null || result.AllEdges.Count == 0)
            {
                log.Warning("Insufficient data for placement.");
                return;
            }

            var augmentor = new RoutingGraphAugmentationService();

            var augmentedEdges =
                augmentor.InsertNodes(
                    result.AllEdges,
                    result.Corners.Concat(result.Drains).ToList());

            var graph =
                new RoofPathGraphBuilderService()
                    .BuildGraph(augmentedEdges);

            var pathService = new ShortestPathService();
            var elevation = view.GenLevel.Elevation;

            var routedLines = new List<Line>();

            foreach (var corner in result.Corners)
            {
                var path =
                    pathService.FindShortestPath(
                        corner,
                        result.Drains,
                        graph);

                if (path == null || path.OrderedNodes == null || path.OrderedNodes.Count < 2)
                    continue;

                for (int i = 0; i < path.OrderedNodes.Count - 1; i++)
                {
                    XYZ p1 = path.OrderedNodes[i];
                    XYZ p2 = path.OrderedNodes[i + 1];

                    XYZ a = new XYZ(p1.X, p1.Y, elevation);
                    XYZ b = new XYZ(p2.X, p2.Y, elevation);

                    if (a.DistanceTo(b) > GeometryTolerance.Point)
                        routedLines.Add(Line.CreateBound(a, b));
                }
            }

            if (routedLines.Count == 0)
            {
                log.Warning("No valid routing lines generated.");
                return;
            }

            var cleanedLines =
                new PathCleanupService()
                    .Clean(routedLines);

            if (cleanedLines.Count == 0)
            {
                log.Warning("All routing lines removed during cleanup.");
                return;
            }

            var placer =
                new DetailItemPlacementService(doc, view);

            placer.PlaceAlongLines(cleanedLines, symbol, log);

            log.Info($"Placed {cleanedLines.Count} routed detail lines.");
        }
    }
}
