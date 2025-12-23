// ============================================================
// File: PathMetrics.cs
// Namespace: Revit26_Plugin.Creaser_V07.Commands
// ============================================================

using System.Collections.Generic;

namespace Revit26_Plugin.Creaser_V07.Commands
{
    internal static class PathMetrics
    {
        public static double ComputePolylineLength(IReadOnlyList<XYZKey> path)
        {
            if (path == null || path.Count < 2)
                return 0;

            double sum = 0;
            for (int i = 0; i < path.Count - 1; i++)
                sum += path[i].DistanceTo(path[i + 1]);

            return sum;
        }
    }
}
