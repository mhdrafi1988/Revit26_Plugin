using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.V5_00.Domain.Services
{
    /// <summary>
    /// Builds directed line segments from a solved drainage graph.
    /// Uses Z to orient each segment (higher Z = P1).
    /// </summary>
    public class PathSegmentBuilderService
    {
        /// <summary>
        /// Simple version: for each vertex, connect it directly to its nearest drain.
        /// For a full polyline reconstruction, extend this to walk the Dijkstra parent chain.
        /// </summary>
        public List<(XYZ P1, XYZ P2)> BuildDirectedSegments(
            Dictionary<SlabShapeVertex, (SlabShapeVertex nearest, double dist)> pathResults)
        {
            var segments = new List<(XYZ P1, XYZ P2)>();

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

                // Orient by Z: higher Z is P1 (start)
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
