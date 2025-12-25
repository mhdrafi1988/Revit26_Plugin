using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.Creaser_V100.Models
{
    public class PathResult
    {
        public XYZ CornerPoint { get; }
        public XYZ DrainPoint { get; }
        public List<XYZ> PathPoints { get; }
        public double TotalLength { get; }

        public PathResult(
            XYZ cornerPoint,
            XYZ drainPoint,
            List<XYZ> pathPoints,
            double totalLength)
        {
            CornerPoint = cornerPoint;
            DrainPoint = drainPoint;
            PathPoints = pathPoints;
            TotalLength = totalLength;
        }
    }
}
