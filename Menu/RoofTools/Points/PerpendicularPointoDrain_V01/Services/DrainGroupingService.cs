using Autodesk.Revit.DB;
using Revit26_Plugin.PerpendicularPointoDrain.V01.Models;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.PerpendicularPointoDrain.V01.Services
{
    /// <summary>
    /// Groups nearby drain points using a tolerance distance, same Union-Find shape as the
    /// drain clustering in AutoSlopeByPoint's ridge detection — reimplemented locally here
    /// per the agreed design (no shared service between the two tools).
    /// </summary>
    public class DrainGroupingService
    {
        public List<DrainGroupModel> GroupDrains(List<XYZ> drainPoints, double toleranceFeet)
        {
            int n = drainPoints.Count;
            var result = new List<DrainGroupModel>();
            if (n == 0) return result;

            int[] parent = Enumerable.Range(0, n).ToArray();

            int Find(int i)
            {
                while (parent[i] != i)
                {
                    parent[i] = parent[parent[i]];
                    i = parent[i];
                }
                return i;
            }

            void Union(int a, int b)
            {
                a = Find(a);
                b = Find(b);
                if (a != b) parent[a] = b;
            }

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    if (drainPoints[i].DistanceTo(drainPoints[j]) <= toleranceFeet)
                        Union(i, j);
                }
            }

            var buckets = new Dictionary<int, List<XYZ>>();
            for (int i = 0; i < n; i++)
            {
                int root = Find(i);
                if (!buckets.TryGetValue(root, out var list))
                {
                    list = new List<XYZ>();
                    buckets[root] = list;
                }
                list.Add(drainPoints[i]);
            }

            int idx = 1;
            foreach (var pts in buckets.Values)
            {
                XYZ centroid = new XYZ(
                    pts.Average(p => p.X),
                    pts.Average(p => p.Y),
                    pts.Average(p => p.Z));

                result.Add(new DrainGroupModel
                {
                    Label       = $"G{idx}",
                    DrainPoints = pts,
                    Centroid    = centroid
                });
                idx++;
            }

            return result;
        }
    }
}
