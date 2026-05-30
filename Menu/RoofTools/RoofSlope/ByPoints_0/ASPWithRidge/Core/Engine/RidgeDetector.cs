// =======================================================
// File: RidgeDetector.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.WithRidge
// Purpose:
//   Drain-group-based ridge detection using a roof-bounded
//   rectangle constructed from the shortest inter-group
//   drain pair.
//
// How it works:
//   1. Drain points are clustered into groups using a
//      user-defined grouping radius (DrainGroupRadiusMm).
//      Two drains within that XY distance share a group.
//   2. If fewer than 2 groups exist → no ridge possible.
//   3. The CLOSEST pair of drains across the two groups
//      (one drain from Group A, one from Group B, minimum
//      XY distance) defines the ridge line segment.
//   4. A local coordinate frame is built from this segment:
//        û    = unit vector along  the segment (A → B)
//        perp = unit vector across the segment (rotate û 90°)
//   5. Every roof boundary vertex (all SlabShapeVertices)
//      is projected onto both û and perp relative to the
//      near-A drain position.
//      - along  projections → min_along .. max_along
//      - across projections → min_across .. max_across
//      These four scalars define the roof-bounded rectangle
//      in local ridge coordinates.
//   6. The rectangle LENGTH is clamped to the two drain
//      positions: [0 .. segmentLength] along û.
//      The rectangle WIDTH spans the full roof boundary
//      on both sides of the ridge line (min_across .. max_across).
//   7. Any roof vertex whose local (along, across) lies
//      inside this rectangle is a ridge member.
//      Z is ignored throughout.
//   8. For each ridge member: average Dijkstra path to each
//      group is compared. The farther group wins — elevation
//      = path to nearest reachable drain in that group × slope.
//   9. Non-ridge vertices fall through to Q7 geometric
//      drain assignment (inverse-path-weighted direction vote).
// =======================================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByPoint.WithRidge.Core.Engine
{
    public static class RidgeDetector
    {
        // ── Public result types ───────────────────────────────────────────────

        /// <summary>
        /// Pre-computed ridge context built once before the main vertex loop.
        /// Passed into EvaluateVertex() for each vertex.
        /// Null when no ridge is possible (fewer than 2 drain groups).
        /// </summary>
        public class RidgeContext
        {
            // ── Ridge segment endpoints ───────────────────────────────────────

            /// <summary>XYZ position of the closest drain from Group A.</summary>
            public XYZ DrainNearA_Pos { get; set; }
            /// <summary>Vertex index of the closest drain from Group A.</summary>
            public int DrainNearA_Idx { get; set; }

            /// <summary>XYZ position of the closest drain from Group B.</summary>
            public XYZ DrainNearB_Pos { get; set; }
            /// <summary>Vertex index of the closest drain from Group B.</summary>
            public int DrainNearB_Idx { get; set; }

            /// <summary>All drain indices that belong to Group A.</summary>
            public List<int> GroupA_Indices { get; set; }
            /// <summary>All drain indices that belong to Group B.</summary>
            public List<int> GroupB_Indices { get; set; }

            // ── Local coordinate frame (XY only) ─────────────────────────────

            /// <summary>Origin of the local frame = DrainNearA projected to XY.</summary>
            internal XYZ Origin { get; set; }

            /// <summary>Unit vector along the ridge segment (A → B), Z = 0.</summary>
            internal XYZ UDir { get; set; }

            /// <summary>Unit vector perpendicular to the ridge (rotate UDir 90°), Z = 0.</summary>
            internal XYZ PerpDir { get; set; }

            // ── Rectangle extents in local coordinates ────────────────────────

            /// <summary>Min projection along UDir (= 0, the A drain position).</summary>
            internal double MinAlong { get; set; }

            /// <summary>Max projection along UDir (= segment length, the B drain position).</summary>
            internal double MaxAlong { get; set; }

            /// <summary>Min projection along PerpDir across the roof boundary.</summary>
            internal double MinAcross { get; set; }

            /// <summary>Max projection along PerpDir across the roof boundary.</summary>
            internal double MaxAcross { get; set; }
        }

        /// <summary>Result returned for each vertex from EvaluateVertex().</summary>
        public class RidgeResult
        {
            public bool IsRidge { get; set; }

            // When IsRidge = true
            public double PathToUse_ft  { get; set; }
            public int    DrainA        { get; set; } = -1;   // near-A drain index
            public int    DrainB        { get; set; } = -1;   // near-B drain index
            public double PathA_ft      { get; set; }         // path to near-A (informational)
            public double PathB_ft      { get; set; }         // path to far-group target

            // When IsRidge = false — geometric drain for Q7
            public int    GeometricDrainIndex   { get; set; } = -1;
            public double GeometricDrainPath_ft { get; set; }
        }

        // ── Step 1-6: build RidgeContext once before the main loop ───────────

        /// <summary>
        /// Clusters drain points, finds the closest cross-group drain pair,
        /// builds the local ridge coordinate frame, then projects all roof
        /// boundary vertices to compute the rectangle extents.
        /// Returns null if no ridge is possible.
        /// </summary>
        /// <param name="drainIndices">All matched drain vertex indices.</param>
        /// <param name="drainPositions">drainIndex → XYZ position.</param>
        /// <param name="allVertexPositions">All roof SlabShapeVertex positions (for boundary projection).</param>
        /// <param name="groupRadiusFt">Drain grouping radius in feet.</param>
        public static RidgeContext BuildContext(
            IEnumerable<int>      drainIndices,
            Dictionary<int, XYZ> drainPositions,
            IEnumerable<XYZ>     allVertexPositions,
            double                groupRadiusFt)
        {
            var idxList = drainIndices.ToList();
            if (idxList.Count < 2) return null;

            // ── Step 1: cluster drains by XY proximity ────────────────────────
            var groups = ClusterDrains(idxList, drainPositions, groupRadiusFt);
            if (groups.Count < 2) return null;

            // ── Step 2: find the two groups that will define the ridge ─────────
            // Use the farthest centroid pair so we always pick the dominant ridge.
            (List<int> gA, List<int> gB) = FindFarthestGroupPair(groups, drainPositions);

            // ── Step 3: closest cross-group drain pair (shortest segment) ──────
            (int nearA, int nearB) = FindClosestCrossGroupPair(gA, gB, drainPositions);

            XYZ posA = ToXY(drainPositions[nearA]);
            XYZ posB = ToXY(drainPositions[nearB]);

            // ── Step 4: build local coordinate frame ──────────────────────────
            XYZ segVec = posB - posA;
            double segLen = segVec.GetLength();
            if (segLen < 0.001) return null;   // coincident drains — no ridge

            XYZ uDir   = segVec.Multiply(1.0 / segLen);          // unit along ridge
            XYZ perpDir = new XYZ(-uDir.Y, uDir.X, 0);           // unit across ridge (90° rotate)

            // ── Step 5: project all roof boundary vertices onto both axes ──────
            // along  → clamped to [0, segLen] (between the two drain positions)
            // across → full roof extent on both sides
            double minAlong  = 0;
            double maxAlong  = segLen;
            double minAcross = double.MaxValue;
            double maxAcross = double.MinValue;

            foreach (XYZ v in allVertexPositions)
            {
                XYZ vxy   = ToXY(v);
                XYZ delta = vxy - posA;

                // We only use the across projection for boundary width;
                // the along extent is fixed to the drain segment (rule 3-C).
                double across = delta.DotProduct(perpDir);
                if (across < minAcross) minAcross = across;
                if (across > maxAcross) maxAcross = across;
            }

            // Safety: if no vertices projected (empty roof), use zero width
            if (minAcross == double.MaxValue) { minAcross = 0; maxAcross = 0; }

            return new RidgeContext
            {
                DrainNearA_Pos  = drainPositions[nearA],
                DrainNearA_Idx  = nearA,
                DrainNearB_Pos  = drainPositions[nearB],
                DrainNearB_Idx  = nearB,
                GroupA_Indices  = gA,
                GroupB_Indices  = gB,
                Origin          = posA,
                UDir            = uDir,
                PerpDir         = perpDir,
                MinAlong        = minAlong,
                MaxAlong        = maxAlong,
                MinAcross       = minAcross,
                MaxAcross       = maxAcross
            };
        }

        // ── Step 7-8: evaluate each vertex ───────────────────────────────────

        /// <summary>
        /// Tests whether a vertex falls inside the ridge rectangle and,
        /// if so, computes its elevation path using the farther drain group.
        /// </summary>
        /// <param name="vertexPos">Position of the vertex being tested.</param>
        /// <param name="pathsByDrain">drainIndex → Dijkstra path in feet for this vertex.</param>
        /// <param name="ctx">Pre-built RidgeContext (from BuildContext).</param>
        /// <param name="thresholdFt">Max path threshold in feet.</param>
        public static RidgeResult EvaluateVertex(
            XYZ                     vertexPos,
            Dictionary<int, double> pathsByDrain,
            RidgeContext            ctx,
            double                  thresholdFt)
        {
            if (ctx == null)
                return FallbackResult(vertexPos, pathsByDrain, ctx, thresholdFt);

            // ── Step 7: rectangle membership test (2-axis projection) ─────────
            XYZ vxy   = ToXY(vertexPos);
            XYZ delta = vxy - ctx.Origin;

            double along  = delta.DotProduct(ctx.UDir);
            double across = delta.DotProduct(ctx.PerpDir);

            bool insideAlong  = along  >= ctx.MinAlong  && along  <= ctx.MaxAlong;
            bool insideAcross = across >= ctx.MinAcross  && across <= ctx.MaxAcross;

            if (!insideAlong || !insideAcross)
                return FallbackResult(vertexPos, pathsByDrain, ctx, thresholdFt);

            // ── Step 8: which group is farther by average Dijkstra path? ──────
            double avgPathA = AverageGroupPath(ctx.GroupA_Indices, pathsByDrain, thresholdFt);
            double avgPathB = AverageGroupPath(ctx.GroupB_Indices, pathsByDrain, thresholdFt);

            // Far group = the group with the larger average path from this vertex
            List<int> farGroup  = avgPathA >= avgPathB ? ctx.GroupA_Indices : ctx.GroupB_Indices;
            List<int> nearGroup = avgPathA >= avgPathB ? ctx.GroupB_Indices : ctx.GroupA_Indices;

            // Nearest reachable drain in the far group
            int targetDrain = NearestReachableDrain(farGroup, pathsByDrain, thresholdFt);
            if (targetDrain < 0)
                targetDrain = NearestReachableDrain(nearGroup, pathsByDrain, thresholdFt);

            if (targetDrain < 0)
                return FallbackResult(vertexPos, pathsByDrain, ctx, thresholdFt);

            double pathToTarget = pathsByDrain.TryGetValue(targetDrain, out double p)
                ? p : double.PositiveInfinity;

            if (double.IsInfinity(pathToTarget) || pathToTarget > thresholdFt)
                return FallbackResult(vertexPos, pathsByDrain, ctx, thresholdFt);

            // Informational: path to the near-side facing drain
            int nearFacing = avgPathA >= avgPathB
                ? ctx.DrainNearB_Idx
                : ctx.DrainNearA_Idx;
            double pathNear = pathsByDrain.TryGetValue(nearFacing, out double pn) ? pn : 0;

            return new RidgeResult
            {
                IsRidge      = true,
                DrainA       = ctx.DrainNearA_Idx,
                DrainB       = ctx.DrainNearB_Idx,
                PathA_ft     = pathNear,
                PathB_ft     = pathToTarget,
                PathToUse_ft = pathToTarget
            };
        }

        // ── Clustering ────────────────────────────────────────────────────────

        /// <summary>
        /// Single-linkage clustering: two drains are in the same group
        /// if their XY distance is within groupRadiusFt.
        /// </summary>
        private static List<List<int>> ClusterDrains(
            List<int>            idxList,
            Dictionary<int, XYZ> positions,
            double               radiusFt)
        {
            var parent = idxList.ToDictionary(i => i, i => i);

            int Find(int x)
            {
                while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
                return x;
            }
            void Union(int a, int b)
            {
                int ra = Find(a), rb = Find(b);
                if (ra != rb) parent[ra] = rb;
            }

            for (int i = 0; i < idxList.Count; i++)
                for (int j = i + 1; j < idxList.Count; j++)
                {
                    double d = XYDist(positions[idxList[i]], positions[idxList[j]]);
                    if (d <= radiusFt) Union(idxList[i], idxList[j]);
                }

            return idxList
                .GroupBy(Find)
                .Select(g => g.ToList())
                .ToList();
        }

        /// <summary>
        /// Finds the pair of groups whose XY centroids are farthest apart.
        /// Used to identify the dominant ridge axis across the roof.
        /// </summary>
        private static (List<int>, List<int>) FindFarthestGroupPair(
            List<List<int>>      groups,
            Dictionary<int, XYZ> positions)
        {
            List<int> bestA = groups[0], bestB = groups[1];
            double    bestD = 0;

            for (int i = 0; i < groups.Count; i++)
            for (int j = i + 1; j < groups.Count; j++)
            {
                double d = XYDist(
                    GroupCentroidXY(groups[i], positions),
                    GroupCentroidXY(groups[j], positions));
                if (d > bestD) { bestD = d; bestA = groups[i]; bestB = groups[j]; }
            }
            return (bestA, bestB);
        }

        /// <summary>
        /// Finds the single closest drain pair across two groups —
        /// one drain from gA and one from gB with minimum XY distance.
        /// This pair defines the ridge segment.
        /// </summary>
        private static (int idxA, int idxB) FindClosestCrossGroupPair(
            List<int>            gA,
            List<int>            gB,
            Dictionary<int, XYZ> positions)
        {
            int    bestA = gA[0], bestB = gB[0];
            double bestD = double.MaxValue;

            foreach (int a in gA)
            foreach (int b in gB)
            {
                double d = XYDist(positions[a], positions[b]);
                if (d < bestD) { bestD = d; bestA = a; bestB = b; }
            }
            return (bestA, bestB);
        }

        // ── Q7 geometric fallback ─────────────────────────────────────────────

        private static RidgeResult FallbackResult(
            XYZ                     vertexPos,
            Dictionary<int, double> pathsByDrain,
            RidgeContext            ctx,
            double                  thresholdFt)
        {
            var reachable = pathsByDrain
                .Where(kvp => !double.IsInfinity(kvp.Value) && kvp.Value <= thresholdFt)
                .OrderBy(kvp => kvp.Value)
                .ToList();

            if (reachable.Count == 0)
                return new RidgeResult { IsRidge = false, GeometricDrainIndex = -1 };

            if (reachable.Count == 1)
                return new RidgeResult
                {
                    IsRidge               = false,
                    GeometricDrainIndex   = reachable[0].Key,
                    GeometricDrainPath_ft = reachable[0].Value
                };

            // Build full drain position lookup from all group indices
            var drainPositions = new Dictionary<int, XYZ>();
            if (ctx != null)
            {
                foreach (int idx in ctx.GroupA_Indices)
                    if (!drainPositions.ContainsKey(idx))
                        drainPositions[idx] = ctx.DrainNearA_Pos;   // approximate; correct pos set below
                foreach (int idx in ctx.GroupB_Indices)
                    if (!drainPositions.ContainsKey(idx))
                        drainPositions[idx] = ctx.DrainNearB_Pos;

                // Override with the exact known positions for the two facing drains
                drainPositions[ctx.DrainNearA_Idx] = ctx.DrainNearA_Pos;
                drainPositions[ctx.DrainNearB_Idx] = ctx.DrainNearB_Pos;
            }

            if (drainPositions.Count == 0)
                return new RidgeResult
                {
                    IsRidge               = false,
                    GeometricDrainIndex   = reachable[0].Key,
                    GeometricDrainPath_ft = reachable[0].Value
                };

            // Outward vector weighted by inverse path — closest drain pulls hardest
            XYZ outward = XYZ.Zero;
            foreach (var kvp in reachable)
            {
                if (!drainPositions.TryGetValue(kvp.Key, out XYZ dPos)) continue;
                XYZ away = ToXY(vertexPos - dPos);
                if (away.GetLength() < 0.001) continue;
                double w = kvp.Value > 0 ? 1.0 / kvp.Value : 1.0;
                outward = outward.Add(away.Normalize().Multiply(w));
            }

            if (outward.GetLength() < 0.001)
                return new RidgeResult
                {
                    IsRidge               = false,
                    GeometricDrainIndex   = reachable[0].Key,
                    GeometricDrainPath_ft = reachable[0].Value
                };

            outward = outward.Normalize();

            // Pick the drain whose direction from the vertex most opposes outward
            int    best    = reachable[0].Key;
            double bestDot = double.MaxValue;
            foreach (var kvp in reachable)
            {
                if (!drainPositions.TryGetValue(kvp.Key, out XYZ dPos)) continue;
                XYZ vec = ToXY(dPos - vertexPos);
                if (vec.GetLength() < 0.001) continue;
                double dot = outward.DotProduct(vec.Normalize());
                if (dot < bestDot) { bestDot = dot; best = kvp.Key; }
            }

            return new RidgeResult
            {
                IsRidge               = false,
                GeometricDrainIndex   = best,
                GeometricDrainPath_ft = pathsByDrain.TryGetValue(best, out double bp)
                                        ? bp : double.PositiveInfinity
            };
        }

        // ── Small helpers ─────────────────────────────────────────────────────

        private static XYZ GroupCentroidXY(List<int> group, Dictionary<int, XYZ> positions)
        {
            double sx = 0, sy = 0;
            foreach (int idx in group) { sx += positions[idx].X; sy += positions[idx].Y; }
            return new XYZ(sx / group.Count, sy / group.Count, 0);
        }

        private static int NearestReachableDrain(
            List<int>               group,
            Dictionary<int, double> pathsByDrain,
            double                  thresholdFt)
        {
            int    best     = -1;
            double bestPath = double.MaxValue;
            foreach (int idx in group)
            {
                if (!pathsByDrain.TryGetValue(idx, out double p)) continue;
                if (double.IsInfinity(p) || p > thresholdFt)       continue;
                if (p < bestPath) { bestPath = p; best = idx; }
            }
            return best;
        }

        private static double AverageGroupPath(
            List<int>               group,
            Dictionary<int, double> pathsByDrain,
            double                  thresholdFt)
        {
            var valid = group
                .Where(idx => pathsByDrain.TryGetValue(idx, out double p)
                              && !double.IsInfinity(p) && p <= thresholdFt)
                .Select(idx => pathsByDrain[idx])
                .ToList();
            return valid.Count > 0 ? valid.Average() : double.PositiveInfinity;
        }

        private static double XYDist(XYZ a, XYZ b)
            => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

        private static XYZ ToXY(XYZ v) => new XYZ(v.X, v.Y, 0);
    }
}
