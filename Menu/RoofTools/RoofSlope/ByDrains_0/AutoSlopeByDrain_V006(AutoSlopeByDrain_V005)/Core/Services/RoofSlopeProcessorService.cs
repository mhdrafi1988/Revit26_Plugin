// File: RoofSlopeProcessorService.cs
// Location: Core/Services/
// Base: ported from AutoSlopeByDrain V004 (DijkstraPathEngine-based path
// solving, straight-line primary / tangent-arc fallback).
//
// NOTE (unchanged from V004): ByPoint's tangent-arc math has a known,
// unresolved bug (arc-adjacent vertices can get an incorrect elevation) —
// ported here as-is, not fixed, since this is out of scope for the merge.
//
// NEW (V005), per Rafi's confirmed decisions on 2026-07-21:
//   - Curve-intersection point insertion (opt-in, off by default) — ported
//     from AutoSlopeByPoint's engine, wired in via InsertCurveIntersectionPoints
//     on the payload. Runs BEFORE the connectivity graph is built, with the
//     full re-fetch-fresh-handles cycle after the insertion transaction
//     commits (doc.Regenerate() invalidates the SlabShapeEditor + topFace).
//   - Elevation read-back verification (opt-in, OFF by default) — re-reads
//     each processed vertex's elevation from the model after Commit() and
//     populates DrainVertexData.ElevationFromModel_mm / ElevationDiff_mm.
//   - ArcsCalculated and UnreachableVertexCount surfaced for the result/UI
//     metrics (the unreachable-count LOG line already existed in V004; it's
//     now also captured as a number instead of just logged text).

