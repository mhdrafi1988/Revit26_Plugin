// =======================================================
// File: AutoSlopeEngine.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.V021
// Changes vs V06:
//   - LogColorHelper removed entirely.
//   - All data.Log() calls now emit new LogEntry(LogLevel.X, "...")
//     so colour is driven by LogLevelToColorConverter in the UI.
// =======================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlopeByPoint.V021.Core.Models;
using Revit26_Plugin.AutoSlopeByPoint.V021.Core.Parameters;
using Revit26_Plugin.AutoSlopeByPoint.V021.Infrastructure.Helpers;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByPoint.V021.Core.Engine
{
    public static class AutoSlopeEngine
    {
        public static void Execute(UIApplication app, AutoSlopePayload data)
        {
            Document doc = app.ActiveUIDocument.Document;

            // ── Guard: roof ─────────────────────────────────────────────────
            RoofBase roof = doc.GetElement(data.RoofId) as RoofBase;
            if (roof == null)
            {
                FireFailure(data, "Roof element not found. Aborting.");
                return;
            }

            // ── Guard: slab shape editor ────────────────────────────────────
            SlabShapeEditor editor = roof.GetSlabShapeEditor();
            if (editor == null || !editor.IsValidObject)
            {
                data.Log?.Invoke(new LogEntry(LogLevel.Error,
                    "Roof slab shape editor is not available. Aborting."));
                FireFailure(data, "Roof slab shape editor is not available.");
                return;
            }

            // ── Reset vertices ───────────────────────────────────────────────
            using (Transaction tx = new Transaction(doc, "Reset Roof Vertices"))
            {
                tx.Start();
                foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                    editor.ModifySubElement(v, 0);
                tx.Commit();
            }

            // ── Collect vertices ─────────────────────────────────────────────
            var vertices = new List<SlabShapeVertex>();
            foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                vertices.Add(v);

            double slopeFactor = data.SlopePercent / 100.0;
            double thresholdFt = UnitUtils.ConvertToInternalUnits(data.ThresholdMeters, UnitTypeId.Meters);

            // ── Guard: top face ──────────────────────────────────────────────
            Face topFace = AutoSlopeGeometry.GetTopFace(roof);
            if (topFace == null)
            {
                data.Log?.Invoke(new LogEntry(LogLevel.Error, "Top face not found. Aborting."));
                FireFailure(data, "Top face not found.");
                return;
            }

            // ── Opt-in: insert real vertices at line/arc intersection points ──
            // (entry/exit points where a straight roof-shape edge partially
            // overlaps a boundary or opening arc). Only runs if the user has
            // enabled "Insert intersection points on curves" in the UI.
            // curveTolFt is declared here (not inside the block below) so the
            // SAME tolerance is later passed into DijkstraPathEngine — previously
            // that call used the constructor's default (~1mm) instead of this
            // ~2mm value, so a drain vertex matched via a looser drain-tolerance
            // could fail the arc "on-curve" check and miss the same-arc bypass.
            double curveTolFt = UnitUtils.ConvertToInternalUnits(2.0, UnitTypeId.Millimeters); // ~1-2mm tolerance
            List<Arc> boundaryArcs = null;
            if (data.InsertCurveIntersectionPoints)
            {
                boundaryArcs = AutoSlopeGeometry.GetBoundaryArcs(topFace);

                if (boundaryArcs.Count == 0)
                {
                    data.Log?.Invoke(new LogEntry(LogLevel.Info,
                        "Curve intersection check enabled, but no arc edges found on this roof."));
                }
                else
                {
                    var positions = new List<XYZ>(vertices.Count);
                    foreach (var v in vertices) positions.Add(v.Position);

                    // Only the roof's real physical edges are tested against the arcs —
                    // not arbitrary vertex pairs — so points are inserted only where
                    // actually required.
                    List<Line> boundaryLines = AutoSlopeGeometry.GetBoundaryLines(topFace);

                    int insertedCount;
                    using (Transaction tx = new Transaction(doc, "Insert Curve Intersection Points"))
                    {
                        tx.Start();
                        insertedCount = CurveIntersectionHelper.InsertIntersectionPoints(
                            editor, boundaryLines, positions, boundaryArcs, curveTolFt,
                            msg => data.Log?.Invoke(new LogEntry(LogLevel.Info, msg)));
                        tx.Commit();
                    }

                    if (insertedCount > 0)
                    {
                        // NOTE: no explicit doc.Regenerate() here — tx.Commit() above already
                        // regenerated the document. Calling Regenerate() again here runs with
                        // no transaction open and throws "Modification of the document is
                        // forbidden ... no open transaction."

                        // The commit above already regenerated the document, which invalidates
                        // the SlabShapeEditor handle acquired before it — re-fetch a fresh one
                        // before touching it again, per this suite's own rule on this.
                        editor = roof.GetSlabShapeEditor();
                        if (editor == null || !editor.IsValidObject)
                        {
                            data.Log?.Invoke(new LogEntry(LogLevel.Error,
                                "Slab shape editor became invalid after inserting curve points. Aborting."));
                            FireFailure(data, "Slab shape editor became invalid after inserting curve points.");
                            return;
                        }

                        // Re-collect vertices (now includes the newly inserted points)
                        // and re-acquire topFace + arcs, since geometry changed underneath.
                        vertices = new List<SlabShapeVertex>();
                        foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                            vertices.Add(v);

                        topFace = AutoSlopeGeometry.GetTopFace(roof);
                        if (topFace == null)
                        {
                            data.Log?.Invoke(new LogEntry(LogLevel.Error,
                                "Top face not found after inserting curve points. Aborting."));
                            FireFailure(data, "Top face not found after inserting curve points.");
                            return;
                        }
                        boundaryArcs = AutoSlopeGeometry.GetBoundaryArcs(topFace);
                    }
                }
            }

            // ── Build final drain points ─────────────────────────────────────
            List<XYZ> finalDrainPoints = data.DrainPoints ?? new List<XYZ>();

            data.Log?.Invoke(new LogEntry(LogLevel.Info,
                $"DEBUG: Initial drain points count = {finalDrainPoints.Count}"));

            if (data.EnableDrainTolerance && data.DrainToleranceMm > 0)
            {
                data.Log?.Invoke(new LogEntry(LogLevel.Info,
                    $"🔍 Checking for nearby roof shape points within {data.DrainToleranceMm}mm of selected points..."));

                finalDrainPoints = DrainDetectionHelper.DetectDrainsWithinRadius(
                    roof, finalDrainPoints, data.DrainToleranceMm, data.Log);

                finalDrainPoints = DrainDetectionHelper.RemoveDuplicates(
                    finalDrainPoints, data.DrainToleranceMm);

                data.Log?.Invoke(new LogEntry(LogLevel.Info,
                    $"DEBUG: After tolerance expansion count = {finalDrainPoints.Count}"));
            }

            if (finalDrainPoints == null || finalDrainPoints.Count == 0)
            {
                data.Log?.Invoke(new LogEntry(LogLevel.Error,
                    "No drain points are available. Aborting."));
                FireFailure(data, "No drain points are available.");
                return;
            }

            // ── Build Dijkstra graph ─────────────────────────────────────────
            var dijkstra = new DijkstraPathEngine(vertices, topFace, thresholdFt, boundaryArcs, curveTolFt);

            double drainMatchToleranceFt = data.EnableDrainTolerance && data.DrainToleranceMm > 0
                ? UnitUtils.ConvertToInternalUnits(data.DrainToleranceMm, UnitTypeId.Millimeters)
                : 0.001;

            var drainIndices = new HashSet<int>();
            for (int i = 0; i < vertices.Count; i++)
            {
                foreach (XYZ drainPoint in finalDrainPoints)
                {
                    if (drainPoint == null) continue;
                    if (vertices[i].Position.DistanceTo(drainPoint) <= drainMatchToleranceFt)
                    {
                        drainIndices.Add(i);
                        break;
                    }
                }
            }

            data.Log?.Invoke(new LogEntry(LogLevel.Info,
                $"DEBUG: drainIndices count (vertices matching drains) = {drainIndices.Count}"));

            if (drainIndices.Count == 0)
            {
                data.Log?.Invoke(new LogEntry(LogLevel.Error,
                    "No roof vertices matched the selected drain points. Aborting."));
                FireFailure(data, "No roof vertices matched the selected drain points.");
                return;
            }

            // ── OPTIMIZATION: Single multi-source Dijkstra ───────────────────
            double[] distances = dijkstra.ComputeAllDistances(drainIndices);

            // ── Ridge Point Detection (V021, opt-in) ─────────────────────────
            // Overrides distances[] for the handful of vertices selected as ridge
            // points, using the FARTHER adjacent group's per-group Dijkstra distance
            // instead of the standard nearest-drain-overall value computed above.
            // Everything else in the main slope loop below is untouched.
            var ridgeVertexRefGroup = new Dictionary<int, int>();   // vertexIndex -> farther group index
            var ridgeVertexPairIndex = new Dictionary<int, int>();  // vertexIndex -> pairwise edge's pair index (only set for edge-sourced ridge points)
            var ridgeVertexJunctionIndex = new Dictionary<int, int>(); // vertexIndex -> junction index (only set for junction-sourced ridge points)
            var ridgePairResults = new List<RidgePairResult>();
            var ridgeJunctionResults = new List<RidgeJunctionResult>();
            int ridgePairsSkipped = 0;

            if (data.EnableRidgePointDetection)
            {
                data.Log?.Invoke(new LogEntry(LogLevel.Info, "RIDGE: Ridge Point Detection is enabled."));

                double clusterToleranceFt = data.EnableDrainTolerance && data.DrainToleranceMm > 0
                    ? UnitUtils.ConvertToInternalUnits(data.DrainToleranceMm, UnitTypeId.Millimeters)
                    : UnitUtils.ConvertToInternalUnits(50.0, UnitTypeId.Millimeters); // sensible fallback if tolerance is off

                List<DrainGroup> groups = RidgePointEngine.ClusterDrainPoints(finalDrainPoints, clusterToleranceFt);
                RidgePointEngine.MatchGroupVertices(groups, vertices, drainMatchToleranceFt);

                data.Log?.Invoke(new LogEntry(LogLevel.Info,
                    $"RIDGE: Clustered {finalDrainPoints.Count} points into {groups.Count} drain group(s) (tol = {UnitUtils.ConvertFromInternalUnits(clusterToleranceFt, UnitTypeId.Millimeters):F0}mm)."));

                if (groups.Count < 2)
                {
                    data.Log?.Invoke(new LogEntry(LogLevel.Info,
                        "RIDGE: Fewer than 2 drain groups detected — no adjacent pairs possible. Skipping ridge detection."));
                }
                else
                {
                    List<GroupAdjacency> adjacencies = RidgePointEngine.BuildDelaunayAdjacency(groups, topFace);
                    data.Log?.Invoke(new LogEntry(LogLevel.Info,
                        $"RIDGE: Delaunay adjacency → {adjacencies.Count} group pair(s) found."));

                    // Voronoi diagram of group centers — the mathematically real
                    // boundary between each pair of groups' "territories". Empty
                    // (falls back per-pair below) for degenerate inputs: fewer
                    // than 3 groups total, or collinear group centers.
                    BoundingBoxXYZ roofBbox = RidgePointEngine.GetRoofBoundingBox(topFace);
                    var voronoiEdges = RidgePointEngine.BuildVoronoiEdges(groups, topFace, roofBbox);
                    data.Log?.Invoke(new LogEntry(LogLevel.Info,
                        voronoiEdges.Count > 0
                            ? $"RIDGE: Voronoi diagram built — {voronoiEdges.Count} edge(s) derived from group centers."
                            : "RIDGE: Voronoi diagram unavailable (coincident group centers) — using midpoint-perpendicular fallback for all pairs."));

                    // Cache one per-group multi-source Dijkstra distance array, computed
                    // lazily (only for groups that actually appear in a processed pair).
                    var perGroupDistances = new Dictionary<int, double[]>();
                    double[] GetGroupDistances(DrainGroup g)
                    {
                        if (!perGroupDistances.TryGetValue(g.GroupIndex, out var d))
                        {
                            var srcSet = new HashSet<int>(g.VertexIndices);
                            d = srcSet.Count > 0
                                ? dijkstra.ComputeAllDistances(srcSet)
                                : Enumerable.Repeat(double.PositiveInfinity, vertices.Count).ToArray();
                            perGroupDistances[g.GroupIndex] = d;
                        }
                        return d;
                    }

                    var groupsByIndex = groups.ToDictionary(g => g.GroupIndex, g => g);

                    // Edge tolerance — repurposed from the old "Corridor Width" field
                    // (confirmed): now a TIGHT proximity check against the real
                    // Voronoi edge/ridge line, default 100mm, rather than a wide
                    // search band around a straight midpoint-perpendicular line.
                    double edgeToleranceFt = UnitUtils.ConvertToInternalUnits(
                        data.RidgeCorridorWidthMm > 0 ? data.RidgeCorridorWidthMm : 100.0,
                        UnitTypeId.Millimeters);

                    // ── Fallback (built lazily, confirmed — only on first actual
                    // need, not upfront): when the normal tolerance search finds
                    // ZERO vertices for a pair/junction, fall back to a roof-
                    // vertex Delaunay triangulation + nearest-group classification.
                    // A roof vertex qualifies if its Delaunay neighbors include a
                    // MIX of at least 2 different groups from the pair/junction's
                    // group set (confirmed "mix" rule). ALL qualifying vertices
                    // are used (confirmed), same as a normal multi-match result.
                    RidgePointEngine.RoofVertexTopology roofTopology = null;
                    RidgePointEngine.RoofVertexTopology GetRoofTopology()
                        => roofTopology ??= new RidgePointEngine.RoofVertexTopology(vertices, topFace);

                    int ClassifyVertex(int vIdx)
                    {
                        // Nearest-drain-group classification (confirmed method) —
                        // reuses the same per-group Dijkstra distances already
                        // cached for elevation, no extra Dijkstra runs needed.
                        int bestGroup = -1;
                        double bestDist = double.PositiveInfinity;
                        foreach (var g in groups)
                        {
                            double d = GetGroupDistances(g)[vIdx];
                            if (d < bestDist) { bestDist = d; bestGroup = g.GroupIndex; }
                        }
                        return bestGroup;
                    }

                    // DIAGNOSTIC (temporary) — local, self-contained distance-to-
                    // segment helper for the fallback trace log below. Not the
                    // engine's real matching logic (that lives in RidgePointEngine
                    // and is unaffected), just a cheap re-derivation for logging.
                    double DistancePointToSegmentDiagnostic(XYZ p, XYZ a, XYZ b)
                    {
                        XYZ segVec = b - a;
                        double segLenSq = segVec.DotProduct(segVec);
                        if (segLenSq < 1e-12) return p.DistanceTo(a);
                        double t = (p - a).DotProduct(segVec) / segLenSq;
                        t = Math.Max(0.0, Math.Min(1.0, t));
                        return p.DistanceTo(a + segVec * t);
                    }

                    // Process shortest center-distance pairs first (confirmed default —
                    // deterministic winner when two pairs compete for the same vertex).
                    var orderedPairs = adjacencies.OrderBy(a => a.CenterDistanceFt).ToList();

                    int pairCounter = 0;
                    foreach (var adj in orderedPairs)
                    {
                        pairCounter++;
                        DrainGroup gA = groupsByIndex[adj.GroupAIndex];
                        DrainGroup gB = groupsByIndex[adj.GroupBIndex];

                        if (gA.VertexIndices.Count == 0 || gB.VertexIndices.Count == 0)
                        {
                            data.Log?.Invoke(new LogEntry(LogLevel.Warning,
                                $"RIDGE: Pair (G{gA.GroupIndex + 1},G{gB.GroupIndex + 1}) — one side has no matched roof vertex. Skipped."));
                            ridgePairsSkipped++;
                            continue;
                        }

                        var pairKey = adj.GroupAIndex < adj.GroupBIndex
                            ? (adj.GroupAIndex, adj.GroupBIndex)
                            : (adj.GroupBIndex, adj.GroupAIndex);
                        (XYZ, XYZ)? voronoiSegment = voronoiEdges.TryGetValue(pairKey, out var seg)
                            ? seg
                            : ((XYZ, XYZ)?)null;

                        RidgePairResult pairResult = RidgePointEngine.FindRidgePoints(
                            pairCounter, gA, gB, vertices, topFace, edgeToleranceFt, voronoiSegment);

                        double distFt = UnitUtils.ConvertFromInternalUnits(pairResult.CenterDistanceFt, UnitTypeId.Meters);
                        data.Log?.Invoke(new LogEntry(LogLevel.Info,
                            $"RIDGE: Pair (G{gA.GroupIndex + 1},G{gB.GroupIndex + 1}) processed — center dist {distFt:F2} m, " +
                            $"{(pairResult.UsedVoronoiEdge ? "Voronoi edge" : "fallback line")}, tolerance {data.RidgeCorridorWidthMm:0}mm."));

                        if (pairResult.Skipped)
                        {
                            // DIAGNOSTIC (temporary): log the ridge line's actual world
                            // coordinates and the closest roof vertex distance, so a
                            // fallback here is traceable — confirms whether the
                            // Voronoi-edge tolerance match is landing near real
                            // geometry or still off, without needing to attach a debugger.
                            double nearestMm = double.PositiveInfinity;
                            foreach (var sv in vertices)
                            {
                                double d = DistancePointToSegmentDiagnostic(sv.Position, pairResult.RidgeLineStart, pairResult.RidgeLineEnd);
                                if (d < nearestMm) nearestMm = d;
                            }
                            double nearestVertexMm = UnitUtils.ConvertFromInternalUnits(nearestMm, UnitTypeId.Millimeters);
                            data.Log?.Invoke(new LogEntry(LogLevel.Info,
                                $"RIDGE:   → DIAG: ridge line ({pairResult.RidgeLineStart.X:F2},{pairResult.RidgeLineStart.Y:F2},{pairResult.RidgeLineStart.Z:F2}) → " +
                                $"({pairResult.RidgeLineEnd.X:F2},{pairResult.RidgeLineEnd.Y:F2},{pairResult.RidgeLineEnd.Z:F2}), nearest roof vertex {nearestVertexMm:F0}mm away (tolerance {data.RidgeCorridorWidthMm:0}mm)."));

                            var fallbackMatches = GetRoofTopology().FindBoundaryVertices(
                                new List<int> { gA.GroupIndex, gB.GroupIndex }, ClassifyVertex);

                            if (fallbackMatches.Count == 0)
                            {
                                data.Log?.Invoke(new LogEntry(LogLevel.Info,
                                    "RIDGE:   → no roof vertex found near the ridge line, and no boundary vertex found in roof-vertex fallback either. Pair skipped — falls back to standard Dijkstra."));
                                ridgePairsSkipped++;
                                ridgePairResults.Add(pairResult);
                                continue;
                            }

                            data.Log?.Invoke(new LogEntry(LogLevel.Info,
                                $"RIDGE:   → no vertex within tolerance; roof-vertex boundary fallback found {fallbackMatches.Count} vertex(es)."));

                            foreach (int idx in fallbackMatches)
                            {
                                pairResult.MatchedVertexIndices.Add(idx);
                            }
                        }

                        double[] distA = GetGroupDistances(gA);
                        double[] distB = GetGroupDistances(gB);

                        data.Log?.Invoke(new LogEntry(LogLevel.Info,
                            $"RIDGE:   → {pairResult.MatchedVertexIndices.Count} vertex(es) found near ridge line."));

                        foreach (int vIdx in pairResult.MatchedVertexIndices)
                        {
                            double dToA = distA[vIdx];
                            double dToB = distB[vIdx];

                            if (double.IsInfinity(dToA) && double.IsInfinity(dToB))
                            {
                                data.Log?.Invoke(new LogEntry(LogLevel.Warning,
                                    $"RIDGE:   → vertex {vIdx}: no path to either group. Falling back to standard Dijkstra for this vertex."));
                                continue;
                            }

                            // Farther group wins (existing/default rule) — its nearest drain is the elevation reference.
                            bool aIsFarther = !double.IsInfinity(dToA) && (double.IsInfinity(dToB) || dToA >= dToB);
                            int fartherGroupIdx = aIsFarther ? gA.GroupIndex : gB.GroupIndex;

                            ridgeVertexRefGroup[vIdx] = fartherGroupIdx;

                            data.Log?.Invoke(new LogEntry(LogLevel.Info,
                                $"RIDGE:   → vertex {vIdx} @ ({vertices[vIdx].Position.X:F3}, {vertices[vIdx].Position.Y:F3}) — farther group = G{fartherGroupIdx + 1}."));

                            ridgeVertexPairIndex[vIdx] = pairResult.PairIndex;
                        }

                        ridgePairResults.Add(pairResult);
                    }

                    // ── Voronoi JUNCTIONS (3+ way meeting points) — processed
                    // AFTER pairwise edges, on purpose: junctions are more
                    // specific/significant, so under last-write-wins they must
                    // run last to reclaim priority over any pair that matched
                    // the same vertex. Ordered smallest-circumradius-first
                    // among themselves (confirmed default). Elevation
                    // reference = FARTHEST of the 3+ groups, extending the
                    // pairwise "farther wins" rule to N groups.
                    var junctions = RidgePointEngine.BuildVoronoiJunctions(groups, topFace);

                    if (junctions.Count > 0)
                    {
                        data.Log?.Invoke(new LogEntry(LogLevel.Info,
                            $"RIDGE: {junctions.Count} multi-group junction(s) found (3+ drain groups meeting at a point)."));

                        int junctionCounter = 0;
                        foreach (var (groupIndicesForJunction, junctionPoint, circumRadiusFt) in junctions)
                        {
                            junctionCounter++;

                            RidgeJunctionResult junctionResult = RidgePointEngine.FindJunctionRidgePoints(
                                junctionCounter, groupIndicesForJunction, junctionPoint, circumRadiusFt,
                                vertices, topFace, edgeToleranceFt);

                            string groupsLabel = string.Join(",", groupIndicesForJunction.Select(gi => $"G{gi + 1}"));

                            if (junctionResult.Skipped)
                            {
                                double nearestJMm = double.PositiveInfinity;
                                foreach (var sv in vertices)
                                {
                                    double d = sv.Position.DistanceTo(junctionPoint);
                                    if (d < nearestJMm) nearestJMm = d;
                                }
                                double nearestJVertexMm = UnitUtils.ConvertFromInternalUnits(nearestJMm, UnitTypeId.Millimeters);
                                data.Log?.Invoke(new LogEntry(LogLevel.Info,
                                    $"RIDGE:   → DIAG: junction point ({junctionPoint.X:F2},{junctionPoint.Y:F2},{junctionPoint.Z:F2}), " +
                                    $"nearest roof vertex {nearestJVertexMm:F0}mm away (tolerance {data.RidgeCorridorWidthMm:0}mm)."));

                                var fallbackMatches = GetRoofTopology().FindBoundaryVertices(
                                    groupIndicesForJunction, ClassifyVertex);

                                if (fallbackMatches.Count == 0)
                                {
                                    data.Log?.Invoke(new LogEntry(LogLevel.Info,
                                        $"RIDGE: Junction #{junctionCounter} ({groupsLabel}) — no roof vertex found near junction point, and no boundary vertex found in roof-vertex fallback either. Skipped."));
                                    continue;
                                }

                                data.Log?.Invoke(new LogEntry(LogLevel.Info,
                                    $"RIDGE: Junction #{junctionCounter} ({groupsLabel}) — no vertex within tolerance; roof-vertex boundary fallback found {fallbackMatches.Count} vertex(es)."));

                                foreach (int idx in fallbackMatches)
                                {
                                    junctionResult.MatchedVertexIndices.Add(idx);
                                }
                            }

                            data.Log?.Invoke(new LogEntry(LogLevel.Info,
                                $"RIDGE: Junction #{junctionCounter} ({groupsLabel}) — {junctionResult.MatchedVertexIndices.Count} vertex(es) found."));

                            // Farthest of the N groups wins (existing/default rule, extended from pairwise).
                            var junctionGroupDistances = groupIndicesForJunction
                                .Select(gi => (GroupIdx: gi, Dist: GetGroupDistances(groupsByIndex[gi])))
                                .ToList();

                            foreach (int vIdx in junctionResult.MatchedVertexIndices)
                            {
                                var validDistances = junctionGroupDistances
                                    .Select(g => (g.GroupIdx, Dist: g.Dist[vIdx]))
                                    .Where(g => !double.IsInfinity(g.Dist))
                                    .ToList();

                                if (validDistances.Count == 0)
                                {
                                    data.Log?.Invoke(new LogEntry(LogLevel.Warning,
                                        $"RIDGE:   → vertex {vIdx}: no path to any of the {groupIndicesForJunction.Count} groups. Falling back to standard Dijkstra for this vertex."));
                                    continue;
                                }

                                var farthest = validDistances.OrderByDescending(g => g.Dist).First();

                                // Overwrites any pairwise assignment already made for
                                // this vertex above — junctions win on overlap (last-write-wins).
                                ridgeVertexRefGroup[vIdx] = farthest.GroupIdx;
                                ridgeVertexJunctionIndex[vIdx] = junctionResult.JunctionIndex;
                                ridgeVertexPairIndex.Remove(vIdx);

                                data.Log?.Invoke(new LogEntry(LogLevel.Info,
                                    $"RIDGE:   → vertex {vIdx} @ ({vertices[vIdx].Position.X:F3}, {vertices[vIdx].Position.Y:F3}) — farthest group = G{farthest.GroupIdx + 1} (of {groupIndicesForJunction.Count})."));
                            }

                            ridgeJunctionResults.Add(junctionResult);
                        }
                    }

                    // Apply the farther-group distance override for every resolved ridge vertex.
                    foreach (var kvp in ridgeVertexRefGroup)
                    {
                        int vIdx = kvp.Key;
                        int groupIdx = kvp.Value;
                        double[] groupDist = perGroupDistances[groupIdx];
                        distances[vIdx] = groupDist[vIdx];
                    }

                    int resolvedRidgeCount = ridgeVertexRefGroup.Count;
                    data.Log?.Invoke(new LogEntry(LogLevel.Info,
                        $"RIDGE: {resolvedRidgeCount} ridge point(s) resolved, {ridgePairsSkipped} pair(s) skipped."));

                    // IMPORTANT: ComputeAllDistances mutates DijkstraPathEngine's internal
                    // _lastPred (used by DescribePathEdgeTypes below, for the pre-existing
                    // ARC-DEBUG log). The per-group calls above overwrote it with the LAST
                    // group's predecessor tree. Re-run against the original overall drain
                    // set so the arc diagnostic below still describes the real routes.
                    // distances[] itself is untouched by this call — only _lastPred resets.
                    dijkstra.ComputeAllDistances(drainIndices);
                }
            }

            // ── Diagnostic: on-arc vertex path breakdown ─────────────────────
            // Read-only logging pass, no document modification — safe outside
            // any transaction. Reports which vertices sit on an arc, their
            // resolved path length to the nearest drain, and whether that path
            // used same-arc traversal, a tangent-route detour, straight chords,
            // or a mix of these across its hops.
            int onArcCount = dijkstra.OnArcVertexCount;
            data.Log?.Invoke(new LogEntry(LogLevel.Info,
                $"ARC-DEBUG: {onArcCount} vertex(es) classified as on-arc (tol = curveTolFt)."));

            for (int i = 0; i < vertices.Count; i++)
            {
                if (!dijkstra.IsOnArc(i)) continue;

                double pathFtArc = distances[i];
                string pathDesc = dijkstra.DescribePathEdgeTypes(i);
                string pathStr = double.IsInfinity(pathFtArc)
                    ? "no path found"
                    : $"{UnitUtils.ConvertFromInternalUnits(pathFtArc, UnitTypeId.Meters):F3} m";

                data.Log?.Invoke(new LogEntry(LogLevel.Info,
                    $"ARC-DEBUG: vertex {i} @ ({vertices[i].Position.X:F3}, {vertices[i].Position.Y:F3}) — path = {pathStr} — route: {pathDesc}"));
            }

            // ── Main slope loop ──────────────────────────────────────────────
            int processed = 0, skipped = 0;
            double maxPathFt = 0;
            var vertexDataList = new List<VertexData>();
            Stopwatch sw = Stopwatch.StartNew();

            double drainBaselineZFt = drainIndices.Count > 0
                ? drainIndices.Average(idx => vertices[idx].Position.Z)
                : 0;

            using (Transaction tx = new Transaction(doc, "Apply AutoSlope"))
            {
                tx.Start();

                for (int i = 0; i < vertices.Count; i++)
                {
                    double pathFt = distances[i];

                    if (double.IsInfinity(pathFt) || pathFt > thresholdFt)
                    {
                        skipped++;

                        if (data.ExportConfig?.IncludeVertexDetails == true)
                        {
                            vertexDataList.Add(new VertexData
                            {
                                VertexIndex       = i,
                                Position          = vertices[i].Position,
                                PathLengthMeters  = double.IsInfinity(pathFt) ? 0
                                    : UnitUtils.ConvertFromInternalUnits(pathFt, UnitTypeId.Meters),
                                ElevationOffsetMm      = 0,
                                ElevationFromModel_mm  = 0,
                                NearestDrainIndex      = -1,
                                DirectionVector        = XYZ.Zero,
                                WasProcessed           = false
                            });
                        }
                        continue;
                    }

                    double elevFt = pathFt * slopeFactor;
                    editor.ModifySubElement(vertices[i], elevFt);

                    processed++;
                    if (pathFt > maxPathFt) maxPathFt = pathFt;

                    int nearestDrainIndex = FindNearestDrainIndex(vertices[i].Position, finalDrainPoints);
                    XYZ directionVector   = nearestDrainIndex >= 0
                        ? CalculateDirectionVector(vertices[i].Position, finalDrainPoints[nearestDrainIndex])
                        : XYZ.Zero;

                    bool isRidge = ridgeVertexRefGroup.TryGetValue(i, out int ridgeRefGroup);

                    bool isJunctionSourced = isRidge && ridgeVertexJunctionIndex.ContainsKey(i);
                    bool isPairSourced = isRidge && ridgeVertexPairIndex.ContainsKey(i);

                    vertexDataList.Add(new VertexData
                    {
                        VertexIndex       = i,
                        Position          = vertices[i].Position,
                        PathLengthMeters  = UnitUtils.ConvertFromInternalUnits(pathFt, UnitTypeId.Meters),
                        ElevationOffsetMm = UnitUtils.ConvertFromInternalUnits(elevFt, UnitTypeId.Millimeters),
                        ElevationFromModel_mm = 0,
                        NearestDrainIndex = nearestDrainIndex,
                        DirectionVector   = directionVector,
                        WasProcessed      = true,
                        IsRidgePoint            = isRidge,
                        RidgePairIndex          = isPairSourced ? ridgeVertexPairIndex[i] : -1,
                        IsJunctionPoint         = isJunctionSourced,
                        RidgeJunctionIndex      = isJunctionSourced ? ridgeVertexJunctionIndex[i] : -1,
                        RidgeReferenceGroupIndex = isRidge ? ridgeRefGroup : -1
                    });
                }

                // ── Ridge Point Detection: mark resolved ridge points in active view ──
                // (V021, opt-in sub-toggle of EnableRidgePointDetection, on by default
                // whenever ridge detection itself is enabled.) Must run HERE — inside
                // this still-open "Apply AutoSlope" transaction — since it uses a
                // SubTransaction internally, and a SubTransaction requires an already
                // open host Transaction or Revit throws. Non-fatal on any failure.
                if (data.EnableRidgePointDetection && data.MarkRidgePointsInView)
                {
                    var ridgePositions = vertexDataList
                        .Where(v => v.IsRidgePoint)
                        .Select(v => v.Position)
                        .ToList();

                    if (ridgePositions.Count == 0)
                    {
                        data.Log?.Invoke(new LogEntry(LogLevel.Info,
                            "RIDGE-MARK: No ridge points resolved this run — nothing to mark."));
                    }
                    else
                    {
                        View activeView = app.ActiveUIDocument?.ActiveView;
                        data.Log?.Invoke(new LogEntry(LogLevel.Info,
                            $"RIDGE-MARK: Marking {ridgePositions.Count} ridge point(s) in active view '{activeView?.Name ?? "(none)"}'..."));

                        int circlesDrawn = RidgePointMarker.DrawRidgePointCircles(
                            doc, activeView, ridgePositions,
                            data.MarkerLineStyleName, data.MarkerColorName, data.RidgePointCircleRadiusMm,
                            data.Log);

                        if (circlesDrawn > 0)
                            data.Log?.Invoke(new LogEntry(LogLevel.Success,
                                $"✅ RIDGE-MARK: Drew {circlesDrawn} ridge-point circle(s) ({data.RidgePointCircleRadiusMm:0}mm radius, {data.MarkerColorName}, \"{data.MarkerLineStyleName}\")."));
                        else
                            data.Log?.Invoke(new LogEntry(LogLevel.Warning,
                                "RIDGE-MARK: No circles were drawn (see warnings above)."));
                    }
                }

                tx.Commit();
            }

            // ── Re-read vertices from Revit after commit ─────────────────────
            double maxElevFt = 0;
            var refreshedVertices = new List<SlabShapeVertex>();
            foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                refreshedVertices.Add(v);

            data.Log?.Invoke(new LogEntry(LogLevel.Info,
                $"DEBUG: Refreshed vertex count after commit = {refreshedVertices.Count}"));

            var refreshedZByIndex = new Dictionary<int, double>();
            for (int i = 0; i < refreshedVertices.Count; i++)
            {
                for (int j = 0; j < vertices.Count; j++)
                {
                    double xyDist = Math.Sqrt(
                        Math.Pow(refreshedVertices[i].Position.X - vertices[j].Position.X, 2) +
                        Math.Pow(refreshedVertices[i].Position.Y - vertices[j].Position.Y, 2));

                    if (xyDist < 0.001)
                    {
                        refreshedZByIndex[j] = refreshedVertices[i].Position.Z;
                        break;
                    }
                }
            }

            foreach (var vd in vertexDataList)
            {
                if (!vd.WasProcessed) continue;

                if (refreshedZByIndex.TryGetValue(vd.VertexIndex, out double refreshedZFt))
                {
                    double elevFromModelFt = refreshedZFt - drainBaselineZFt;
                    vd.ElevationFromModel_mm = UnitUtils.ConvertFromInternalUnits(
                        elevFromModelFt, UnitTypeId.Millimeters);

                    if (elevFromModelFt > maxElevFt) maxElevFt = elevFromModelFt;
                }
                else
                {
                    vd.ElevationFromModel_mm = vd.ElevationOffsetMm;
                    data.Log?.Invoke(new LogEntry(LogLevel.Warning,
                        $"WARN: Could not match refreshed vertex for index {vd.VertexIndex}, using calculated value."));
                }
            }

            data.Log?.Invoke(new LogEntry(LogLevel.Info,
                $"DEBUG: maxElevFt from model re-read = {maxElevFt:F6} ft"));

            sw.Stop();

            int    highest_mm  = (int)Math.Round(
                UnitUtils.ConvertFromInternalUnits(maxElevFt, UnitTypeId.Millimeters),
                MidpointRounding.AwayFromZero);
            double longest_m   = Math.Round(
                UnitUtils.ConvertFromInternalUnits(maxPathFt, UnitTypeId.Meters),
                2, MidpointRounding.AwayFromZero);
            int    durationSec = (int)Math.Round(sw.Elapsed.TotalSeconds);
            string runDate     = DateTime.Now.ToString("dd-MM-yy HH:mm");

            const string toolVersion = "P.10.00";

            int statusCode = AutoSlopeParameterWriter.WriteAll(
                doc, roof, data,
                highest_mm, maxPathFt,
                processed, skipped, durationSec,
                finalDrainPoints.Count,
                runDate,
                toolVersion);

            string compactPath = null;
            if (data.ExportConfig?.ExportToExcel == true)
            {
                compactPath = ExcelExportService.ExportCompactVertexData(
                    data, vertexDataList, roof, data.SlopePercent,
                    toolVersion, statusCode, ridgePairResults, ridgeJunctionResults);

                if (!string.IsNullOrEmpty(compactPath))
                {
                    data.Log?.Invoke(new LogEntry(LogLevel.Success,
                        $"✅ Compact Excel exported to: {compactPath}"));
                    data.Log?.Invoke(new LogEntry(LogLevel.Info,
                        "  • Sorted by PathLength_Meters (longest first)"));
                    data.Log?.Invoke(new LogEntry(LogLevel.Info,
                        $"  • Contains {processed} processed vertices"));

                    var longestVertex = vertexDataList
                        .Where(v => v.WasProcessed)
                        .OrderByDescending(v => v.PathLengthMeters)
                        .FirstOrDefault();

                    if (longestVertex != null)
                        data.Log?.Invoke(new LogEntry(LogLevel.Info,
                            $"  • Longest path: {longestVertex.PathLengthMeters:F2} m to drain {longestVertex.NearestDrainIndex}"));
                }

                if (data.ExportConfig.IncludeVertexDetails)
                {
                    string detailedPath = ExcelExportService.ExportDetailedVertexData(
                        data, vertexDataList, roof, finalDrainPoints, data.SlopePercent, ridgePairResults, ridgeJunctionResults);

                    if (!string.IsNullOrEmpty(detailedPath))
                    {
                        data.Log?.Invoke(new LogEntry(LogLevel.Success,
                            $"✅ Detailed Excel exported to: {detailedPath}"));
                        data.Log?.Invoke(new LogEntry(LogLevel.Info,
                            $"  • {vertexDataList.Count} total vertices ({processed} processed, {skipped} skipped)"));
                        // NOTE: sheet list corrected to match what's actually written —
                        // this workbook has always been "Vertex Data" + "Run Summary"
                        // (ExportDetailedVertexData delegates to the compact export);
                        // the previous log line claimed 4 sheets that were never produced.
                        var sheetNames = new List<string> { "Vertex Data", "Run Summary" };
                        if (data.EnableRidgePointDetection && ridgePairResults?.Count > 0)
                            sheetNames.Add("Ridge Points");
                        if (data.EnableRidgePointDetection && ridgeJunctionResults?.Count > 0)
                            sheetNames.Add("Ridge Junctions");
                        string sheetList = string.Join(", ", sheetNames);
                        data.Log?.Invoke(new LogEntry(LogLevel.Info,
                            $"  • Sheets: {sheetList}"));
                    }
                }
            }

            // ── Summary ──────────────────────────────────────────────────────
            data.Log?.Invoke(new LogEntry(LogLevel.Info,    "===== AutoSlope Summary ====="));
            data.Log?.Invoke(new LogEntry(LogLevel.Success, $"Applied Slope Percentage : {data.SlopePercent}%"));
            data.Log?.Invoke(new LogEntry(LogLevel.Success, $"Vertices Processed       : {processed}"));
            data.Log?.Invoke(new LogEntry(LogLevel.Warning, $"Vertices Skipped         : {skipped}"));
            data.Log?.Invoke(new LogEntry(LogLevel.Info,    $"Highest Elevation        : {highest_mm:0} mm  ← from model re-read"));
            data.Log?.Invoke(new LogEntry(LogLevel.Info,    $"Longest Path             : {longest_m:0.00} m"));
            data.Log?.Invoke(new LogEntry(LogLevel.Info,    $"Picked Drain Count       : {data.PickedDrainPoints?.Count ?? 0}"));
            data.Log?.Invoke(new LogEntry(LogLevel.Info,    $"Final Drain Count        : {finalDrainPoints.Count}"));
            data.Log?.Invoke(new LogEntry(LogLevel.Info,    $"Run Duration             : {durationSec} sec"));
            data.Log?.Invoke(new LogEntry(LogLevel.Info,    $"Run Date                 : {runDate}"));
            if (data.EnableDrainTolerance)
                data.Log?.Invoke(new LogEntry(LogLevel.Info,
                    $"Drain Tolerance          : {data.DrainToleranceMm} mm (enabled)"));
            if (data.EnableRidgePointDetection)
            {
                data.Log?.Invoke(new LogEntry(LogLevel.Info,
                    $"Ridge Points Resolved    : {ridgeVertexRefGroup.Count}"));
                data.Log?.Invoke(new LogEntry(LogLevel.Info,
                    $"Ridge Pairs Skipped      : {ridgePairsSkipped}"));
                data.Log?.Invoke(new LogEntry(LogLevel.Info,
                    $"Ridge Junctions Found    : {ridgeJunctionResults.Count(j => !j.Skipped)} (of {ridgeJunctionResults.Count} candidate junction(s))"));
            }
            data.Log?.Invoke(new LogEntry(LogLevel.Success, "===== AutoSlope Finished Successfully ====="));

            data.OnCompleted?.Invoke(new AutoSlopeResult
            {
                Success           = true,
                VerticesProcessed = processed,
                VerticesSkipped   = skipped,
                PickedDrainCount  = data.PickedDrainPoints?.Count ?? 0,
                FinalDrainCount   = finalDrainPoints.Count,
                HighestElevation_mm = highest_mm,
                LongestPath_m     = longest_m,
                RunDuration_sec   = durationSec,
                RunDate           = runDate,
                Version           = toolVersion,
                Status            = statusCode,
                ExportedFilePath  = compactPath,
                RidgePointsResolved = ridgeVertexRefGroup.Count,
                RidgePairsSkipped   = ridgePairsSkipped,
                RidgeJunctionsResolved = ridgeJunctionResults.Count(j => !j.Skipped)
            });
        }

        private static void FireFailure(AutoSlopePayload data, string reason)
        {
            data.Log?.Invoke(new LogEntry(LogLevel.Error, reason));
            data.Log?.Invoke(new LogEntry(LogLevel.Error, "DEBUG: Firing failure callback"));
            data.OnCompleted?.Invoke(new AutoSlopeResult
            {
                Success      = false,
                ErrorMessage = reason,
                PickedDrainCount = 0,
                FinalDrainCount  = 0
            });
        }

        private static int FindNearestDrainIndex(XYZ vertexPos, List<XYZ> drainPoints)
        {
            if (drainPoints == null || drainPoints.Count == 0) return -1;
            int nearestIndex = 0;
            double minDistance = double.MaxValue;
            for (int i = 0; i < drainPoints.Count; i++)
            {
                if (drainPoints[i] == null) continue;
                double d = vertexPos.DistanceTo(drainPoints[i]);
                if (d < minDistance) { minDistance = d; nearestIndex = i; }
            }
            return nearestIndex;
        }

        private static XYZ CalculateDirectionVector(XYZ fromPoint, XYZ toPoint)
        {
            if (fromPoint.DistanceTo(toPoint) < 0.001) return XYZ.Zero;
            return (toPoint - fromPoint).Normalize();
        }
    }
}
