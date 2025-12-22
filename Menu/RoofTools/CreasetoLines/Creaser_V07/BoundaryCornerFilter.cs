using System.Collections.Generic;

namespace Revit26_Plugin.Creaser_V07.Commands
{
    internal static class BoundaryCornerFilter
    {
        public static IReadOnlyList<XYZKey> GetBoundaryCorners(
            Dictionary<XYZKey, List<XYZKey>> graph)
        {
            List<XYZKey> result = new();

            foreach (var kvp in graph)
            {
                if (kvp.Value.Count == 1)
                    result.Add(kvp.Key);
            }

            return result;
        }
    }
}
