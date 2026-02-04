using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlopeByPoint_WIP2.Engine;
using Revit26_Plugin.AutoSlopeByPoint_WIP2.Export;
using Revit26_Plugin.AutoSlopeByPoint_WIP2.Models;
using Revit26_Plugin.AutoSlopeByPoint_WIP2.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByPoint_WIP2.ExternalEvents
{
    public class AutoSlopeExternalEventHandler : IExternalEventHandler
    {
        private readonly Document _doc;

        public AutoSlopeExternalEventHandler(Document doc)
        {
            _doc = doc;
        }

        public void Execute(UIApplication app)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var payload = AutoSlopeEventManager.Payload;
                if (payload == null)
                {
                    TaskDialog.Show("Error", "Payload is null!");
                    return;
                }

                // Get the roof element
                var roof = _doc.GetElement(payload.RoofId) as RoofBase;
                if (roof == null)
                {
                    payload.Log?.Invoke("ERROR: Roof element not found!");
                    return;
                }

                payload.Log?.Invoke("Starting slope calculation...");

                // Create export context
                var exportContext = new AutoSlopeExportContext(payload.ExportFolder);
                int pointIndex = 0;
                double highestElevationFt = 0;
                double longestPathMeters = 0;
                int verticesProcessed = 0;
                int verticesSkipped = 0;

                using (Transaction tx = new Transaction(_doc, "Apply Auto Slope"))
                {
                    tx.Start();

                    try
                    {
                        // Get the slab shape editor
                        var editor = roof.GetSlabShapeEditor();
                        if (!editor.IsEnabled)
                        {
                            editor.Enable();
                        }

                        // Get all vertices
                        var vertices = editor.SlabShapeVertices.Cast<SlabShapeVertex>().ToList();
                        payload.Log?.Invoke($"Found {vertices.Count} vertices");

                        // Get top face for Dijkstra
                        var topFace = AutoSlopeGeometry.GetTopFace(roof);
                        if (topFace == null)
                        {
                            payload.Log?.Invoke("ERROR: Could not get top face!");
                            return;
                        }

                        payload.Log?.Invoke("Building graph for Dijkstra pathfinding...");

                        // Convert threshold from meters to Revit internal units (feet)
                        double thresholdFeet = UnitUtils.ConvertToInternalUnits(
                            payload.ThresholdMeters,
                            UnitTypeId.Meters);

                        var dijkstraEngine = new DijkstraPathEngine(
                            vertices,
                            topFace,
                            thresholdFeet);

                        // Identify drain vertices
                        var drainIndices = new HashSet<int>();
                        for (int i = 0; i < vertices.Count; i++)
                        {
                            var vertexPos = vertices[i].Position;

                            foreach (var drainPoint in payload.DrainPoints)
                            {
                                // Check if drain point is near this vertex (within 0.1m ~ 0.328ft)
                                double distance = vertexPos.DistanceTo(drainPoint);
                                if (distance < 0.328084) // 0.1m in feet
                                {
                                    drainIndices.Add(i);
                                    break; // This vertex is a drain, move to next vertex
                                }
                            }
                        }

                        payload.Log?.Invoke($"Found {drainIndices.Count} drain vertices");

                        // Get maximum allowed offset based on roof thickness
                        double maxAllowedOffsetFt = GetMaxAllowedOffset(roof);
                        double maxAllowedOffsetMm = UnitUtils.ConvertFromInternalUnits(
                            maxAllowedOffsetFt,
                            UnitTypeId.Millimeters);

                        payload.Log?.Invoke($"Maximum allowed elevation offset: {maxAllowedOffsetFt:F3}ft " +
                                           $"({maxAllowedOffsetMm:F0}mm)");

                        // If no drain vertices found, use the closest vertex to each drain point
                        if (drainIndices.Count == 0 && payload.DrainPoints.Count > 0)
                        {
                            payload.Log?.Invoke("No vertices found near drain points. Finding closest vertices...");

                            foreach (var drainPoint in payload.DrainPoints)
                            {
                                double minDistance = double.MaxValue;
                                int closestVertexIndex = -1;

                                for (int i = 0; i < vertices.Count; i++)
                                {
                                    double distance = vertices[i].Position.DistanceTo(drainPoint);
                                    if (distance < minDistance)
                                    {
                                        minDistance = distance;
                                        closestVertexIndex = i;
                                    }
                                }

                                if (closestVertexIndex != -1)
                                {
                                    drainIndices.Add(closestVertexIndex);
                                    payload.Log?.Invoke($"Assigned drain point to vertex {closestVertexIndex} (distance: {minDistance:F3}ft)");
                                }
                            }
                        }

                        // Process each vertex
                        for (int i = 0; i < vertices.Count; i++)
                        {
                            var vertex = vertices[i];

                            // Skip if this is a drain vertex
                            if (drainIndices.Contains(i))
                            {
                                // Ensure drain vertex is at elevation 0
                                try
                                {
                                    editor.ModifySubElement(vertex, 0);
                                    payload.Log?.Invoke($"Vertex {i} set as drain point (elevation: 0)");
                                }
                                catch (Exception ex)
                                {
                                    payload.Log?.Invoke($"WARNING: Failed to reset drain vertex {i}: {ex.Message}");
                                }
                                verticesSkipped++;
                                continue;
                            }

                            // Find shortest path to any drain using Dijkstra
                            double shortestDistanceFeet = dijkstraEngine.ComputeShortestPath(i, drainIndices);

                            if (shortestDistanceFeet == double.PositiveInfinity)
                            {
                                payload.Log?.Invoke($"WARNING: Vertex {i} has no path to any drain - skipping");
                                verticesSkipped++;
                                continue;
                            }

                            // Calculate elevation based on slope percentage
                            // slopePercent is in % (e.g., 1.5 for 1.5%)
                            // Convert to decimal: 1.5% = 0.015
                            // elevation (ft) = distance (ft) * slope (decimal)
                            double elevationOffsetFt = shortestDistanceFeet * (payload.SlopePercent / 100.0);

                            // Check against roof thickness limit
                            if (Math.Abs(elevationOffsetFt) > maxAllowedOffsetFt)
                            {
                                double scaleFactor = maxAllowedOffsetFt / Math.Abs(elevationOffsetFt);
                                double originalOffset = elevationOffsetFt;
                                elevationOffsetFt *= scaleFactor;
                                payload.Log?.Invoke($"Vertex {i}: Offset scaled from {originalOffset:F3}ft to {elevationOffsetFt:F3}ft (thickness limit)");
                            }

                            // Track highest elevation
                            if (Math.Abs(elevationOffsetFt) > highestElevationFt)
                                highestElevationFt = Math.Abs(elevationOffsetFt);

                            // Convert feet to meters for display
                            double pathMeters = UnitUtils.ConvertFromInternalUnits(
                                shortestDistanceFeet,
                                UnitTypeId.Meters);

                            if (pathMeters > longestPathMeters)
                                longestPathMeters = pathMeters;

                            // Apply the elevation (negative for downward slope from high to low)
                            try
                            {
                                editor.ModifySubElement(vertex, -elevationOffsetFt);
                                verticesProcessed++;

                                if (i % 10 == 0) // Log progress every 10 vertices
                                {
                                    payload.Log?.Invoke($"Processed vertex {i}/{vertices.Count}: " +
                                                       $"distance={shortestDistanceFeet:F2}ft, " +
                                                       $"elevation={elevationOffsetFt:F3}ft");
                                }
                            }
                            catch (Exception ex)
                            {
                                payload.Log?.Invoke($"ERROR modifying vertex {i}: {ex.Message}");
                                verticesSkipped++;
                                continue;
                            }

                            // Convert for export (meters for CSV)
                            double elevationOffsetMeters = UnitUtils.ConvertFromInternalUnits(
                                elevationOffsetFt,
                                UnitTypeId.Meters);

                            // Add to export
                            exportContext.Rows.Add(new AutoSlopeVertexExportDto
                            {
                                RoofElementId = (int)roof.Id.Value,
                                DrainElementId = -1,
                                PointIndex = pointIndex,
                                PathLength = pathMeters,
                                SlopePercent = payload.SlopePercent,
                                ElevationOffset = elevationOffsetMeters,
                                Direction = "Down"
                            });

                            pointIndex++;
                        }

                        // Commit export
                        if (exportContext.Rows.Count > 0)
                        {
                            exportContext.Commit();
                            string exportPath = System.IO.Path.Combine(exportContext.ExportFolder,
                                $"AutoSlope_Report_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                            payload.Log?.Invoke($"CSV exported to: {exportPath}");
                        }
                        else
                        {
                            payload.Log?.Invoke("WARNING: No data to export");
                        }

                        stopwatch.Stop();

                        // Convert feet to mm for display
                        double highestElevationMm = UnitUtils.ConvertFromInternalUnits(
                            highestElevationFt,
                            UnitTypeId.Millimeters);

                        // Update ViewModel properties
                        if (payload.Vm != null)
                        {
                            payload.Vm.VerticesProcessed = verticesProcessed;
                            payload.Vm.VerticesSkipped = verticesSkipped;
                            payload.Vm.DrainCount = payload.DrainPoints.Count;
                            payload.Vm.RunDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                            payload.Vm.RunDuration_sec = (int)stopwatch.Elapsed.TotalSeconds;
                            payload.Vm.HighestElevation_mm = highestElevationMm;
                            payload.Vm.LongestPath_m = longestPathMeters;
                            payload.Vm.HasRun = false; // Reset to allow re-running
                        }

                        payload.Log?.Invoke("========================================");
                        payload.Log?.Invoke($"SLOPE CALCULATION COMPLETE");
                        payload.Log?.Invoke($"Time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
                        payload.Log?.Invoke($"Vertices processed: {verticesProcessed}");
                        payload.Log?.Invoke($"Vertices skipped: {verticesSkipped}");
                        payload.Log?.Invoke($"Highest elevation: {highestElevationFt:F3}ft ({highestElevationMm:F0}mm)");
                        payload.Log?.Invoke($"Longest path: {longestPathMeters:F2}m");
                        payload.Log?.Invoke("========================================");

                        tx.Commit();
                    }
                    catch (Exception ex)
                    {
                        tx.RollBack();
                        payload.Log?.Invoke($"ERROR during slope calculation: {ex.Message}");
                        payload.Log?.Invoke($"Stack trace: {ex.StackTrace}");
                        if (payload.Vm != null)
                            payload.Vm.HasRun = false; // Reset on error
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("External Event Error", ex.ToString());
            }
        }

        private double GetMaxAllowedOffset(RoofBase roof)
        {
            try
            {
                var roofType = _doc.GetElement(roof.GetTypeId()) as RoofType;
                if (roofType == null)
                {
                    AutoSlopeEventManager.Payload?.Log?.Invoke($"WARNING: Could not get roof type. Using default max offset: 1.0ft");
                    return 1.0; // Default 1ft if can't determine
                }

                // Try to get thickness parameter
                Parameter thicknessParam = roofType.LookupParameter("Structure Thickness");
                if (thicknessParam == null)
                    thicknessParam = roofType.LookupParameter("Thickness");

                if (thicknessParam != null && thicknessParam.HasValue)
                {
                    double thicknessFt = thicknessParam.AsDouble(); // Already in feet

                    // Log the thickness for debugging
                    double thicknessMm = UnitUtils.ConvertFromInternalUnits(
                        thicknessFt,
                        UnitTypeId.Millimeters);

                    AutoSlopeEventManager.Payload?.Log?.Invoke(
                        $"Roof thickness: {thicknessFt:F3}ft ({thicknessMm:F0}mm)");

                    // Conservative: use 60% of thickness for safety (Revit allows ~70-80%)
                    double maxOffset = thicknessFt * 0.6;

                    double maxOffsetMm = UnitUtils.ConvertFromInternalUnits(
                        maxOffset,
                        UnitTypeId.Millimeters);

                    AutoSlopeEventManager.Payload?.Log?.Invoke(
                        $"Maximum allowed offset: {maxOffset:F3}ft ({maxOffsetMm:F0}mm)");

                    return maxOffset;
                }
                else
                {
                    AutoSlopeEventManager.Payload?.Log?.Invoke(
                        $"WARNING: Could not determine roof thickness. Using default max offset: 1.0ft");
                    return 1.0; // Default 1ft fallback
                }
            }
            catch (Exception ex)
            {
                AutoSlopeEventManager.Payload?.Log?.Invoke(
                    $"WARNING: Error getting roof thickness: {ex.Message}. Using default max offset: 1.0ft");
                return 1.0; // Default fallback
            }
        }

        public string GetName()
        {
            return "AutoSlope External Event";
        }
    }
}