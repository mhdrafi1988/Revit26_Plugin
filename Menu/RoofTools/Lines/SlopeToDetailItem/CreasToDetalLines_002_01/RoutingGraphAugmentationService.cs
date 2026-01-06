using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    public class RoutingGraphAugmentationService
    {
        public IList<FlattenedEdge2D> InsertNodes(
            IList<FlattenedEdge2D> edges,
            IList<XYZ> points)
        {
            var comparer = new Point2DComparer();
            var result = new List<FlattenedEdge2D>(edges);

            foreach (var p in points)
            {
                FlattenedEdge2D closest = null;
                XYZ projected = null;
                double minDist = double.MaxValue;

                foreach (var e in result)
                {
                    var line = Line.CreateBound(e.Start2D, e.End2D);
                    var projection = line.Project(p);
                    if (projection == null)
                        continue;

                    XYZ q = projection.XYZPoint;
                    double d = p.DistanceTo(q);

                    if (d < minDist)
                    {
                        minDist = d;
                        closest = e;
                        projected = q;
                    }
                }

                if (closest == null || projected == null)
                    continue;

                if (projected.DistanceTo(closest.Start2D) < GeometryTolerance.Point ||
                    projected.DistanceTo(closest.End2D) < GeometryTolerance.Point)
                    continue;

                result.Remove(closest);

                result.Add(new FlattenedEdge2D(
                    closest.Start2D,
                    projected));

                result.Add(new FlattenedEdge2D(
                    projected,
                    closest.End2D));
            }

            return result;
        }
    }
}
