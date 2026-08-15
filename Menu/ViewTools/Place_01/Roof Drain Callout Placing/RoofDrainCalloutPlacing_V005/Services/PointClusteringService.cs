using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.RoofDrainCalloutPlacing.V005.Helpers;
using Revit26_Plugin.RoofDrainCalloutPlacing.V005.Models;

namespace Revit26_Plugin.RoofDrainCalloutPlacing.V005.Services
{
    /// <summary>
    /// Clusters candidate points by XY proximity using union-find — same
    /// approach as RoofDetailLineIntersect / CreaserAdvanced — then reduces
    /// each cluster to a single centroid. Mode-agnostic: works the same
    /// whether points came from auto-detection, user picks, or grid selection.
    /// </summary>
    public class PointClusteringService
    {
        /// <summary>
        /// Groups points whose XY distance is within toleranceFeet of each other
        /// (transitively — points don't need to be pairwise within tolerance of
        /// every other point in the group, only chained).
        /// </summary>
        public List<ZeroOffsetPointGroup> Cluster(ElementId roofId, List<XYZ> points, double toleranceFeet)
        {
            var groups = new List<ZeroOffsetPointGroup>();
            if (points == null || points.Count == 0)
                return groups;

            var uf = new UnionFind(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                for (int j = i + 1; j < points.Count; j++)
                {
                    double dx = points[i].X - points[j].X;
                    double dy = points[i].Y - points[j].Y;
                    double distXY = System.Math.Sqrt(dx * dx + dy * dy);
                    if (distXY <= toleranceFeet)
                        uf.Union(i, j);
                }
            }

            foreach (var indexGroup in uf.GetGroups(points.Count))
            {
                var groupPoints = indexGroup.Select(i => points[i]).ToList();
                groups.Add(new ZeroOffsetPointGroup
                {
                    RoofId = roofId,
                    Points = groupPoints,
                    Centroid = ComputeCentroidXY(groupPoints)
                });
            }

            return groups;
        }

        /// <summary>Averages X and Y across the group; Z comes from the first point (flattened later by caller as needed).</summary>
        private XYZ ComputeCentroidXY(List<XYZ> points)
        {
            double sumX = 0, sumY = 0, sumZ = 0;
            foreach (var p in points)
            {
                sumX += p.X;
                sumY += p.Y;
                sumZ += p.Z;
            }
            int n = points.Count;
            return new XYZ(sumX / n, sumY / n, sumZ / n);
        }
    }
}
