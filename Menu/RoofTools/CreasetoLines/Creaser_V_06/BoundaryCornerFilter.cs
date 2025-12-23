// ============================================================
// File: BoundaryCornerFilter.cs
// Namespace: Revit26_Plugin.Creaser_V06.Commands
// ============================================================

using System;
using System.Collections.Generic;

namespace Revit26_Plugin.Creaser_V06_01.Commands
{
    internal static class BoundaryCornerFilter
    {
        public static IReadOnlyList<XYZKey> GetBoundaryCorners(
            Dictionary<XYZKey, List<XYZKey>> graph)
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));

            List<XYZKey> result = new();

            foreach (var kvp in graph)
            {
                if (kvp.Value != null && kvp.Value.Count == 1)
                    result.Add(kvp.Key);
            }

            return result;
        }
    }
}
