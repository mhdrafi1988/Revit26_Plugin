// =======================================================
// File: AutoSlopeEngine.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.RPF_001
// Changes vs V06:
//   - LogColorHelper removed entirely.
//   - All data.Log() calls now emit new LogEntry(LogLevel.X, "...")
//     so colour is driven by LogLevelToColorConverter in the UI.
// =======================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlopeByPointRPF.V002.RPF_001.Core.Models;
using Revit26_Plugin.AutoSlopeByPointRPF.V002.RPF_001.Core.Parameters;
using Revit26_Plugin.AutoSlopeByPointRPF.V002.RPF_001.Infrastructure.Helpers;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByPointRPF.V002.RPF_001.Core.Engine
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

            // ── Ridge Point Detection (new in RPF_001, opt-in) ────────────────
            // Produces a per-vertex elevation OVERRIDE dictionary
            // (ridgeElevationOverrideFt) that the main slope loop below
            // consults before falling back to the standard pathFt × slope
            // formula. Vertices not present in the dictionary are entirely
            // unaffected by this feature — same behavior as before.
            var ridgeElevationOverrideFt = new Dictionary<int, double>();
            int totalRidgePointsFound = 0;

            // drainBaselineZFt: moved up from its original location (just before
            // the main slope loop) since ridge logging below references it for
            // context; still used, unchanged, by the model re-read comparison
            // further down this method.
            double drainBaselineZFt = drainIndices.Count > 0
                ? drainIndices.Average(idx => vertices[idx].Position.Z)
                : 0;

            if (data.EnableRidgePoints)
            {
                data.Log?.Invoke(new LogEntry(LogLevel.Info, "── Ridge Point Detection: starting ──"));

                double ridgeToleranceFt = UnitUtils.ConvertToInternalUnits(data.RidgeToleranceMm, UnitTypeId.Millimeters);

                // Reuse the same tolerance already used for drain-tolerance
                // expansion as the group-clustering radius — no separate
                // "group tolerance" input, per confirmed spec.
                double clusterToleranceFt = data.EnableDrainTolerance && data.DrainToleranceMm > 0
                    ? UnitUtils.ConvertToInternalUnits(data.DrainToleranceMm, UnitTypeId.Millimeters)
                    : UnitUtils.ConvertToInternalUnits(50, UnitTypeId.Millimeters); // fallback default when drain tolerance is off

                List<DrainGroup> drainGroups = RidgePointEngine.ClusterDrainPoints(finalDrainPoints, clusterToleranceFt);

                data.Log?.Invoke(new LogEntry(LogLevel.Info,
                    $"Ridge: clustered {finalDrainPoints.Count} drain point(s) into {drainGroups.Count} group(s) " +
                    $"(cluster tolerance = {data.DrainToleranceMm}mm)."));

                if (drainGroups.Count < 2)
                {
                    data.Log?.Invoke(new LogEntry(LogLevel.Info,
                        "Ridge: fewer than 2 drain groups — no ridge lines possible, skipping ridge detection."));
                }
                else
                {
                    var basis = new PlaneBasis(vertices[drainIndices.First()].Position, GetFaceNormal(topFace));

                    // Per-group vertex-index sets (retained for traceability/
                    // future debugging per this suite's logging-depth standard,
                    // even though only .Count is logged below) and per-group
                    // Dijkstra distances — reuses the SAME DijkstraPathEngine
                    // graph already built above, just called once per group
                    // instead of once for all drains combined.
                    var groupVertexIndexSets = new List<HashSet<int>>();
                    var groupDistances = new List<double[]>();

                    foreach (var group in drainGroups)
                    {
                        var idxSet = new HashSet<int>();
                        for (int vi = 0; vi < vertices.Count; vi++)
                        {
                            foreach (XYZ gp in group.Points)
                            {
                                if (vertices[vi].Position.DistanceTo(gp) <= drainMatchToleranceFt)
                                {
                                    idxSet.Add(vi);
                                    break;
                                }
                            }
                        }
                        groupVertexIndexSets.Add(idxSet);
                        groupDistances.Add(idxSet.Count > 0
                            ? dijkstra.ComputeAllDistances(idxSet)
                            : Enumerable.Repeat(double.PositiveInfinity, vertices.Count).ToArray());

                        data.Log?.Invoke(new LogEntry(LogLevel.Info,
                            $"Ridge: group {group.Index} — {group.Points.Count} drain point(s), " +
                            $"{idxSet.Count} matched roof vertex(es)."));
                    }

                    // classify(vertexIndex): nearest group by Dijkstra distance, or -1 if unreachable from all groups.
                    int Classify(int vertexIndex)
                    {
                        int best = -1;
                        double bestDist = double.PositiveInfinity;
                        for (int gi = 0; gi < groupDistances.Count; gi++)
                        {
                            double d = groupDistances[gi][vertexIndex];
                            if (d < bestDist) { bestDist = d; best = gi; }
                        }
                        return best;
                    }

                    double roofSpanFt = GetRoofSpanFt(vertices);
                    var voronoiEdges = RidgePointEngine.BuildVoronoiEdges(drainGroups, basis, roofSpanFt);
                    var ridgePairLines = RidgePointEngine.BuildRidgeLinesForPairs(drainGroups, basis, voronoiEdges, roofSpanFt);

                    data.Log?.Invoke(new LogEntry(LogLevel.Info,
                        $"Ridge: {ridgePairLines.Count} adjacent group pair(s) found " +
                        $"({ridgePairLines.Count(p => p.UsedVoronoi)} real Voronoi edge(s), " +
                        $"{ridgePairLines.Count(p => !p.UsedVoronoi)} fallback bisector(s))."));

                    var claimed = new HashSet<int>();
                    var claimedByJunction = new HashSet<int>();
                    var pairResults = new List<RidgePairResult>();

                    // ── Pairwise ridge matching (shortest group-distance first) ──
                    foreach (var (a, b, start, end, usedVoronoi) in ridgePairLines)
                    {
                        var matches = RidgePointEngine.FindMatchingVertices(
                            vertices, start, end, ridgeToleranceFt,
                            drainIndices, claimed, Classify, new[] { a, b });

                        var pr = new RidgePairResult
                        {
                            GroupAIndex = a,
                            GroupBIndex = b,
                            RidgeLineStart = start,
                            RidgeLineEnd = end,
                            UsedVoronoiEdge = usedVoronoi,
                            MatchedVertexIndices = matches,
                            TrueSkipped = matches.Count == 0
                        };
                        pairResults.Add(pr);

                        if (pr.TrueSkipped)
                        {
                            data.Log?.Invoke(new LogEntry(LogLevel.Info,
                                $"Ridge: pair (group {a}, group {b}) — no roof vertex within {data.RidgeToleranceMm}mm of ridge line. " +
                                $"Falls back to standard Dijkstra slope for this pair's territory."));
                            continue;
                        }

                        foreach (int vi in matches)
                            claimed.Add(vi);

                        data.Log?.Invoke(new LogEntry(LogLevel.Info,
                            $"Ridge: pair (group {a}, group {b}) — matched {matches.Count} roof vertex(es)."));
                    }

                    // ── Junction matching (processed LAST — junctions win over pairwise claims) ──
                    var junctions = RidgePointEngine.FindJunctions(drainGroups, basis);
                    var junctionGroupsByVertex = new Dictionary<int, List<int>>();

                    foreach (var (groupIndices, point) in junctions)
                    {
                        // Junctions may OVERWRITE existing pairwise claims — pass an
                        // empty claimed-set so pairwise-claimed vertices are still
                        // eligible here, per the confirmed "junctions always win" rule.
                        // Uses a true point-radius test (FindMatchingVerticesNearPoint),
                        // NOT a segment test — a junction is a single meeting point,
                        // and testing against a segment along an arbitrary direction
                        // would produce an elongated, orientation-biased search area
                        // instead of a circular one centered on the junction.
                        var matches = RidgePointEngine.FindMatchingVerticesNearPoint(
                            vertices, point, ridgeToleranceFt,
                            drainIndices, new HashSet<int>(),
                            Classify, groupIndices);

                        if (matches.Count == 0) continue;

                        foreach (int vi in matches)
                        {
                            claimed.Add(vi);
                            claimedByJunction.Add(vi);
                            junctionGroupsByVertex[vi] = groupIndices;
                        }

                        data.Log?.Invoke(new LogEntry(LogLevel.Info,
                            $"Ridge: junction of groups [{string.Join(",", groupIndices)}] — matched {matches.Count} roof vertex(es) (overwrites any pairwise claim)."));
                    }

                    // ── Elevation: FartherGroup rule ──────────────────────────────
                    // Ridge point offset = slope × max(DijkstraDist over the
                    // groups meeting at this vertex's ridge/junction) — same
                    // "offset from reset-to-0 position" convention that
                    // ModifySubElement uses everywhere else in this engine
                    // (pathFt × slopeFactor for normal vertices).
                    foreach (var pr in pairResults.Where(p => !p.TrueSkipped))
                    {
                        foreach (int vi in pr.MatchedVertexIndices)
                        {
                            if (claimedByJunction.Contains(vi)) continue; // junction overrides this pairwise claim — handled below instead
                            double distA = groupDistances[pr.GroupAIndex][vi];
                            double distB = groupDistances[pr.GroupBIndex][vi];
                            double farDist = Math.Max(distA, distB);
                            if (double.IsInfinity(farDist)) continue;
                            ridgeElevationOverrideFt[vi] = farDist * slopeFactor;
                        }
                    }

                    foreach (var kvp in junctionGroupsByVertex)
                    {
                        int vi = kvp.Key;
                        List<int> involvedGroups = kvp.Value;
                        double farDist = involvedGroups
                            .Select(gi => groupDistances[gi][vi])
                            .Where(d => !double.IsInfinity(d))
                            .DefaultIfEmpty(double.PositiveInfinity)
                            .Max();
                        if (double.IsInfinity(farDist)) continue;
                        ridgeElevationOverrideFt[vi] = farDist * slopeFactor;
                    }

                    totalRidgePointsFound = ridgeElevationOverrideFt.Count;

                    data.Log?.Invoke(new LogEntry(LogLevel.Success,
                        $"Ridge Point Detection: {totalRidgePointsFound} total ridge point(s) found."));
                }
            }

            // ── Main slope loop ──────────────────────────────────────────────
            int processed = 0, skipped = 0;
            double maxPathFt = 0;
            var vertexDataList = new List<VertexData>();
            Stopwatch sw = Stopwatch.StartNew();

            CircleMarkerService.PlacementCounts markerCounts = null;

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

                    // Ridge point override: if vertex i was matched as a ridge
                    // point, use its FartherGroup elevation (farDist × slope,
                    // where farDist is the LARGER of the two/more groups'
                    // Dijkstra distances) instead of the standard single-source
                    // pathFt × slope value. Vertices not in
                    // ridgeElevationOverrideFt are entirely unaffected —
                    // identical behavior to before this feature existed.
                    // Both branches feed ModifySubElement the same kind of
                    // value: an OFFSET from the vertex's current (reset-to-0)
                    // position, in internal feet.
                    bool isRidgePoint = ridgeElevationOverrideFt.TryGetValue(i, out double ridgeOffsetFt);
                    double elevFt = isRidgePoint
                        ? ridgeOffsetFt
                        : pathFt * slopeFactor;

                    editor.ModifySubElement(vertices[i], elevFt);

                    processed++;
                    // For ridge points, the governing distance is the FartherGroup
                    // distance (ridgeOffsetFt / slopeFactor), which can exceed the
                    // single-source pathFt used elsewhere — use the larger of the
                    // two so "Longest Path" reflects the true governing distance
                    // rather than under-reporting for ridge vertices.
                    double effectivePathFt = isRidgePoint && slopeFactor > 0
                        ? Math.Max(pathFt, ridgeOffsetFt / slopeFactor)
                        : pathFt;
                    if (effectivePathFt > maxPathFt) maxPathFt = effectivePathFt;

                    int nearestDrainIndex = FindNearestDrainIndex(vertices[i].Position, finalDrainPoints);
                    XYZ directionVector   = nearestDrainIndex >= 0
                        ? CalculateDirectionVector(vertices[i].Position, finalDrainPoints[nearestDrainIndex])
                        : XYZ.Zero;

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
                        IsRidgePoint      = isRidgePoint
                    });
                }

                // ── Circle Markers (V026) ────────────────────────────────────
                // Runs inside this same transaction (one commit, one undo step
                // for the whole Run) — see CircleMarkerService header comment
                // for why Highest Point uses ElevationOffsetMm here.
                View activeView = app.ActiveUIDocument?.ActiveView;
                markerCounts = CircleMarkerService.PlaceMarkers(
                    doc,
                    activeView,
                    finalDrainPoints,
                    vertexDataList,
                    data.DrainMarkerGroup,
                    data.HighestPointMarkerGroup,
                    data.AllowedOffsetMarkerGroup,
                    data.AllowedOffsetThresholdMm,
                    data.Log);

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
                    toolVersion, statusCode);

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
                        data, vertexDataList, roof, finalDrainPoints, data.SlopePercent);

                    if (!string.IsNullOrEmpty(detailedPath))
                    {
                        data.Log?.Invoke(new LogEntry(LogLevel.Success,
                            $"✅ Detailed Excel exported to: {detailedPath}"));
                        data.Log?.Invoke(new LogEntry(LogLevel.Info,
                            $"  • {vertexDataList.Count} total vertices ({processed} processed, {skipped} skipped)"));
                        data.Log?.Invoke(new LogEntry(LogLevel.Info,
                            "  • Sheets: Summary, Drain Points, Vertices, Statistics"));
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
            data.Log?.Invoke(new LogEntry(LogLevel.Info,
                $"Circles Placed            : {markerCounts?.DrainCirclesPlaced ?? 0} drain, " +
                $"{markerCounts?.HighestCirclesPlaced ?? 0} highest, " +
                $"{markerCounts?.OffsetCirclesPlaced ?? 0} allowed-offset"));
            if (data.EnableRidgePoints)
                data.Log?.Invoke(new LogEntry(LogLevel.Info,
                    $"Ridge Points Found        : {totalRidgePointsFound}"));
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
                CurvesCalculated  = boundaryArcs?.Count ?? 0,
                Version           = toolVersion,
                Status            = statusCode,
                ExportedFilePath  = compactPath,
                DrainCirclesPlaced   = markerCounts?.DrainCirclesPlaced ?? 0,
                HighestCirclesPlaced = markerCounts?.HighestCirclesPlaced ?? 0,
                OffsetCirclesPlaced  = markerCounts?.OffsetCirclesPlaced ?? 0,
                TotalRidgePointsFound = totalRidgePointsFound
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

        // ── Ridge Point Detection helpers (new in RPF_001) ────────────────

        /// <summary>
        /// Face normal evaluated at the face's own UV bounding-box midpoint —
        /// used only to build a genuine orthonormal PlaneBasis (see
        /// RidgePointEngine.cs) for ridge geometry math; never used for
        /// distance/area calculations directly.
        /// </summary>
        private static XYZ GetFaceNormal(Face face)
        {
            BoundingBoxUV bb = face.GetBoundingBox();
            UV mid = new UV((bb.Min.U + bb.Max.U) * 0.5, (bb.Min.V + bb.Max.V) * 0.5);
            return face.ComputeNormal(mid);
        }

        /// <summary>
        /// Rough overall roof span (max pairwise vertex distance's bounding
        /// diagonal) — used only as a generous clip length for unbounded
        /// Voronoi hull rays and as the fallback bisector half-length. Does
        /// not need to be exact, only large enough to guarantee any ray/
        /// bisector segment fully crosses the roof's actual extent.
        /// </summary>
        private static double GetRoofSpanFt(List<SlabShapeVertex> vertices)
        {
            if (vertices.Count == 0) return 100.0; // fallback default (~30m), degenerate case only
            double minX = vertices.Min(v => v.Position.X), maxX = vertices.Max(v => v.Position.X);
            double minY = vertices.Min(v => v.Position.Y), maxY = vertices.Max(v => v.Position.Y);
            double dx = maxX - minX, dy = maxY - minY;
            return Math.Sqrt(dx * dx + dy * dy) + 10.0; // diagonal + margin
        }
    }
}