using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlopeByDrain.V006.Core.Engine;
using Revit26_Plugin.AutoSlopeByDrain.V006.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByDrain.V006.Core.Services
{
    public class RoofSlopeProcessorService
    {
        private List<DrainVertexData> _lastExportData;
        private int _lastRunDuration;
        private int _lastArcsCalculated;
        private int _lastUnreachableCount;
        private int _lastOverThresholdCount;

        public int LastRunDuration => _lastRunDuration;

        /// <summary>Boundary/opening arcs found during this run. 0 if curve-intersection insertion was off or none were found.</summary>
        public int LastArcsCalculated => _lastArcsCalculated;

        /// <summary>Vertices that had no path to any selected drain at all (disconnected graph), not just "too far".</summary>
        public int LastUnreachableCount => _lastUnreachableCount;

        /// <summary>NEW (V005): vertices where a path existed but exceeded Max Path Distance.</summary>
        public int LastOverThresholdCount => _lastOverThresholdCount;

        /// <summary>
        /// Process slopes for the roof.
        /// </summary>
        /// <param name="connectionThresholdMeters">Max Edge Distance — gates graph edge creation, not final path length.</param>
        /// <param name="pathSampleCount">Interior points sampled per candidate edge to verify it lies on the roof face. Recommended range: 5-20.</param>
        /// <param name="insertCurveIntersectionPoints">NEW (V005). Opt-in: insert real vertices at boundary/opening arc crossings before pathfinding.</param>
        /// <param name="verifyElevationsAfterCommit">NEW (V005). Opt-in, off by default: re-read each processed vertex's elevation from the model after Commit() to detect silent Revit adjustments.</param>
        /// <param name="thresholdMeters">
        ///     NEW (V005). Max Path Distance — max TOTAL (multi-hop) path length from a
        ///     vertex to its nearest selected drain. Checked INSIDE the transaction,
        ///     before any elevation is written, so an over-threshold vertex is truly
        ///     skipped (no elevation change at all) rather than merely mislabeled
        ///     after the fact. Pass 0 or less to disable (no path-length cutoff beyond
        ///     what Max Edge Distance already implies).
        /// </param>
        public (int modifiedCount, double maxOffset, double longestPath) ProcessRoofSlopes(
            RoofData roofData,
            List<DrainItem> selectedDrains,
            double slopePercentage,
            Action<string> logAction,
            double connectionThresholdMeters = 30.0,
            int pathSampleCount = 10,
            bool insertCurveIntersectionPoints = false,
            bool verifyElevationsAfterCommit = false,
            double thresholdMeters = 0)
        {
            var doc = roofData.Roof.Document;
            int modifiedCount = 0;
            double maxOffset = 0;
            double longestPath = 0;
            DateTime startTime = DateTime.Now;
            _lastArcsCalculated = 0;
            _lastUnreachableCount = 0;
            _lastOverThresholdCount = 0;

            var topFace = roofData.TopFace;
            var vertexList = roofData.Vertices;

            // ── NEW (V005): opt-in curve-intersection point insertion ─────────
            // Runs in its own transaction BEFORE the main slope transaction, since
            // it changes roof geometry (adds shape editor points) and requires a
            // regenerate + fresh-handle re-fetch, per this suite's standing rule
            // that doc.Regenerate() invalidates SlabShapeEditor handles.
            double curveTolFt = UnitUtils.ConvertToInternalUnits(2.0, UnitTypeId.Millimeters);
            List<Arc> boundaryArcs = RoofBoundaryHelper.GetBoundaryArcs(topFace);

            if (insertCurveIntersectionPoints)
            {
                if (boundaryArcs.Count == 0)
                {
                    logAction("Curve intersection check enabled, but no arc edges found on this roof.");
                }
                else
                {
                    var positions = new List<XYZ>(vertexList.Count);
                    foreach (var v in vertexList) positions.Add(v.Position);

                    List<Line> boundaryLines = RoofBoundaryHelper.GetBoundaryLines(topFace);

                    int insertedCount;
                    var editor = roofData.Roof.GetSlabShapeEditor();
                    using (Transaction tx = new Transaction(doc, "Insert Curve Intersection Points"))
                    {
                        tx.Start();
                        insertedCount = CurveIntersectionHelper.InsertIntersectionPoints(
                            editor, boundaryLines, positions, boundaryArcs, curveTolFt, logAction);
                        tx.Commit();
                    }

                    if (insertedCount > 0)
                    {
                        // tx.Commit() above already regenerated the document, which
                        // invalidates the SlabShapeEditor handle acquired before it —
                        // re-fetch fresh handles before touching geometry again.
                        editor = roofData.Roof.GetSlabShapeEditor();
                        if (editor == null || !editor.IsValidObject)
                        {
                            logAction("ERROR: Slab shape editor became invalid after inserting curve points. Aborting.");
                            throw new InvalidOperationException("Slab shape editor became invalid after inserting curve points.");
                        }

                        vertexList = new List<SlabShapeVertex>();
                        foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                            vertexList.Add(v);
                        roofData.Vertices = vertexList;

                        topFace = GetTopFaceFresh(roofData.Roof);
                        if (topFace == null)
                        {
                            logAction("ERROR: Top face not found after inserting curve points. Aborting.");
                            throw new InvalidOperationException("Top face not found after inserting curve points.");
                        }
                        roofData.TopFace = topFace;
                        boundaryArcs = RoofBoundaryHelper.GetBoundaryArcs(topFace);
                    }
                }
            }

            _lastArcsCalculated = boundaryArcs.Count;

            using (var transaction = new Transaction(doc, "Auto Roof Sloper - Apply Slopes"))
            {
                transaction.Start();

                try
                {
                    logAction($"Building connectivity graph (max edge distance: {connectionThresholdMeters:F1} m, samples per edge: {pathSampleCount})...");
                    logAction($"Found {boundaryArcs.Count} boundary/opening arc(s) for tangent-route fallback.");

                    double connectionThresholdFeet = connectionThresholdMeters / 0.3048;
                    var pathEngine = new DijkstraPathEngine(
                        vertexList, topFace, connectionThresholdFeet, boundaryArcs,
                        curveTolFt, pathSampleCount);

                    // Step 1: Get all drain vertices from SELECTED drains
                    var selectedDrainVertices = new HashSet<SlabShapeVertex>();
                    foreach (var drain in selectedDrains)
                    {
                        foreach (var vertex in drain.DrainVertices)
                        {
                            selectedDrainVertices.Add(vertex);
                        }
                        logAction($"Drain {drain.SizeCategory} has {drain.DrainVertices.Count} vertices within 5mm tolerance");
                    }

                    // Captured BEFORE zeroing, for the post-commit verification pass —
                    // ElevationOffsetMm is relative to this baseline, not absolute Z=0,
                    // matching how AutoSlopeByPoint derives drainBaselineZFt.
                    _lastDrainBaselineZFt = selectedDrainVertices.Count > 0
                        ? selectedDrainVertices.Average(v => v.Position.Z)
                        : 0;

                    logAction($"Found {selectedDrainVertices.Count} vertices on selected drain loops - setting to ZERO elevation");
                    SetDrainLoopVerticesToZero(roofData.Roof, selectedDrainVertices, logAction);
                    modifiedCount += selectedDrainVertices.Count;

                    // Step 2: map vertex <-> index, and vertex -> owning DrainItem
                    var drainVertexIndices = new HashSet<int>();
                    var vertexToDrain = new Dictionary<SlabShapeVertex, DrainItem>();

                    for (int i = 0; i < vertexList.Count; i++)
                        if (selectedDrainVertices.Contains(vertexList[i]))
                            drainVertexIndices.Add(i);

                    foreach (var drain in selectedDrains)
                        foreach (var v in drain.DrainVertices)
                            vertexToDrain[v] = drain;

                    logAction($"Computing multi-source shortest paths to {drainVertexIndices.Count} drain vertices for {vertexList.Count} roof vertices...");

                    double[] distances = pathEngine.ComputeAllDistances(drainVertexIndices);

                    int onArc = 0;
                    for (int i = 0; i < vertexList.Count; i++)
                        if (pathEngine.IsOnArc(i)) onArc++;
                    if (onArc > 0)
                        logAction($"{onArc} vertex(es) sit on a boundary/opening arc within tolerance.");

                    var pathResults = BuildPathResultsFromEngine(vertexList, distances, drainVertexIndices, vertexToDrain, pathEngine, logAction);

                    // ── NEW (V005): Max Path Distance (ThresholdMeters) ────────────
                    // Applied HERE, before any elevation is written — removes
                    // over-threshold entries from pathResults entirely, the same way
                    // unreachable vertices never entered pathResults in the first
                    // place (see BuildPathResultsFromEngine). This guarantees a
                    // vertex that fails this check gets NO elevation change and is
                    // correctly reported as skipped in the export — not written to
                    // the model and then mislabeled afterward.
                    int overThresholdCount = 0;
                    if (thresholdMeters > 0)
                    {
                        var overThreshold = pathResults
                            .Where(kvp => kvp.Value.drain != null && kvp.Value.totalDistance * 0.3048 > thresholdMeters)
                            .Select(kvp => kvp.Key)
                            .ToList();

                        foreach (var vertex in overThreshold)
                            pathResults.Remove(vertex);

                        overThresholdCount = overThreshold.Count;
                        if (overThresholdCount > 0)
                        {
                            logAction($"WARNING: {overThresholdCount} vertex(es) exceeded Max Path Distance ({thresholdMeters:F1} m total path) and were skipped (no elevation change applied).");
                        }
                    }
                    _lastOverThresholdCount = overThresholdCount;

                    logAction("Applying elevations based on path distances...");
                    int slopeModifiedCount = ApplyElevationsWithDrainHierarchy(
                        roofData.Roof, pathResults, selectedDrainVertices, slopePercentage,
                        logAction, out maxOffset, out longestPath);
                    modifiedCount += slopeModifiedCount;

                    // Collect export data (elevation read-back happens AFTER commit, below)
                    _lastExportData = CollectVertexExportData(pathResults, selectedDrainVertices, vertexList, selectedDrains, slopePercentage);
                    _lastRunDuration = (int)(DateTime.Now - startTime).TotalSeconds;

                    transaction.Commit();

                    logAction($"SUCCESS: Set {selectedDrainVertices.Count} drain vertices to zero + modified {slopeModifiedCount} slope vertices");
                    logAction($"Maximum offset: {maxOffset:F1} mm");
                    logAction($"Longest drainage path: {longestPath:F2} meters");
                }
                catch (Exception ex)
                {
                    transaction.RollBack();
                    logAction($"ERROR: Transaction rolled back - {ex.Message}");
                    throw;
                }
            }

            // ── NEW (V005): elevation read-back verification ───────────────────
            // Runs OUTSIDE the main transaction (it already committed above) — this
            // is a read-only re-fetch, not a modification, so no transaction is
            // needed. Off by default; only runs when the toggle is on.
            if (verifyElevationsAfterCommit && _lastExportData != null && _lastExportData.Count > 0)
            {
                VerifyElevationsAfterCommit(roofData.Roof, vertexList, _lastDrainBaselineZFt, _lastExportData, logAction);
            }

            return (modifiedCount, maxOffset, longestPath);
        }

        public List<DrainVertexData> GetLastExportData()
        {
            return _lastExportData;
        }

        // Captured during the main transaction so the post-commit verification
        // pass (below) can compute each vertex's offset relative to the drain
        // baseline, the same way the elevations were originally calculated.
        private double _lastDrainBaselineZFt;

        /// <summary>
        /// NEW (V005), ported from AutoSlopeByPoint's post-commit re-read logic
        /// (adapted here for ByDrain's DrainVertexData / HashSet-of-vertices shape).
        /// Revit does not guarantee SlabShapeEditor.SlabShapeVertices index order is
        /// stable across a re-fetch after Commit(), so each pre-commit vertex is
        /// re-matched to its refreshed counterpart by X/Y position (not by index)
        /// before reading back its Z. The offset is computed relative to the drain
        /// baseline Z (drain vertices were set to a ZERO offset, not absolute Z=0),
        /// exactly as the engine calculated ElevationOffsetMm in the first place.
        /// Read-only — does not open a transaction.
        /// </summary>
        private void VerifyElevationsAfterCommit(
            RoofBase roof,
            List<SlabShapeVertex> preCommitVertices,
            double drainBaselineZFt,
            List<DrainVertexData> exportData,
            Action<string> logAction)
        {
            logAction("Verifying elevations against model after commit...");

            var editor = roof.GetSlabShapeEditor();
            if (editor == null || !editor.IsValidObject)
            {
                logAction("WARNING: Could not verify elevations — slab shape editor unavailable after commit.");
                return;
            }

            var refreshedVertices = new List<SlabShapeVertex>();
            foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                refreshedVertices.Add(v);

            // Match refreshed vertices back to pre-commit indices by X/Y position —
            // index order is not guaranteed stable across the re-fetch.
            var refreshedZByIndex = new Dictionary<int, double>();
            for (int i = 0; i < refreshedVertices.Count; i++)
            {
                for (int j = 0; j < preCommitVertices.Count; j++)
                {
                    double xyDist = Math.Sqrt(
                        Math.Pow(refreshedVertices[i].Position.X - preCommitVertices[j].Position.X, 2) +
                        Math.Pow(refreshedVertices[i].Position.Y - preCommitVertices[j].Position.Y, 2));

                    if (xyDist < 0.001)
                    {
                        refreshedZByIndex[j] = refreshedVertices[i].Position.Z;
                        break;
                    }
                }
            }

            int adjustedCount = 0;
            int unmatchedCount = 0;

            foreach (var data in exportData)
            {
                if (!data.WasProcessed) continue;

                if (refreshedZByIndex.TryGetValue(data.VertexIndex, out double refreshedZFt))
                {
                    double elevFromModelFt = refreshedZFt - drainBaselineZFt;
                    data.ElevationFromModel_mm = UnitUtils.ConvertFromInternalUnits(elevFromModelFt, UnitTypeId.Millimeters);

                    if (Math.Round(data.ElevationDiff_mm, 0) != 0)
                        adjustedCount++;
                }
                else
                {
                    // Could not match — fall back to the calculated value so ElevDiff_mm
                    // reads as 0 (no false positive) instead of a meaningless large diff.
                    data.ElevationFromModel_mm = data.ElevationOffsetMm;
                    unmatchedCount++;
                }
            }

            if (unmatchedCount > 0)
                logAction($"WARNING: Could not re-match {unmatchedCount} vertex(es) after commit — verification skipped for those.");

            if (adjustedCount > 0)
                logAction($"WARNING: {adjustedCount} vertex(es) were adjusted by Revit during commit — check ElevDiff_mm column in the export.");
            else
                logAction("Verification complete — no elevation adjustments detected.");
        }

        private List<DrainVertexData> CollectVertexExportData(
            Dictionary<SlabShapeVertex, (DrainItem drain, double totalDistance, List<XYZ> path)> pathResults,
            HashSet<SlabShapeVertex> drainVertices,
            List<SlabShapeVertex> allVertices,
            List<DrainItem> selectedDrains,
            double slopePercentage)
        {
            // NEW (V005) fix: iterate ALL roof vertices, not just pathResults.Keys.
            // In V004, any vertex absent from pathResults (unreachable, or now also
            // over-Max-Path-Distance) silently produced NO export row at all. Per
            // Rafi's confirmed decision, skipped vertices must always appear in the
            // Vertex Data sheet with WasProcessed=NO — matching the same principle
            // already applied to ByPoint's export (see ExcelExportHelper).
            var vertexDataList = new List<DrainVertexData>();

            for (int i = 0; i < allVertices.Count; i++)
            {
                var vertex = allVertices[i];
                if (vertex?.Position == null) continue;

                bool inPathResults = pathResults.TryGetValue(vertex, out var result);
                var drain = inPathResults ? result.drain : null;
                var totalDistance = inPathResults ? result.totalDistance : 0;
                var path = inPathResults ? result.path : null;

                bool wasProcessed = inPathResults && !drainVertices.Contains(vertex) && drain != null;

                double elevationMm = wasProcessed ? slopePercentage / 100.0 * totalDistance * 304.8 : 0;

                int drainIndex = -1;
                string drainSize = "";
                string drainShape = "";

                if (drain != null)
                {
                    drainIndex = selectedDrains.IndexOf(drain);
                    drainSize = drain.SizeCategory;
                    drainShape = drain.ShapeType;
                }

                XYZ direction = XYZ.Zero;
                if (wasProcessed && path != null && path.Count >= 2)
                {
                    direction = (path[path.Count - 1] - path[0]).Normalize();
                }

                vertexDataList.Add(new DrainVertexData
                {
                    VertexIndex = i,
                    Position = vertex.Position,
                    // For a skipped/unreachable vertex that never entered pathResults,
                    // totalDistance is 0 (unknown) rather than a misleading computed
                    // value — the export's WasProcessed=NO column is the signal to
                    // read, not PathLengthMeters, for these rows.
                    PathLengthMeters = inPathResults ? totalDistance * 0.3048 : 0,
                    ElevationOffsetMm = elevationMm,
                    NearestDrainId = drainIndex + 1,
                    DrainSize = drainSize,
                    DrainShape = drainShape,
                    DirectionVector = direction,
                    WasProcessed = wasProcessed
                });
            }

            return vertexDataList;
        }

        private void SetDrainLoopVerticesToZero(RoofBase roof, HashSet<SlabShapeVertex> drainVertices, Action<string> logAction)
        {
            var slabShapeEditor = roof.GetSlabShapeEditor();
            int setToZeroCount = 0;

            foreach (var vertex in drainVertices)
            {
                if (vertex == null) continue;
                try
                {
                    slabShapeEditor.ModifySubElement(vertex, 0.0);
                    setToZeroCount++;
                }
                catch (Exception ex)
                {
                    logAction($"WARNING: Could not set drain vertex to zero: {ex.Message}");
                }
            }

            logAction($"Set {setToZeroCount} drain loop vertices to ZERO elevation");
        }

        private Dictionary<SlabShapeVertex, (DrainItem drain, double totalDistance, List<XYZ> path)> BuildPathResultsFromEngine(
            List<SlabShapeVertex> vertexList,
            double[] distances,
            HashSet<int> drainVertexIndices,
            Dictionary<SlabShapeVertex, DrainItem> vertexToDrain,
            DijkstraPathEngine pathEngine,
            Action<string> logAction)
        {
            var results = new Dictionary<SlabShapeVertex, (DrainItem, double, List<XYZ>)>();
            int processedCount = 0;
            int unreachable = 0;

            for (int i = 0; i < vertexList.Count; i++)
            {
                var vertex = vertexList[i];
                if (vertex?.Position == null) continue;

                if (drainVertexIndices.Contains(i))
                {
                    results[vertex] = (null, 0, null);
                    continue;
                }

                double dist = distances[i];
                if (double.IsInfinity(dist))
                {
                    unreachable++;
                    continue; // no path found within threshold — vertex is simply skipped
                }

                int rootIdx = pathEngine.GetRootDrainIndex(i);
                DrainItem drain = null;
                if (rootIdx >= 0 && rootIdx < vertexList.Count)
                    vertexToDrain.TryGetValue(vertexList[rootIdx], out drain);

                if (drain == null) continue;

                var path = pathEngine.GetPathPositions(i);
                results[vertex] = (drain, dist, path);

                processedCount++;
                if (processedCount % 50 == 0)
                    logAction($"Computed paths for {processedCount}/{vertexList.Count} vertices...");
            }

            _lastUnreachableCount = unreachable;

            if (unreachable > 0)
                logAction($"WARNING: {unreachable} vertex(es) had no valid path to any selected drain — likely disconnected from the graph. Check Max Edge Distance if this seems too high.");

            logAction($"✓ Path computation completed for {processedCount} vertices");
            return results;
        }

        private int ApplyElevationsWithDrainHierarchy(RoofBase roof,
            Dictionary<SlabShapeVertex, (DrainItem drain, double totalDistance, List<XYZ> path)> pathResults,
            HashSet<SlabShapeVertex> drainVertices,
            double slopePercentage,
            Action<string> logAction,
            out double maxOffset,
            out double longestPath)
        {
            int modifiedCount = 0;
            maxOffset = 0;
            longestPath = 0;

            var slabShapeEditor = roof.GetSlabShapeEditor();

            foreach (var kvp in pathResults)
            {
                var vertex = kvp.Key;
                var totalDistance = kvp.Value.totalDistance;
                var drain = kvp.Value.drain;

                if (vertex == null) continue;

                if (drainVertices.Contains(vertex))
                    continue;

                if (drain == null)
                    continue;

                double pathLengthMeters = totalDistance * 0.3048;
                if (pathLengthMeters > longestPath)
                    longestPath = pathLengthMeters;

                double slopeRatio = slopePercentage / 100.0;
                double elevationChange = slopeRatio * totalDistance * 304.8;
                double newElevationFeet = elevationChange / 304.8;
                slabShapeEditor.ModifySubElement(vertex, newElevationFeet);
                modifiedCount++;

                if (elevationChange > maxOffset)
                    maxOffset = elevationChange;
            }

            logAction($"Applied slopes to {modifiedCount} non-drain vertices");
            return modifiedCount;
        }

        private static Face GetTopFaceFresh(RoofBase roof)
        {
            GeometryElement geomElem = roof.get_Geometry(new Options());
            Face topFace = null;
            double maxZ = double.MinValue;

            foreach (GeometryObject geomObj in geomElem)
            {
                if (geomObj is Solid solid)
                {
                    foreach (Face face in solid.Faces)
                    {
                        if (face == null) continue;
                        BoundingBoxUV bb = face.GetBoundingBox();
                        if (bb == null) continue;

                        UV midpointUV = new UV((bb.Min.U + bb.Max.U) / 2, (bb.Min.V + bb.Max.V) / 2);
                        XYZ midpoint = face.Evaluate(midpointUV);

                        if (midpoint != null && midpoint.Z > maxZ)
                        {
                            maxZ = midpoint.Z;
                            topFace = face;
                        }
                    }
                }
            }
            return topFace;
        }
    }
}
