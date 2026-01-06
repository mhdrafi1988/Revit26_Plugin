using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    public class CornerDetectionService
    {
        public IList<XYZ> DetectCornersFromEdges(
            IList<Line> boundaryLines,
            LoggingService log)
        {
            var corners =
                boundaryLines
                    .SelectMany(l => new[] { l.GetEndPoint(0), l.GetEndPoint(1) })
                    .Distinct(new Point2DComparer())
                    .ToList();

            log.Info($"Corners detected: {corners.Count}");
            return corners;
        }
    }
}
