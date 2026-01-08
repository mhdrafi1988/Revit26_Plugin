using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.V5_00.Domain.Services
{
    /// <summary>
    /// Builds directed line segments from a solved drainage graph.
    /// Uses Z to orient each segment (higher Z = start).
    /// </summary>
    public class PathSegmentBuilderService
    {
        /// <summary>
        /// Simple version: connect each vertex directly to its nearest drain.
        /// </summary>
        public List<(XYZ, XYZ)> BuildDirectedSegments(
            Dictionary<SlabShapeVertex, (SlabShapeVertex nearest, double dist)> pathResults)
        {
            var segments = new List<(XYZ, XYZ)>();

            if (pathResults == null)
                return segments;

            foreach (var kvp in pathResults)
            {
                var v = kvp.Key;
                var nearest = kvp.Value.nearest;

                XYZ pStart = v?.Position;
                XYZ pEnd = nearest?.Position;

                if (pStart == null || pEnd == null)
                    continue;

                XYZ P1, P2;
                if (pStart.Z >= pEnd.Z)
                {
                    P1 = pStart;
                    P2 = pEnd;
                }
                else
                {
                    P1 = pEnd;
                    P2 = pStart;
                }

                segments.Add((P1, P2));
            }

            return segments;
        }
    }
}
