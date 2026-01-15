using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    public class TopologyDrainSnapService
    {
        public IList<XYZ> SnapDrainsToTopology(
            IList<XYZ> drains,
            IList<FlattenedEdge2D> topologyEdges)
        {
            var snapped = new List<XYZ>();

            foreach (var drain in drains)
            {
                FlattenedEdge2D closestEdge = null;
                XYZ closestPoint = null;
                double minDist = double.MaxValue;

                foreach (var edge in topologyEdges)
                {
                    var line = Line.CreateBound(edge.Start2D, edge.End2D);
                    var projection = line.Project(drain);
                    if (projection == null)
                        continue;

                    var p = projection.XYZPoint;
                    double d = drain.DistanceTo(p);

                    if (d < minDist)
                    {
                        minDist = d;
                        closestEdge = edge;
                        closestPoint = p;
                    }
                }

                if (closestEdge != null && closestPoint != null)
                {
                    snapped.Add(new XYZ(
                        closestPoint.X,
                        closestPoint.Y,
                        0));
                }
            }

            return snapped
                .Distinct(new Point2DComparer())
                .ToList();
        }
    }
}
