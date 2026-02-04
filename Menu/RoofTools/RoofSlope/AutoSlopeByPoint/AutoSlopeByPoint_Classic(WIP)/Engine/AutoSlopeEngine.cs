using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit_26.CornertoDrainArrow_V05;
using Revit26_Plugin.AutoSlopeByPoint_WIP2.Models;
using Revit26_Plugin.AutoSlopeByPoint_WIP2.Parameters;
using Revit26_Plugin.AutoSlopeByPoint_WIP2.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByPoint_WIP2.Engine
{
    public static class AutoSlopeEngine
    {
        public static void Execute(UIApplication app, AutoSlopePayload payload)
        {
            Document doc = app.ActiveUIDocument.Document;
            RoofBase roof = doc.GetElement(payload.RoofId) as RoofBase;

            if (roof == null)
            {
                payload.LogCallback?.Invoke("? Roof element not found. Aborting.");
                return;
            }

            try
            {
                payload.LogCallback?.Invoke("?? Initializing AutoSlope Engine...");

                // Get shape editor
                SlabShapeEditor editor = roof.GetSlabShapeEditor();

                if (!editor.IsEnabled)
                {
                    using (Transaction tx = new Transaction(doc, "Enable Shape Editing"))
                    {
                        tx.Start();
                        editor.Enable();
                        tx.Commit();
                    }
                }

                // Collect vertices
                List<SlabShapeVertex> vertices = editor.SlabShapeVertices.Cast<SlabShapeVertex>().ToList();
                payload.LogCallback?.Invoke($"?? Found {vertices.Count} roof vertices");

                // Reset vertices to zero elevation
                ResetVertices(doc, editor, vertices, payload.LogCallback);

                // Get top face for validation
                Face topFace = GeometryHelper.GetTopFace(roof);
                if (topFace == null)
                {
                    payload.LogCallback?.Invoke("? Could not find roof top face. Aborting.");
                    return;
                }

                // Convert threshold to internal units (feet)
                double thresholdFeet = UnitUtils.ConvertToInternalUnits(
                    payload.ThresholdMeters,
                    UnitTypeId.Meters);

                // Find drain indices
                HashSet<int> drainIndices = FindDrainIndices(vertices, payload.DrainPoints, payload.LogCallback);

                // Calculate slope factor
                double slopeFactor = payload.SlopePercent / 100.0;

                // Initialize Dijkstra engine
                var dijkstra = new DijkstraPathEngine(vertices, topFace, thresholdFeet);

                // Process vertices
                var metrics = ProcessVertices(
                    doc,
                    editor,
                    vertices,
                    dijkstra,
                    drainIndices,
                    slopeFactor,
                    thresholdFeet,
                    payload.LogCallback);

                // Update UI with results
                UpdateViewModel(payload, metrics);

                // Write parameters to roof
                WriteRoofParameters(doc, roof, payload, metrics);

                // Export to CSV if folder specified
                ExportToCsv(doc, payload, metrics);

                payload.LogCallback?.Invoke("? AutoSlope completed successfully!");
            }
            catch (Exception ex)
            {
                payload.LogCallback?.Invoke($"?? Error: {ex.Message}");
                payload.LogCallback?.Invoke($"Stack Trace: {ex.StackTrace}");
            }
        }

        private static void ResetVertices(
            Document doc,
            SlabShapeEditor editor,
            List<SlabShapeVertex> vertices,
            Action<string> logCallback)
        {
            logCallback?.Invoke("?? Resetting vertices to zero elevation...");

            using (Transaction tx = new Transaction(doc, "Reset Roof Vertices"))
            {
                tx.Start();
                foreach (SlabShapeVertex vertex in vertices)
                {
                    editor.ModifySubElement(vertex, 0);
                }
                tx.Commit();
            }

            logCallback?.Invoke($"? Reset {vertices.Count} vertices");
        }

        private static HashSet<int> FindDrainIndices(
            List<SlabShapeVertex> vertices,
            List<XYZ> drainPoints,
            Action<string> logCallback)
        {
            var drainIndices = new HashSet<int>();
            const double toleranceFeet = 0.5; // ~15cm tolerance

            for (int i = 0; i < vertices.Count; i++)
            {
                foreach (XYZ drainPoint in drainPoints)
                {
                    if (vertices[i].Position.DistanceTo(drainPoint) < toleranceFeet)
                    {
                        drainIndices.Add(i);
                        break;
                    }
                }
            }

            logCallback?.Invoke($"?? Found {drainIndices.Count} drain points matching vertices");
            return drainIndices;
        }

        private static AutoSlopeMetrics ProcessVertices(
            Document doc,
            SlabShapeEditor editor,
            List<SlabShapeVertex> vertices,
            DijkstraPathEngine dijkstra,
            HashSet<int> drainIndices,
            double slopeFactor,
            double thresholdFeet,
            Action<string> logCallback)
        {
            logCallback?.Invoke("?? Processing vertices with Dijkstra algorithm...");

            var metrics = new AutoSlopeMetrics();
            var stopwatch = Stopwatch.StartNew();

            using (Transaction tx = new Transaction(doc, "Apply AutoSlope Elevations"))
            {
                tx.Start();

                for (int i = 0; i < vertices.Count; i++)
                {
                    // Calculate shortest path to nearest drain
                    double pathFeet = dijkstra.ComputeShortestPath(i, drainIndices);

                    if (double.IsInfinity(pathFeet) || pathFeet > thresholdFeet)
                    {
                        metrics.Skipped++;
                        continue;
                    }

                    // Calculate elevation
                    double elevationFeet = pathFeet * slopeFactor;

                    // Apply elevation to vertex
                    editor.ModifySubElement(vertices[i], elevationFeet);

                    metrics.Processed++;

                    // Update metrics
                    if (elevationFeet > metrics.HighestElevation)
                        metrics.HighestElevation = elevationFeet;

                    if (pathFeet > metrics.LongestPath)
                        metrics.LongestPath = pathFeet;
                }

                tx.Commit();
            }

            stopwatch.Stop();
            metrics.RunDurationSeconds = (int)stopwatch.Elapsed.TotalSeconds;

            // Convert units for display
            metrics.HighestElevation = UnitUtils.ConvertFromInternalUnits(
                metrics.HighestElevation,
                UnitTypeId.Millimeters);

            metrics.LongestPath = UnitUtils.ConvertFromInternalUnits(
                metrics.LongestPath,
                UnitTypeId.Meters);

            logCallback?.Invoke($"?? Processed: {metrics.Processed}, Skipped: {metrics.Skipped}");
            logCallback?.Invoke($"??  Duration: {metrics.RunDurationSeconds} seconds");

            return metrics;
        }

        private static void UpdateViewModel(AutoSlopePayload payload, AutoSlopeMetrics metrics)
        {
            payload.ViewModel.VerticesProcessed = metrics.Processed;
            payload.ViewModel.VerticesSkipped = metrics.Skipped;
            payload.ViewModel.DrainCount = payload.DrainPoints.Count;
            payload.ViewModel.HighestElevationMm = metrics.HighestElevation;
            payload.ViewModel.LongestPathM = metrics.LongestPath;
            payload.ViewModel.RunDurationSec = metrics.RunDurationSeconds;
            payload.ViewModel.RunDate = DateTime.Now.ToString("dd-MM-yy HH:mm");
        }

        private static void WriteRoofParameters(
            Document doc,
            RoofBase roof,
            AutoSlopePayload payload,
            AutoSlopeMetrics metrics)
        {
            try
            {
                AutoSlopeParameterWriter.WriteAll(
                    doc,
                    roof,
                    payload,
                    metrics.HighestElevation,
                    metrics.LongestPath,
                    metrics.Processed,
                    metrics.Skipped,
                    metrics.RunDurationSeconds);

                payload.LogCallback?.Invoke("?? Parameters written to roof element");
            }
            catch (Exception ex)
            {
                payload.LogCallback?.Invoke($"?? Failed to write parameters: {ex.Message}");
            }
        }

        private static void ExportToCsv(Document doc, AutoSlopePayload payload, AutoSlopeMetrics metrics)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(payload.ExportFolderPath))
                {
                    string filePath = CsvExportService.ExportResultsToCsv(
                        payload.ExportFolderPath,
                        doc,
                        payload.RoofId,
                        metrics,
                        payload.SlopePercent,
                        payload.ThresholdMeters,
                        payload.DrainPoints);

                    payload.LogCallback?.Invoke($"?? Report exported to: {filePath}");
                }
            }
            catch (Exception ex)
            {
                payload.LogCallback?.Invoke($"?? CSV export failed: {ex.Message}");
            }
        }
    }
}