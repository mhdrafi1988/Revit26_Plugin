using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    public class RoofGeometryPipelineService
    {
        private readonly LoggingService _log;

        public RoofGeometryPipelineService(LoggingService log)
        {
            _log = log;
        }

        public PipelineResult Execute(
            UIDocument uiDoc,
            Element roof)
        {
            _log.Info("Roof geometry pipeline started.");

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

            _log.Info($"Flattened edges collected: {flattenedEdges.Count}");

            // ⚠️ IMPORTANT:
            // Loops are used ONLY to detect corners
            // Routing must NEVER use cleaned / loop edges

            var loops =
                new RoofLoopBuilderService()
                    .BuildLoops(flattenedEdges, _log);

            var classified =
                new RoofEdgeClassificationService()
                    .Classify(loops, _log);

            var corners =
                new CornerDetectionService()
                    .DetectCorners(
                        classified.OuterLoop,
                        _log);

            _log.Info($"Corners detected: {corners.Count}");

            return new PipelineResult
            {
                Corners = corners,
                Drains = drains,

                // 🔑 ROUTING USES ALL EDGES
                AllEdges = flattenedEdges
            };
        }
    }

    public class PipelineResult
    {
        public IList<XYZ> Corners { get; set; }
        public IList<XYZ> Drains { get; set; }
        public IList<FlattenedEdge2D> AllEdges { get; set; }
    }
}
