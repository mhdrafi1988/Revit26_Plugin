using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.Creaser_V08.Commands.Services
{
    /// <summary>
    /// Computes shortest paths from corners to drains
    /// and converts them into crease line segments.
    /// </summary>
    public class ShortestPathService
    {
        /// <summary>
        /// Builds all crease paths and returns line segments.
        /// Provides optional per-crease diagnostics.
        /// </summary>
        public IList<Line> BuildPaths(
            Dictionary<XYZ, List<XYZ>> graph,
            IList<XYZ> corners,
            IList<XYZ> drains,
            out int failedPaths,
            Action<int, XYZ, XYZ, int> perCreaseLog = null)
        {
            failedPaths = 0;
            List<Line> allLines = new();

            int creaseIndex = 1;

            foreach (XYZ corner in corners)
            {
                // Find shortest path from this corner to any drain
                List<XYZ> path =
                    DrainPathSolver.FindShortestPath(
                        corner,
                        drains,
                        graph);   // ✅ graph (NOT graphDict)

                if (path == null || path.Count < 2)
                {
                    failedPaths++;
                    creaseIndex++;
                    continue;
                }

                int segmentCount = 0;

                for (int i = 0; i < path.Count - 1; i++)
                {
                    Line line = Line.CreateBound(
                        path[i],
                        path[i + 1]);

                    allLines.Add(line);
                    segmentCount++;
                }

                // Per-crease diagnostic callback
                perCreaseLog?.Invoke(
                    creaseIndex,
                    corner,
                    path[^1],        // final drain
                    segmentCount);

                creaseIndex++;
            }

            return allLines;
        }
    }
}
