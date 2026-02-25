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

        public (int modifiedCount, double maxOffset, double longestPath) ProcessRoofSlopes(
            RoofData roofData, List<DrainItem> selectedDrains, double slopePercentage, Action<string> logAction)
        {
            var doc = roofData.Roof.Document;
            int modifiedCount = 0;
            double maxOffset = 0;
            double longestPath = 0;
            DateTime startTime = DateTime.Now;

            // Use a single transaction for the entire slope application process
            using (var transaction = new Transaction(doc, "Auto Roof Sloper - Apply Slopes"))
            {
                transaction.Start();

                try
                {
                    logAction("Building connectivity graph for pathfinding...");
                    var graph = _graphBuilder.BuildGraph(roofData.Vertices, roofData.TopFace);

                    // Step 1: Get all drain vertices from SELECTED drains (already identified with 5mm tolerance)
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

                    // Step 2: Create drain targets from SELECTED drains using their vertices
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

        /// <summary>
        /// Get the last export data
        /// </summary>
        public List<DrainVertexData> GetLastExportData()
        {
            return _lastExportData;
        }

        /// <summary>
        /// Collect vertex data for CSV export
        /// </summary>
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

                // Find nearest drain index
                int drainIndex = -1;
                string drainSize = "";
                string drainShape = "";

                if (drain != null)
                {
                    drainIndex = selectedDrains.IndexOf(drain);
                    drainSize = drain.SizeCategory;
                    drainShape = drain.ShapeType;
                }

                // Calculate direction vector
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
                    NearestDrainId = drainIndex + 1, // 1-based for readability
                    DrainSize = drainSize,
                    DrainShape = drainShape,
                    DirectionVector = direction,
                    WasProcessed = wasProcessed
                });
            }

            return vertexDataList;
        }

        /// <summary>
        /// Update all custom parameters on the roof element after successful slope calculation
        /// </summary>
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
                    // Get the highest elevation achieved (max offset is already in mm)
                    double highestElevationMm = maxOffset;

                    // Count vertices processed (total vertices)
                    int verticesProcessed = modifiedCount;

                    // Count vertices skipped (if any)
                    int verticesSkipped = 0; // You can calculate this if needed

                    // Current date/time for the run
                    string runDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                    // Update parameters
                    SetParameterValue(roof, "AutoSlope_HighestElevation", highestElevationMm / 304.8); // Convert to feet for Revit
                    SetParameterValue(roof, "AutoSlope_VerticesProcessed", verticesProcessed);
                    SetParameterValue(roof, "AutoSlope_VerticesSkipped", verticesSkipped);
                    SetParameterValue(roof, "AutoSlope_DrainCount", drainCount);
                    SetParameterValue(roof, "AutoSlope_RunDuration_sec", durationSeconds);
                    SetParameterValue(roof, "AutoSlope_Status", "Completed");
                    SetParameterValue(roof, "AutoSlope_LongestPath", longestPath); // Already in meters
                    SetParameterValue(roof, "AutoSlope_SlopePercent", slopePercentage);
                    SetParameterValue(roof, "AutoSlope_Threshold", 100.0); // Default threshold value
                    SetParameterValue(roof, "AutoSlope_RunDate", runDate);
                    SetParameterValue(roof, "AutoSlope_Versions", "D.04.00"); // ADD THIS LINE

                    logAction("✓ Updated roof parameters with calculation results:");
                    logAction($"  - Highest Elevation: {highestElevationMm:F2} mm");
                    logAction($"  - Vertices Processed: {verticesProcessed}");
                    logAction($"  - Drain Count: {drainCount}");
                    logAction($"  - Run Duration: {durationSeconds:F2} seconds");
                    logAction($"  - Run Date: {runDate}");
                    logAction($"  - Version: D.04.00"); // ADD THIS LINE

                    paramTransaction.Commit();
                }
                catch (Exception ex)
                {
                    paramTransaction.RollBack();
                    logAction($"✗ Failed to update roof parameters: {ex.Message}");
                    logAction("  Note: This doesn't affect the slope calculation results.");
                }
            }
        }

        /// <summary>
        /// Helper method to set parameter values safely
        /// </summary>
        private void SetParameterValue(Element element, string paramName, object value)
        {
            Parameter param = element.LookupParameter(paramName);
            if (param != null && !param.IsReadOnly)
            {
                switch (param.StorageType)
                {
                    case StorageType.String:
                        param.Set(value?.ToString() ?? "");
                        break;
                    case StorageType.Double:
                        if (value is double doubleValue)
                            param.Set(doubleValue);
                        else if (value is int intValue)
                            param.Set(Convert.ToDouble(intValue));
                        break;
                    case StorageType.Integer:
                        if (value is int intVal)
                            param.Set(intVal);
                        else if (value is double doubleVal)
                            param.Set(Convert.ToInt32(doubleVal));
                        break;
                }
            }
        }

        /// <summary>
        /// Set all vertices on selected drain loops to ZERO elevation
        /// </summary>
        private void SetDrainLoopVerticesToZero(RoofBase roof, HashSet<SlabShapeVertex> drainVertices, Action<string> logAction)
        {
            int setToZeroCount = 0;

            var slabShapeEditor = roof.GetSlabShapeEditor(); // Get the editor once

            foreach (var vertex in drainVertices)
            {
                if (vertex != null)
                {
                    slabShapeEditor.ModifySubElement(vertex, 0.0);
                    setToZeroCount++;
                }
            }

            logAction($"Set {setToZeroCount} drain loop vertices to ZERO elevation");
        }

        /// <summary>
        /// UPDATED: Create drain targets from SELECTED drains using their pre-identified vertices
        /// </summary>
        private List<DrainTarget> CreateDrainTargetsFromSelectedDrains(List<DrainItem> selectedDrains, Action<string> logAction)
        {
            var drainTargets = new List<DrainTarget>();
            int targetCount = 0;

            foreach (var drain in selectedDrains)
            {
                // Use the pre-identified drain vertices (found with 5mm tolerance)
                foreach (var drainVertex in drain.DrainVertices)
                {
                    drainTargets.Add(new DrainTarget
                    {
                        Vertex = drainVertex,
                        CornerPoint = drainVertex.Position, // Use vertex position
                        ParentDrain = drain,
                        VertexToCornerDistance = 0.0 // Zero since we're using the vertex itself
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

            // Group drain targets by vertex for efficient lookup
            var targetsByVertex = drainTargets.GroupBy(t => t.Vertex).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var vertex in vertices)
            {
                if (vertex?.Position == null) continue;

                // Skip vertices that are part of drain loops (they're already at zero)
                if (excludedVertices.Contains(vertex))
                {
                    results[vertex] = (null, 0, null); // Mark as drain vertex
                    continue;
                }

                // Find shortest path to any drain target
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

                // Find shortest path from start vertex to this drain vertex
                var path = _pathSolver.DijkstraPath(startVertex, drainVertex, graph);
                if (path != null && path.Count >= 2)
                {
                    double pathDistance = CalculatePathLength(path);

                    // Use the first target (all targets for this vertex have same parent drain)
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

            var slabShapeEditor = roof.GetSlabShapeEditor(); // Get the editor once

            foreach (var kvp in pathResults)
            {
                var vertex = kvp.Key;
                var totalDistance = kvp.Value.totalDistance;
                var drain = kvp.Value.drain;

                if (vertex == null) continue;

                // Skip vertices that are part of drain loops (already set to zero)
                if (drainVertices.Contains(vertex))
                    continue;

                // Skip vertices with no path to drain
                if (drain == null)
                    continue;

                // Track longest path (in meters)
                double pathLengthMeters = totalDistance * 0.3048;
                if (pathLengthMeters > longestPath)
                    longestPath = pathLengthMeters;

                // Calculate elevation based on distance and slope
                double slopeRatio = slopePercentage / 100.0;
                double elevationChange = slopeRatio * totalDistance * 304.8; // Convert to mm
                double newElevation = elevationChange;

                // Apply the elevation (in feet, as Revit expects)
                double newElevationFeet = newElevation / 304.8;
                slabShapeEditor.ModifySubElement(vertex, newElevationFeet);
                modifiedCount++;

                if (newElevation > maxOffset)
                    maxOffset = newElevation;
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