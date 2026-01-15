using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.CreaserAdv_V003.Models;

namespace Revit26_Plugin.CreaserAdv_V003.Services
{
    public class RoofGeometryPipelineService
    {
        private readonly LoggingService _log;

        public RoofGeometryPipelineService(LoggingService log)
        {
            _log = log;
        }

        public SimplePipelineResult Execute(UIDocument uiDoc, Element roof)
        {
            _log.Info("Pipeline started.");

            var solid = new RoofSolidExtractionService().ExtractSolid(roof);
            var faces = new RoofTopFaceService().GetTopFaces(solid);
            var edges = new RoofEdgeExtractionService().CollectEdges(faces);
            var flat = new RoofEdgeFlatteningService().Flatten(edges);

            _log.Info($"Edges collected: {flat.Count}");

            return new SimplePipelineResult { Edges = flat };
        }
    }
}
