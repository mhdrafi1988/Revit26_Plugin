// =======================================================
// File: AutoSlopeEngine.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.V06.Core.Engine
//
// OPTIMIZED: Multi-source Dijkstra (one pass instead of per-vertex)
// =======================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlopeByPoint.V06.Core.Models;
using Revit26_Plugin.AutoSlopeByPoint.V06.Core.Parameters;
using Revit26_Plugin.AutoSlopeByPoint.V06.Infrastructure.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByPoint.V06.Core.Engine
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
                data.Log(LogColorHelper.Red("Roof slab shape editor is not available. Aborting."));
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
                data.Log(LogColorHelper.Red("Top face not found. Aborting."));
                FireFailure(data, "Top face not found.");
                return;
            }

            // ── Build final drain points ─────────────────────────────────────
            List<XYZ> finalDrainPoints = data.DrainPoints ?? new List<XYZ>();

            data.Log(LogColorHelper.Cyan($"DEBUG: Initial drain points count = {finalDrainPoints.Count}"));

            if (data.EnableDrainTolerance && data.DrainToleranceMm > 0)
            {
                data.Log(LogColorHelper.Cyan(
                    $"🔍 Checking for nearby roof shape points within {data.DrainToleranceMm}mm of selected points..."));

                finalDrainPoints = DrainDetectionHelper.DetectDrainsWithinRadius(
                    roof, finalDrainPoints, data.DrainToleranceMm, data.Log);

                finalDrainPoints = DrainDetectionHelper.RemoveDuplicates(
                    finalDrainPoints, data.DrainToleranceMm);

                data.Log(LogColorHelper.Cyan($"DEBUG: After tolerance expansion count = {finalDrainPoints.Count}"));
            }

            if (finalDrainPoints == null || finalDrainPoints.Count == 0)
            {
                data.Log(LogColorHelper.Red("No drain points are available. Aborting."));
                FireFailure(data, "No drain points are available.");
                return;
            }

            // ── Build Dijkstra graph ─────────────────────────────────────────
            var dijkstra = new DijkstraPathEngine(vertices, topFace, thresholdFt);

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

            data.Log(LogColorHelper.Cyan($"DEBUG: drainIndices count (vertices matching drains) = {drainIndices.Count}"));

            if (drainIndices.Count == 0)
            {
                data.Log(LogColorHelper.Red("No roof vertices matched the selected drain points. Aborting."));
                FireFailure(data, "No roof vertices matched the selected drain points.");
                return;
            }

            // ── OPTIMIZATION: Single multi-source Dijkstra ───────────────────
            double[] distances = dijkstra.ComputeAllDistances(drainIndices);

            // ── Main slope loop (now O(V) with precomputed distances) ────────
            int processed = 0;
            int skipped = 0;
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
                                VertexIndex = i,
                                Position = vertices[i].Position,
                                PathLengthMeters = double.IsInfinity(pathFt)
                                    ? 0
                                    : UnitUtils.ConvertFromInternalUnits(pathFt, UnitTypeId.Meters),
                                ElevationOffsetMm = 0,
                                ElevationFromModel_mm = 0,
                                NearestDrainIndex = -1,
                                DirectionVector = XYZ.Zero,
                                WasProcessed = false
                            });
                        }
                        continue;
                    }

                    double elevFt = pathFt * slopeFactor;
                    editor.ModifySubElement(vertices[i], elevFt);

                    processed++;
                    if (pathFt > maxPathFt) maxPathFt = pathFt;

                    int nearestDrainIndex = FindNearestDrainIndex(vertices[i].Position, finalDrainPoints);
                    XYZ directionVector = nearestDrainIndex >= 0
                        ? CalculateDirectionVector(vertices[i].Position, finalDrainPoints[nearestDrainIndex])
                        : XYZ.Zero;

                    vertexDataList.Add(new VertexData
                    {
                        VertexIndex = i,
                        Position = vertices[i].Position,
                        PathLengthMeters = UnitUtils.ConvertFromInternalUnits(pathFt, UnitTypeId.Meters),
                        ElevationOffsetMm = UnitUtils.ConvertFromInternalUnits(elevFt, UnitTypeId.Millimeters),
                        ElevationFromModel_mm = 0,
                        NearestDrainIndex = nearestDrainIndex,
                        DirectionVector = directionVector,
                        WasProcessed = true
                    });
                }

                tx.Commit();
            }

            // ── Re-read vertices from Revit after commit ────────────────────
            double maxElevFt = 0;

            var refreshedVertices = new List<SlabShapeVertex>();
            foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                refreshedVertices.Add(v);

            data.Log(LogColorHelper.Cyan($"DEBUG: Refreshed vertex count after commit = {refreshedVertices.Count}"));

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

                    if (elevFromModelFt > maxElevFt)
                        maxElevFt = elevFromModelFt;
                }
                else
                {
                    vd.ElevationFromModel_mm = vd.ElevationOffsetMm;
                    data.Log(LogColorHelper.Yellow(
                        $"WARN: Could not match refreshed vertex for index {vd.VertexIndex}, using calculated value."));
                }
            }

            data.Log(LogColorHelper.Cyan($"DEBUG: maxElevFt from model re-read = {maxElevFt:F6} ft"));

            sw.Stop();

            int highest_mm = (int)Math.Round(
                UnitUtils.ConvertFromInternalUnits(maxElevFt, UnitTypeId.Millimeters),
                MidpointRounding.AwayFromZero);
            double longest_m = Math.Round(
                UnitUtils.ConvertFromInternalUnits(maxPathFt, UnitTypeId.Meters),
                2, MidpointRounding.AwayFromZero);
            int durationSec = (int)Math.Round(sw.Elapsed.TotalSeconds);

            string runDate = DateTime.Now.ToString("dd-MM-yy HH:mm");

            AutoSlopeParameterWriter.WriteAll(
                doc, roof, data,
                highest_mm, maxPathFt,
                processed, skipped, durationSec,
                finalDrainPoints.Count,
                runDate,
                "P.04.00");

            string compactPath = null;
            if (data.ExportConfig?.ExportToExcel == true)
            {
                compactPath = ExcelExportService.ExportCompactVertexData(
                    data, vertexDataList, roof, data.SlopePercent);

                if (!string.IsNullOrEmpty(compactPath))
                {
                    data.Log(LogColorHelper.Green($"✅ Compact Excel exported to: {compactPath}"));
                    data.Log(LogColorHelper.Cyan("  • Sorted by PathLength_Meters (longest first)"));
                    data.Log(LogColorHelper.Cyan($"  • Contains {processed} processed vertices"));

                    var longestVertex = vertexDataList
                        .Where(v => v.WasProcessed)
                        .OrderByDescending(v => v.PathLengthMeters)
                        .FirstOrDefault();

                    if (longestVertex != null)
                        data.Log(LogColorHelper.Cyan(
                            $"  • Longest path: {longestVertex.PathLengthMeters:F2} m to drain {longestVertex.NearestDrainIndex}"));
                }

                if (data.ExportConfig.IncludeVertexDetails)
                {
                    string detailedPath = ExcelExportService.ExportDetailedVertexData(
                        data, vertexDataList, roof, finalDrainPoints, data.SlopePercent);

                    if (!string.IsNullOrEmpty(detailedPath))
                    {
                        data.Log(LogColorHelper.Green($"✅ Detailed Excel exported to: {detailedPath}"));
                        data.Log(LogColorHelper.Cyan(
                            $"  • {vertexDataList.Count} total vertices ({processed} processed, {skipped} skipped)"));
                        data.Log(LogColorHelper.Cyan("  • Sheets: Summary, Drain Points, Vertices, Statistics"));
                    }
                }
            }

            data.Log(LogColorHelper.Cyan("===== AutoSlope Summary ====="));
            data.Log(LogColorHelper.Green($"Applied Slope Percentage : {data.SlopePercent}%"));
            data.Log(LogColorHelper.Green($"Vertices Processed       : {processed}"));
            data.Log(LogColorHelper.Yellow($"Vertices Skipped         : {skipped}"));
            data.Log(LogColorHelper.Cyan($"Highest Elevation        : {highest_mm:0} mm  ← from model re-read"));
            data.Log(LogColorHelper.Cyan($"Longest Path             : {longest_m:0.00} m"));
            data.Log(LogColorHelper.Cyan($"Picked Drain Count       : {data.PickedDrainPoints?.Count ?? 0}"));
            data.Log(LogColorHelper.Cyan($"Final Drain Count        : {finalDrainPoints.Count}"));
            data.Log(LogColorHelper.Cyan($"Run Duration             : {durationSec} sec"));
            data.Log(LogColorHelper.Cyan($"Run Date                 : {runDate}"));
            if (data.EnableDrainTolerance)
                data.Log(LogColorHelper.Cyan($"Drain Tolerance          : {data.DrainToleranceMm} mm (enabled)"));
            data.Log(LogColorHelper.Green("===== AutoSlope Finished Successfully ====="));

            data.OnCompleted?.Invoke(new AutoSlopeResult
            {
                Success = true,
                VerticesProcessed = processed,
                VerticesSkipped = skipped,
                PickedDrainCount = data.PickedDrainPoints?.Count ?? 0,
                FinalDrainCount = finalDrainPoints.Count,
                HighestElevation_mm = highest_mm,
                LongestPath_m = longest_m,
                RunDuration_sec = durationSec,
                RunDate = runDate,
                ExportedFilePath = compactPath
            });
        }

        private static void FireFailure(AutoSlopePayload data, string reason)
        {
            data.Log?.Invoke(LogColorHelper.Red(reason));
            data.Log?.Invoke(LogColorHelper.Red("DEBUG: Firing failure callback"));
            data.OnCompleted?.Invoke(new AutoSlopeResult
            {
                Success = false,
                ErrorMessage = reason,
                PickedDrainCount = 0,
                FinalDrainCount = 0
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
                double distance = vertexPos.DistanceTo(drainPoints[i]);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestIndex = i;
                }
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