using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlopeByDrain.V004.Core.Engine;
using Revit26_Plugin.AutoSlopeByDrain.V004.Core.Models;
using Revit26_Plugin.AutoSlopeByDrain.V004.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByDrain.V004.Core.Services
{
    public class RoofSlopeProcessorService
    {
        // V004: GraphBuilderService (straight-line only) + PathSolverService (per-pair
        // Dijkstra) replaced with DijkstraPathEngine, ported from AutoSlopeByPoint.
        // Behavior: straight-line edges are still tried FIRST; the tangent-in/arc/
        // tangent-out route is only computed as a FALLBACK when a straight chord is
        // blocked by a crossing boundary/opening arc — same as ByPoint. Multi-source
        // Dijkstra (ComputeAllDistances) also fits ByDrain's "several distinct drains"
        // model directly, since any drain vertex can be a source.
        //
        // NOTE: ByPoint's tangent-arc math has a known, unresolved bug (arc-adjacent
        // vertices can get an incorrect elevation) — ported here as-is per instruction,
        // not fixed.

        // Store last export data
        private List<DrainVertexData> _lastExportData;
        private int _lastRunDuration;

        public int LastRunDuration => _lastRunDuration;

        /// <summary>
        /// Process slopes for the roof.
        /// </summary>
        /// <param name="connectionThresholdMeters">
        ///     Max vertex-to-vertex connection distance in meters, supplied by the user.
        /// </param>
        /// <param name="pathSampleCount">
        ///     Number of interior points sampled along each candidate edge to verify
        ///     it lies on the roof face. Passed through to DijkstraPathEngine.
        ///     Recommended range: 5-20. Higher = stricter, slower.
        /// </param>
        public (int modifiedCount, double maxOffset, double longestPath) ProcessRoofSlopes(
            RoofData roofData,
            List<DrainItem> selectedDrains,
            double slopePercentage,
            Action<string> logAction,
            double connectionThresholdMeters = 30.0,
            int pathSampleCount = 5)
        {
            var doc = roofData.Roof.Document;
            int modifiedCount = 0;
            double maxOffset = 0;
            double longestPath = 0;
            DateTime startTime = DateTime.Now;

            using (var transaction = new Transaction(doc, "Auto Roof Sloper - Apply Slopes"))
            {
                transaction.Start();

                try
                {
                    logAction($"Building connectivity graph (threshold: {connectionThresholdMeters:F1} m, samples per edge: {pathSampleCount})...");

                    // V004: extract boundary/opening arcs so the path engine can fall back
                    // to tangent-in/arc/tangent-out routing when a straight chord is blocked.
                    var boundaryArcs = RoofBoundaryHelper.GetBoundaryArcs(roofData.TopFace);
                    logAction($"Found {boundaryArcs.Count} boundary/opening arc(s) for tangent-route fallback.");

                    double connectionThresholdFeet = connectionThresholdMeters / 0.3048;
                    var pathEngine = new DijkstraPathEngine(
                        roofData.Vertices, roofData.TopFace, connectionThresholdFeet, boundaryArcs,
                        pathSampleCount: pathSampleCount);

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

                    logAction($"Found {selectedDrainVertices.Count} vertices on selected drain loops - setting to ZERO elevation");
                    SetDrainLoopVerticesToZero(roofData.Roof, selectedDrainVertices, logAction);
                    modifiedCount += selectedDrainVertices.Count;

                    // Step 2: map vertex <-> index, and vertex -> owning DrainItem, for
                    // reconstructing which drain each shortest path terminates at.
                    var vertexList = roofData.Vertices;
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

                    logAction("Applying elevations based on path distances...");
                    int slopeModifiedCount = ApplyElevationsWithDrainHierarchy(roofData.Roof, pathResults, selectedDrainVertices, slopePercentage, logAction, out maxOffset, out longestPath);
                    modifiedCount += slopeModifiedCount;

                    // Collect export data
                    _lastExportData = CollectVertexExportData(pathResults, selectedDrainVertices, roofData.Vertices, selectedDrains, slopePercentage);
                    _lastRunDuration = (int)(DateTime.Now - startTime).TotalSeconds;

                    transaction.Commit();

                    logAction($"SUCCESS: Set {selectedDrainVertices.Count} drain vertices to zero + modified {slopeModifiedCount} slope vertices");
                    logAction($"Maximum offset: {maxOffset:F1} mm");
                    logAction($"Longest drainage path: {longestPath:F2} meters");

                    // NOTE (V003): parameter writing moved OUT of this method — it is now owned
                    // exclusively by AutoSlopeDrainEngine, which has the correct threshold value
                    // and calls AutoSlopeDrainParameterWriter exactly once after export data is
                    // collected. Writing here too caused a duplicate transaction with a hardcoded
                    // threshold of 0.0.
                }
                catch (Exception ex)
                {
                    transaction.RollBack();
                    logAction($"ERROR: Transaction rolled back - {ex.Message}");
                    throw;
                }
            }

            return (modifiedCount, maxOffset, longestPath);
        }

        public List<DrainVertexData> GetLastExportData()
        {
            return _lastExportData;
        }

        private List<DrainVertexData> CollectVertexExportData(
            Dictionary<SlabShapeVertex, (DrainItem drain, double totalDistance, List<XYZ> path)> pathResults,
            HashSet<SlabShapeVertex> drainVertices,
            List<SlabShapeVertex> allVertices,
            List<DrainItem> selectedDrains,
            double slopePercentage)
        {
            var vertexDataList = new List<DrainVertexData>();

            foreach (var kvp in pathResults)
            {
                var vertex = kvp.Key;
                var totalDistance = kvp.Value.totalDistance;
                var drain = kvp.Value.drain;
                var path = kvp.Value.path;

                bool wasProcessed = !drainVertices.Contains(vertex) && drain != null;

                int vertexIndex = allVertices.IndexOf(vertex);
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
                    VertexIndex = vertexIndex,
                    Position = vertex.Position,
                    PathLengthMeters = totalDistance * 0.3048,
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

        /// <summary>
        /// V004: replaces CreateDrainTargetsFromSelectedDrains + ComputeShortestPaths +
        /// FindShortestPathToAnyDrainTarget + CalculatePathLength. Converts the engine's
        /// index-based multi-source Dijkstra result into the same
        /// Dictionary&lt;SlabShapeVertex, (DrainItem, distance, path)&gt; shape the
        /// downstream ApplyElevationsWithDrainHierarchy / CollectVertexExportData
        /// methods already expect, so nothing further down had to change.
        /// </summary>
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
                    continue; // no path found within threshold — same as before, vertex is simply skipped
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

            if (unreachable > 0)
                logAction($"WARNING: {unreachable} vertex(es) had no valid path to any selected drain within the connection threshold.");

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
    }
}