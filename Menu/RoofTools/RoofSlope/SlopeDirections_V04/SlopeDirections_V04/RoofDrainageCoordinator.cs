using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit_26.CornertoDrainArrow_V05
{
    public class RoofDrainageCoordinator
    {
        private readonly RoofShapePointService _pointService;
        private readonly WaterPathService _pathService;
        private readonly ILogService _logService;

        public RoofDrainageCoordinator(ILogService logService)
        {
            _logService = logService;
            _pointService = new RoofShapePointService();
            _pathService = new WaterPathService();
        }

        /// <summary>
        /// Full analysis returning both summary + paths (UI only).
        /// </summary>
        public RoofDrainageAnalysisDto AnalyzeRoofDetailed(
            Document doc,
            ElementId roofId)
        {
            var roof = doc.GetElement(roofId)
                ?? throw new InvalidOperationException("Selected roof no longer exists.");

            _logService.Info("Collecting roof geometry…");

            var corners = _pointService.GetRoofCorners(roof);
            var drains = _pointService.GetRoofDrains(doc, roofId);

            var paths = PathFindingHelper.FindPaths(corners, drains);
            var validPaths = _pathService.ValidatePaths(paths);

            return new RoofDrainageAnalysisDto
            {
                Summary = new RoofDrainageRunResult
                {
                    CornerCount = corners.Count,
                    DrainCount = drains.Count,
                    TotalPaths = paths.Count,
                    ValidPaths = validPaths.Count(p => p.IsValid)
                },
                Paths = paths
            };
        }
    }
}
