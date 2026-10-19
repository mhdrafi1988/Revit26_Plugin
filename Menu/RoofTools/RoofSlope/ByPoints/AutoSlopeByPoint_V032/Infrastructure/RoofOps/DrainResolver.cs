// =======================================================
// File: DrainResolver.cs
// Namespace: Revit26_Plugin_Revised.AutoSlopeByPoint.V032.Infrastructure.RoofOps
// Replaces V030's DrainDetectionHelper + the engine's second radius match.
// Fixes from the review:
//   D1  Returns the exact set of drain VERTEX INDICES. The engine uses it
//       as-is; V030 matched again by radius with no loop check, undoing
//       the "same edge / opening" rule and chaining to ~2× the radius.
//   D2  Every pick resolves: snap to the nearest shape point in plan, or
//       (Add mode) to the point RoofPreparer added at the pick. V030 with
//       snapping off matched at 0.3 mm and almost always aborted.
//   D3  All distances are plan (XY) distances.
//   B10 No hard-coded 10 mm grid key; duplicates are removed by index.
// =======================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin_Revised.AutoSlopeByPoint.V032.Core.Models;
using Revit26_Plugin_Revised.AutoSlopeByPoint.V032.Core.Services;
using Revit26_Plugin_Revised.Shared.Models;

namespace Revit26_Plugin_Revised.AutoSlopeByPoint.V032.Infrastructure.RoofOps
{
    internal sealed class DrainResolution
    {
        /// <summary>Drain vertex indices, ascending. Position in this list = drain ordinal.</summary>
        public List<int> DrainVertices { get; } = new List<int>();
        public int PickedCount { get; set; }
        public int NearbyAdded { get; set; }
    }

    internal static class DrainResolver
    {
        public static DrainResolution Resolve(
            IReadOnlyList<Point3> picks,
            IReadOnlyList<XYZ> vertexPositions,
            Face topFace,
            AutoSlopeRequest request,
            Action<LogLevel, string> log)
        {
            var result = new DrainResolution { PickedCount = picks?.Count ?? 0 };
            if (picks == null || picks.Count == 0 || vertexPositions == null || vertexPositions.Count == 0)
                return result;

            var points = vertexPositions.Select(p => p.ToPoint3()).ToList();
            double snapRadiusFt = RevitConvert.MmToFt(request.EffectiveSnapRadiusMm);
            var grid = new SpatialGrid2D(points, Math.Max(snapRadiusFt, 1.0));
            double warnFt = RevitConvert.MmToFt(AutoSlopeConstants.SnapWarningDistanceMm);
            double addedMatchFt = RevitConvert.MmToFt(AutoSlopeConstants.AddedPointMatchMm);

            List<List<Curve>> loops = snapRadiusFt > 0 ? AutoSlopeGeometry.GetBoundaryLoops(topFace) : new List<List<Curve>>();
            bool loopAware = loops.Count > 0;
            var vertexLoop = new Dictionary<int, int>();
            int LoopOfVertex(int i)
            {
                if (!vertexLoop.TryGetValue(i, out int loop))
                    vertexLoop[i] = loop = AutoSlopeGeometry.GetClosestLoopIndex(vertexPositions[i], loops);
                return loop;
            }

            if (snapRadiusFt > 0 && !loopAware)
                log(LogLevel.Warning, "Nearby points: the roof's edges could not be read, so the same-edge rule is off for this run.");

            var drains = new HashSet<int>();
            for (int k = 0; k < picks.Count; k++)
            {
                Point3 pick = picks[k];

                // Primary drain for this pick: XY-nearest shape point.
                int nearest = grid.NearestXY(pick, out double nearestFt);
                if (nearest < 0) continue;

                double nearestMm = RevitConvert.FtToMm(nearestFt);
                if (request.DrainMode == DrainMode.AddPointAtPick && nearestFt > addedMatchFt)
                    log(LogLevel.Warning, $"Pick {k + 1}: no shape point at the pick; using the nearest one, {nearestMm:0} mm away.");
                else if (request.DrainMode == DrainMode.SnapToNearestPoint && nearestFt > warnFt)
                    log(LogLevel.Warning, $"Pick {k + 1} is {nearestMm:0} mm from the nearest shape point; the drain is placed there. Choose \"Add a shape point at each pick\" to put it exactly where you clicked.");

                drains.Add(nearest);

                // Nearby points on the same edge or opening.
                if (snapRadiusFt <= 0) continue;

                int pickLoop = loopAware ? AutoSlopeGeometry.GetClosestLoopIndex(pick.ToXyz(), loops) : -1;
                foreach (int j in grid.QueryXY(pick, snapRadiusFt))
                {
                    if (drains.Contains(j)) continue;
                    if (loopAware && LoopOfVertex(j) != pickLoop) continue;
                    drains.Add(j);
                    result.NearbyAdded++;
                }
            }

            result.DrainVertices.AddRange(drains.OrderBy(i => i));

            string nearby = snapRadiusFt > 0
                ? $" ({result.NearbyAdded} nearby point(s) within {request.SnapRadiusMm:0} mm on the same edge/opening)"
                : string.Empty;
            log(LogLevel.Info, $"Drains: {result.PickedCount} picked → {result.DrainVertices.Count} roof shape point(s){nearby}.");
            return result;
        }
    }
}
