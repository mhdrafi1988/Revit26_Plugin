using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlope.V5_00.Commands;
using Revit26_Plugin.AutoSlope.V5_00.Core.Models;
using Revit26_Plugin.AutoSlope.V5_00.Core.Parameters;
using Revit26_Plugin.AutoSlope.V5_00.Infrastructure.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Revit26_Plugin.AutoSlope.V5_00.Core.Engine
{
    public class SlopeProcessorService
    {
        public AutoSlopeMetrics ProcessRoofSlopes(AutoSlopePayload payload)
        {
            var metrics = new AutoSlopeMetrics();
            var stopwatch = Stopwatch.StartNew();

            Document doc = payload.RoofData.Roof.Document;
            RoofBase roof = payload.RoofData.Roof;
            List<SlabShapeVertex> vertices = payload.RoofData.Vertices;
            Face topFace = payload.RoofData.TopFace;
            List<DrainItem> selectedDrains = payload.SelectedDrains;

            payload.Log(LogColorHelper.Cyan("═══════════════════════════════════════"));
            payload.Log(LogColorHelper.Cyan("     PATHFINDING & SLOPE CALCULATION"));
            payload.Log(LogColorHelper.Cyan("═══════════════════════════════════════"));

            // STEP 1: Build graph (READ-ONLY, NO TRANSACTION)
            payload.Log(LogColorHelper.Yellow("📊 Building connectivity graph..."));
            var graphBuilder = new GraphBuilderService(payload.ThresholdMeters);
            var graph = graphBuilder.BuildGraph(vertices, topFace);
            payload.Log(LogColorHelper.Green($"✓ Graph built with {graph.Count} nodes"));

            // STEP 2: Identify drain vertices (READ-ONLY, NO TRANSACTION)
            payload.Log(LogColorHelper.Yellow("🔍 Identifying drain vertices..."));
            var drainIndices = FindDrainVertices(vertices, selectedDrains, topFace);
            payload.Log(LogColorHelper.Green($"✓ Found {drainIndices.Count} drain vertices"));

            // STEP 3: Set drain vertices to zero (WRITE - SEPARATE TRANSACTION)
            payload.Log(LogColorHelper.Yellow("📏 Setting drain vertices to zero elevation..."));

            var drainResult = SetDrainVerticesToZero(roof, vertices, drainIndices, doc, payload);
            if (!drainResult.Success)
            {
                payload.Log(LogColorHelper.Red($"✗ Failed to set drain vertices: {drainResult.Error}"));
                return metrics;
            }

            metrics.Processed = drainIndices.Count;
            payload.Log(LogColorHelper.Green($"✓ Set {drainIndices.Count} drain vertices to zero"));

            // STEP 4: Compute paths and apply slopes (MAIN TRANSACTION)
            payload.Log(LogColorHelper.Yellow("🛤️ Computing shortest paths to drains..."));

            var slopeResult = ApplySlopesToVertices(
                roof, vertices, graph, drainIndices,
                selectedDrains, payload, doc, ref metrics);

            if (!slopeResult.Success)
            {
                payload.Log(LogColorHelper.Red($"✗ Failed to apply slopes: {slopeResult.Error}"));
                return metrics;
            }

            // STEP 5: Write parameters to roof (SEPARATE TRANSACTION)
            if (slopeResult.Success)
            {
                var writer = new ParameterWriterService();
                var writeResult = writer.WriteAll(doc, payload, metrics);

                if (writeResult.HasFailures)
                {
                    payload.Log(LogColorHelper.Yellow($"⚠ Some parameters could not be written ({writeResult.FailCount} failed)"));
                }
            }

            stopwatch.Stop();
            metrics.DurationSeconds = (int)stopwatch.Elapsed.TotalSeconds;

            return metrics;
        }

        private TransactionResult SetDrainVerticesToZero(
            RoofBase roof,
            List<SlabShapeVertex> vertices,
            HashSet<int> drainIndices,
            Document doc,
            AutoSlopePayload payload)
        {
            var result = new TransactionResult();

            using (Transaction tx = new Transaction(doc, "Set Drain Vertices to Zero"))
            {
                // Set failure handling
                var options = tx.GetFailureHandlingOptions();
                options.SetFailuresPreprocessor(new SlopeWarningSwallower());
                tx.SetFailureHandlingOptions(options);

                tx.Start();
                try
                {
                    var editor = roof.GetSlabShapeEditor();

                    // Get a stable list of vertices to modify
                    var drainVertices = drainIndices.Select(idx => vertices[idx]).ToList();

                    foreach (var vertex in drainVertices)
                    {
                        editor.ModifySubElement(vertex, 0.0);
                    }

                    tx.Commit();
                    result.Success = true;
                }
                catch (Exception ex)
                {
                    tx.RollBack();
                    result.Success = false;
                    result.Error = ex.Message;
                }
            }

            return result;
        }

        private TransactionResult ApplySlopesToVertices(
            RoofBase roof,
            List<SlabShapeVertex> vertices,
            Dictionary<int, List<int>> graph,
            HashSet<int> drainIndices,
            List<DrainItem> selectedDrains,
            AutoSlopePayload payload,
            Document doc,
            ref AutoSlopeMetrics metrics)
        {
            var result = new TransactionResult();

            using (Transaction tx = new Transaction(doc, "Apply AutoSlope Elevations"))
            {
                // Set failure handling
                var options = tx.GetFailureHandlingOptions();
                options.SetFailuresPreprocessor(new SlopeWarningSwallower());
                tx.SetFailureHandlingOptions(options);

                tx.Start();
                try
                {
                    var editor = roof.GetSlabShapeEditor();
                    var pathSolver = new PathSolverService(vertices, graph);
                    double slopeFactor = payload.SlopePercent / 100.0;
                    double maxElevation = 0;
                    double longestPath = 0;
                    int processedCount = 0;
                    int skippedCount = 0;

                    var vertexDataList = new List<VertexData>();

                    // Process vertices in batches to avoid long transactions
                    const int batchSize = 50;
                    var nonDrainIndices = Enumerable.Range(0, vertices.Count)
                        .Where(i => !drainIndices.Contains(i))
                        .ToList();

                    for (int batchStart = 0; batchStart < nonDrainIndices.Count; batchStart += batchSize)
                    {
                        var batch = nonDrainIndices.Skip(batchStart).Take(batchSize).ToList();

                        foreach (int i in batch)
                        {
                            double pathLength = pathSolver.ComputeShortestPath(i, drainIndices);

                            if (double.IsInfinity(pathLength) || pathLength > payload.ThresholdMeters * 3.28084)
                            {
                                skippedCount++;
                                if (payload.ExportConfig?.IncludeVertexDetails == true)
                                {
                                    vertexDataList.Add(new VertexData
                                    {
                                        VertexIndex = i,
                                        Position = vertices[i].Position,
                                        PathLengthMeters = pathLength / 3.28084,
                                        ElevationOffsetMm = 0,
                                        NearestDrainId = -1,
                                        WasProcessed = false
                                    });
                                }
                                continue;
                            }

                            double elevationFeet = pathLength * slopeFactor;
                            editor.ModifySubElement(vertices[i], elevationFeet);

                            double elevationMm = elevationFeet * 304.8;
                            double pathMeters = pathLength / 3.28084;

                            if (elevationMm > maxElevation)
                                maxElevation = elevationMm;

                            if (pathMeters > longestPath)
                                longestPath = pathMeters;

                            processedCount++;

                            if (payload.ExportConfig?.IncludeVertexDetails == true)
                            {
                                int nearestDrain = FindNearestDrainIndex(vertices[i].Position, selectedDrains);
                                XYZ direction = CalculateDirectionVector(vertices[i].Position,
                                    selectedDrains[nearestDrain].CenterPoint);

                                vertexDataList.Add(new VertexData
                                {
                                    VertexIndex = i,
                                    Position = vertices[i].Position,
                                    PathLengthMeters = pathMeters,
                                    ElevationOffsetMm = elevationMm,
                                    NearestDrainId = selectedDrains[nearestDrain].DrainId,
                                    DirectionVector = direction,
                                    WasProcessed = true
                                });
                            }
                        }

                        // Log progress every batch
                        payload.Log(LogColorHelper.Cyan($"  Progress: {processedCount + skippedCount}/{nonDrainIndices.Count} vertices processed..."));
                    }

                    metrics.Processed += processedCount;
                    metrics.Skipped = skippedCount;
                    metrics.HighestElevation = maxElevation;
                    metrics.LongestPath = longestPath;

                    tx.Commit();
                    result.Success = true;

                    payload.Log(LogColorHelper.Green($"✓ Processed {processedCount} slope vertices"));
                    if (skippedCount > 0)
                        payload.Log(LogColorHelper.Yellow($"⚠ Skipped {skippedCount} vertices (beyond threshold)"));

                    // Export CSV after transaction (no Revit API calls)
                    if (payload.ExportConfig != null && payload.ExportConfig.ExportToCsv)
                    {
                        ExportCsvResults(payload, vertexDataList, selectedDrains, metrics);
                    }
                }
                catch (Exception ex)
                {
                    tx.RollBack();
                    result.Success = false;
                    result.Error = ex.Message;
                }
            }

            return result;
        }

        private HashSet<int> FindDrainVertices(
            List<SlabShapeVertex> vertices,
            List<DrainItem> drains,
            Face topFace)
        {
            var drainIndices = new HashSet<int>();

            for (int i = 0; i < vertices.Count; i++)
            {
                XYZ pos = vertices[i].Position;

                foreach (var drain in drains)
                {
                    double halfWidth = (drain.Width / 304.8) / 2;
                    double halfHeight = (drain.Height / 304.8) / 2;

                    if (pos.X >= drain.CenterPoint.X - halfWidth &&
                        pos.X <= drain.CenterPoint.X + halfWidth &&
                        pos.Y >= drain.CenterPoint.Y - halfHeight &&
                        pos.Y <= drain.CenterPoint.Y + halfHeight)
                    {
                        if (GeometryHelper.IsPointOnFace(pos, topFace))
                        {
                            drainIndices.Add(i);
                            break;
                        }
                    }
                }
            }

            return drainIndices;
        }

        private int FindNearestDrainIndex(XYZ vertexPos, List<DrainItem> drains)
        {
            int nearest = 0;
            double minDist = double.MaxValue;

            for (int i = 0; i < drains.Count; i++)
            {
                double dist = vertexPos.DistanceTo(drains[i].CenterPoint);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = i;
                }
            }
            return nearest;
        }

        private XYZ CalculateDirectionVector(XYZ from, XYZ to)
        {
            if (from.DistanceTo(to) < 0.001)
                return XYZ.Zero;

            return (to - from).Normalize();
        }

        private void ExportCsvResults(
            AutoSlopePayload payload,
            List<VertexData> vertexData,
            List<DrainItem> drains,
            AutoSlopeMetrics metrics)
        {
            try
            {
                // Export outside of transaction
                string compactPath = CsvExportHelper.ExportCompactData(
                    payload, vertexData, payload.RoofData.Roof, payload.SlopePercent);

                if (!string.IsNullOrEmpty(compactPath))
                {
                    payload.Log(LogColorHelper.Green($"📄 Compact CSV exported: {compactPath}"));
                }

                if (payload.ExportConfig.IncludeVertexDetails)
                {
                    string detailedPath = CsvExportHelper.ExportDetailedData(
                        payload, vertexData, payload.RoofData.Roof, drains, payload.SlopePercent);

                    if (!string.IsNullOrEmpty(detailedPath))
                    {
                        payload.Log(LogColorHelper.Green($"📄 Detailed CSV exported: {detailedPath}"));
                    }
                }

                string summaryPath = CsvExportHelper.ExportSummaryOnly(payload, metrics, payload.RoofData.Roof);

                if (!string.IsNullOrEmpty(summaryPath))
                {
                    payload.Log(LogColorHelper.Green($"📄 Summary CSV exported: {summaryPath}"));
                }
            }
            catch (Exception ex)
            {
                payload.Log(LogColorHelper.Yellow($"⚠ CSV Export warning: {ex.Message}"));
            }
        }

        private class TransactionResult
        {
            public bool Success { get; set; }
            public string Error { get; set; }
        }
    }
}