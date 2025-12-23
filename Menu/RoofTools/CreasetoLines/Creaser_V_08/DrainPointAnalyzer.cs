using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.Creaser_V08.Commands.GeometryHelpers
{
    /// <summary>
    /// Implements exact drain detection logic per specification.
    /// </summary>
    public static class DrainPointAnalyzer
    {
        public static IList<XYZ> DetectDrains(
            IList<XYZ> shapePoints,
            double toleranceInternal,
            double clusterRadiusInternal,
            out double lowestZ,
            out int rawCandidates,
            out int clusterCount)
        {
            List<XYZ> drains = new();

            lowestZ = double.MaxValue;
            foreach (XYZ p in shapePoints)
                lowestZ = Math.Min(lowestZ, p.Z);

            List<XYZ> candidates = new();
            foreach (XYZ p in shapePoints)
            {
                if (Math.Abs(p.Z - lowestZ) <= toleranceInternal)
                    candidates.Add(p);
            }

            rawCandidates = candidates.Count;

            List<List<XYZ>> clusters = new();

            foreach (XYZ p in candidates)
            {
                bool added = false;
                foreach (List<XYZ> cluster in clusters)
                {
                    if (Distance2D(cluster[0], p) <= clusterRadiusInternal)
                    {
                        cluster.Add(p);
                        added = true;
                        break;
                    }
                }

                if (!added)
                    clusters.Add(new List<XYZ> { p });
            }

            clusterCount = clusters.Count;

            foreach (List<XYZ> cluster in clusters)
            {
                XYZ centroid = ComputeCentroid2D(cluster);
                XYZ closest = cluster[0];
                double minDist = Distance2D(centroid, closest);

                foreach (XYZ p in cluster)
                {
                    double d = Distance2D(centroid, p);
                    if (d < minDist)
                    {
                        minDist = d;
                        closest = p;
                    }
                }

                drains.Add(closest);
            }

            return drains;
        }

        private static double Distance2D(XYZ a, XYZ b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static XYZ ComputeCentroid2D(IList<XYZ> pts)
        {
            double x = 0, y = 0, z = pts[0].Z;
            foreach (XYZ p in pts)
            {
                x += p.X;
                y += p.Y;
            }
            return new XYZ(x / pts.Count, y / pts.Count, z);
        }
    }
}
