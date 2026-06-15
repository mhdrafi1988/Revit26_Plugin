using Autodesk.Revit.DB;
using Revit26_Plugin.Asd_19.Models;
using Revit26_Plugin.Asd_19.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.Asd_19.Services
{
    public class RoofSlopeProcessorService
    {
        private readonly GraphBuilderService _graphBuilder;
        private readonly PathSolverService _pathSolver;

        // Store last export data
        private List<DrainVertexData> _lastExportData;
        private int _lastRunDuration;

        public int LastRunDuration => _lastRunDuration;

        public RoofSlopeProcessorService()
        {
            _graphBuilder = new GraphBuilderService();
            _pathSolver = new PathSolverService();
        }

        /// <summary>
        /// Process slopes for the roof.
        /// </summary>
        /// <param name="connectionThresholdMeters">
        ///     Max vertex-to-vertex connection distance in meters, supplied by the user.
        /// </param>
        /// <param name="pathSampleCount">
        ///     Number of interior points sampled along each candidate edge to verify
        ///     it lies on the roof face. Passed through to GraphBuilderService.
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

                    // Pass the user-supplied threshold (in meters) to BuildGraph
                    var graph = _graphBuilder.BuildGraph(roofData.Vertices, roofData.TopFace, connectionThresholdMeters, pathSampleCount);

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

                    // Step 2: Create drain targets from SELECTED drains
                    var drainTargets = CreateDrainTargetsFromSelectedDrains(selectedDrains, logAction);

                    logAction($"Computing paths to {drainTargets.Count} drain targets for {roofData.Vertices.Count} vertices...");
                    var pathResults = ComputeShortestPaths(roofData.Vertices, drainTargets, graph, selectedDrainVertices, logAction);

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

                    // Update all parameters in a separate transaction
                    UpdateRoofParameters(roofData.Roof, modifiedCount, maxOffset, longestPath, slopePercentage, startTime, selectedDrains.Count, logAction);
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

        private void UpdateRoofParameters(RoofBase roof, int modifiedCount, double maxOffset, double longestPath,
            double slopePercentage, DateTime startTime, int drainCount, Action<string> logAction)
        {
            var doc = roof.Document;
            double durationSeconds = (DateTime.Now - startTime).TotalSeconds;

            using (var paramTransaction = new Transaction(doc, "Auto Roof Sloper - Update Parameters"))
            {
                paramTransaction.Start();

                try
                {
                    double highestElevationMm = maxOffset;
                    int verticesProcessed = modifiedCount;
                    int verticesSkipped = 0;
                    string runDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                    var metrics = new DrainExportMetrics
                    {
                        ProcessedVertices = verticesProcessed,
                        SkippedVertices = verticesSkipped,
                        DrainCount = drainCount,
                        HighestElevationMm = highestElevationMm,
                        LongestPathM = longestPath,
                        SlopePercent = slopePercentage,
                        RunDurationSec = (int)durationSeconds,
                        RunDate = runDate
                    };

                    var paramWriter = new Revit26_Plugin.Asd_19.Core.Parameters.AutoSlopeParameterWriter();
                    var writeResult = paramWriter.WriteAll(
                        doc,
                        roof,
                        metrics,
                        slopePercentage,
                        0.0,
                        logAction);

                    paramTransaction.Commit();
                    logAction($"Parameters updated: {writeResult.SuccessCount} written, {writeResult.FailCount} skipped");
                }
                catch (Exception ex)
                {
                    paramTransaction.RollBack();
                    logAction($"WARNING: Parameter update failed - {ex.Message}");
                }
            }
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

        private List<DrainTarget> CreateDrainTargetsFromSelectedDrains(List<DrainItem> selectedDrains, Action<string> logAction)
        {
            var drainTargets = new List<DrainTarget>();
            int targetCount = 0;

            foreach (var drain in selectedDrains)
            {
                foreach (var drainVertex in drain.DrainVertices)
                {
                    drainTargets.Add(new DrainTarget
                    {
                        Vertex = drainVertex,
                        CornerPoint = drainVertex.Position,
                        ParentDrain = drain,
                        VertexToCornerDistance = 0.0
                    });
                    targetCount++;
                }

                logAction($"Created {drain.DrainVertices.Count} targets for drain {drain.SizeCategory}");
            }

            logAction($"Total drain targets created: {targetCount}");
            return drainTargets;
        }

        private Dictionary<SlabShapeVertex, (DrainItem drain, double totalDistance, List<XYZ> path)> ComputeShortestPaths(
            List<SlabShapeVertex> vertices,
            List<DrainTarget> drainTargets,
            Dictionary<SlabShapeVertex, List<SlabShapeVertex>> graph,
            HashSet<SlabShapeVertex> excludedVertices,
            Action<string> logAction)
        {
            var results = new Dictionary<SlabShapeVertex, (DrainItem, double, List<XYZ>)>();
            int processedCount = 0;

            var targetsByVertex = drainTargets.GroupBy(t => t.Vertex).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var vertex in vertices)
            {
                if (vertex?.Position == null) continue;

                if (excludedVertices.Contains(vertex))
                {
                    results[vertex] = (null, 0, null);
                    continue;
                }

                var shortestPathInfo = FindShortestPathToAnyDrainTarget(vertex, targetsByVertex, graph);

                if (shortestPathInfo.drain != null)
                {
                    results[vertex] = shortestPathInfo;
                }

                processedCount++;
                if (processedCount % 50 == 0)
                {
                    logAction($"Computed paths for {processedCount}/{vertices.Count} vertices...");
                }
            }

            logAction($"✓ Path computation completed for {processedCount} vertices");
            return results;
        }

        private (DrainItem drain, double totalDistance, List<XYZ> path) FindShortestPathToAnyDrainTarget(
            SlabShapeVertex startVertex,
            Dictionary<SlabShapeVertex, List<DrainTarget>> targetsByVertex,
            Dictionary<SlabShapeVertex, List<SlabShapeVertex>> graph)
        {
            DrainItem nearestDrain = null;
            double minTotalDistance = double.MaxValue;
            List<XYZ> shortestPath = null;

            foreach (var targetEntry in targetsByVertex)
            {
                var drainVertex = targetEntry.Key;
                var targets = targetEntry.Value;

                var path = _pathSolver.DijkstraPath(startVertex, drainVertex, graph);
                if (path != null && path.Count >= 2)
                {
                    double pathDistance = CalculatePathLength(path);
                    var target = targets.First();
                    double totalDistance = pathDistance + target.VertexToCornerDistance;

                    if (totalDistance < minTotalDistance)
                    {
                        minTotalDistance = totalDistance;
                        shortestPath = path;
                        nearestDrain = target.ParentDrain;
                    }
                }
            }

            return (nearestDrain, minTotalDistance, shortestPath);
        }

        private double CalculatePathLength(List<XYZ> path)
        {
            double length = 0;
            for (int i = 0; i < path.Count - 1; i++)
            {
                if (path[i] != null && path[i + 1] != null)
                {
                    length += path[i].DistanceTo(path[i + 1]);
                }
            }
            return length;
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

    public class DrainTarget
    {
        public SlabShapeVertex Vertex { get; set; }
        public XYZ CornerPoint { get; set; }
        public DrainItem ParentDrain { get; set; }
        public double VertexToCornerDistance { get; set; }
    }
}