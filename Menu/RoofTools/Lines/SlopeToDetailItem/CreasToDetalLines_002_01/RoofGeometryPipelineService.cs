using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.CreaserAdv_V002.Models;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    public class RoofGeometryPipelineService
    {
        private readonly LoggingService _log;

        public RoofGeometryPipelineService(LoggingService log)
        {
            _log = log;
        }

        public SimplePipelineResult Execute(
            UIDocument uiDoc,
            Element roof)
        {
            _log.Info("Simple roof geometry pipeline started.");

            var solid =
                new RoofSolidExtractionService()
                    .ExtractSolid(roof);

            var topFaces =
                new RoofTopFaceService()
                    .GetTopFaces(solid);

            _log.Info($"Top faces found: {topFaces.Count}");

            var drains =
                new DrainDetectionService()
                    .DetectDrains(topFaces, _log);

            var edges3D =
                new RoofEdgeExtractionService()
                    .CollectEdges(topFaces);

            var flattenedEdges =
                new RoofEdgeFlatteningService()
                    .Flatten(edges3D);

            var creaseLines =
                flattenedEdges
                    .Where(e => e.IsCrease)
                    .Select(e => e.ToLine2D())
                    .ToList();

            _log.Info($"Crease lines found: {creaseLines.Count}");

            var boundaryLines =
                flattenedEdges
                    .Where(e => !e.IsCrease)
                    .Select(e => e.ToLine2D())
                    .ToList();

            var corners =
                new CornerDetectionService()
                    .DetectCornersFromEdges(boundaryLines, _log);

            return new SimplePipelineResult
            {
                Corners = corners,
                Drains = drains,
                CreaseLines = creaseLines
            };
        }
    }
}
