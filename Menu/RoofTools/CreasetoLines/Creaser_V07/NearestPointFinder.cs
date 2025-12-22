// ============================================================
// File: NearestPointFinder.cs
// Namespace: Revit26_Plugin.Creaser_V07.Commands
// ============================================================

using System.Collections.Generic;

namespace Revit26_Plugin.Creaser_V07.Commands
{
    internal static class NearestPointFinder
    {
        public static XYZKey FindNearest(XYZKey from, IEnumerable<XYZKey> candidates)
        {
            bool has = false;
            XYZKey best = default;
            double bestD = double.PositiveInfinity;

            foreach (var c in candidates)
            {
                double d = from.DistanceTo(c);
                if (!has || d < bestD)
                {
                    has = true;
                    bestD = d;
                    best = c;
                }
            }

            return best;
        }
    }
}
