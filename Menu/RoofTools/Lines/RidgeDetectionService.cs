using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;
using Revit26_Plugin.AutoLiner_V01.Helpers;

namespace Revit26_Plugin.AutoLiner_V01.Services
{
    public class RidgeDetectionService
    {
        public List<XYZ> DetectRidges(
            Dictionary<SlabShapeVertex, List<SlabShapeVertex>> graph,
            Dictionary<SlabShapeVertex, double> distanceToDrain,
            HashSet<SlabShapeVertex> drainVertices)
        {
            var ridgeCandidates = new List<SlabShapeVertex>();

            if (graph == null || graph.Count == 0)
                return new List<XYZ>();

            // if only one drain → no ridge
            if (drainVertices == null || drainVertices.Count < 2)
                return new List<XYZ>();

            double zTol = GeometryTolerance.MmToFt(5);       // 5 mm
            double dTol = GeometryTolerance.MmToFt(100);     // 100 mm

            foreach (var v in graph.Keys)
            {
                if (v?.Position == null)
                    continue;

                if (drainVertices.Contains(v))
                    continue;

                if (!distanceToDrain.ContainsKey(v))
                    continue;

                double vz = v.Position.Z;
                double vDist = distanceToDrain[v];

                bool isHigher = true;
                bool isFarther = true;

                foreach (var n in graph[v])
                {
                    if (n?.Position == null)
                        continue;

                    if (n.Position.Z > vz + zTol)
                    {
                        isHigher = false;
                        break;
                    }

                    if (distanceToDrain.ContainsKey(n) &&
                        distanceToDrain[n] > vDist + dTol)
                    {
                        isFarther = false;
                        break;
                    }
                }

                if (isHigher && isFarther)
                    ridgeCandidates.Add(v);
            }

            return MergeRidgePoints(ridgeCandidates);
        }

        // =====================================================
        // MERGE RIDGE CLUSTERS
        // =====================================================
        private List<XYZ> MergeRidgePoints(List<SlabShapeVertex> ridges)
        {
            var result = new List<XYZ>();
            var used = new HashSet<SlabShapeVertex>();

            double mergeTol = GeometryTolerance.MmToFt(300); // 300 mm

            foreach (var v in ridges)
            {
                if (used.Contains(v))
                    continue;

                var cluster = new List<XYZ> { v.Position };
                used.Add(v);

                foreach (var other in ridges)
                {
                    if (used.Contains(other))
                        continue;

                    if (v.Position.DistanceTo(other.Position) < mergeTol)
                    {
                        cluster.Add(other.Position);
                        used.Add(other);
                    }
                }

                result.Add(Centroid(cluster));
            }

            return result;
        }

        private XYZ Centroid(List<XYZ> pts)
        {
            return new XYZ(
                pts.Average(p => p.X),
                pts.Average(p => p.Y),
                pts.Average(p => p.Z));
        }
    }
}
