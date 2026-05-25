// =======================================================
// File: AutoSlopeEngine.cs
// Fixes:
//   #6  DateTime.Now captured once into `runDate` and reused.
//   #10 Removed excessive debug log spam; key boundaries kept.
//   #11 Computes AvgSlopePercent and populates Percentage2Applied
//       in AutoSlopeResult so the ViewModel/UI can display them.
// =======================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlopeByPoint_04.Core.Models;
using Revit26_Plugin.AutoSlopeByPoint_04.Core.Parameters;
using Revit26_Plugin.AutoSlopeByPoint_04.Infrastructure.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByPoint_04.Core.Engine
{
    public static class AutoSlopeEngine
    {
        public static void Execute(UIApplication app, AutoSlopePayload data)
        {
            Document doc = app.ActiveUIDocument.Document;

            // ── Guard: roof ──────────────────────────────────────────────────
            RoofBase roof = doc.GetElement(data.RoofId) as RoofBase;
            if (roof == null)
            {
                FireFailure(data, "Roof element not found. Aborting.");
                return;
            }

            // ── Guard: slab shape editor ─────────────────────────────────────
            SlabShapeEditor editor = roof.GetSlabShapeEditor();
            if (editor == null || !editor.IsValidObject)
            {
                FireFailure(data, "Roof slab shape editor is not available. Aborting.");
                return;
            }

            // ── Reset vertices ────────────────────────────────────────────────
            using (Transaction tx = new Transaction(doc, "Reset Roof Vertices"))
            {
                tx.Start();
                foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                    editor.ModifySubElement(v, 0);
                tx.Commit();
            }

            // ── Collect vertices ──────────────────────────────────────────────
            var vertices = new List<SlabShapeVertex>();
            foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                vertices.Add(v);

            double slopeFactor  = data.SlopePercent / 100.0;
            double thresholdFt  = UnitUtils.ConvertToInternalUnits(data.ThresholdMeters, UnitTypeId.Meters);

            // ── Guard: top face ───────────────────────────────────────────────
            Face topFace = AutoSlopeGeometry.GetTopFace(roof);
            if (topFace == null)
            {
                FireFailure(data, "Top face not found. Aborting.");
                return;
            }

            // ── Build final drain points ──────────────────────────────────────
            List<XYZ> finalDrainPoints = data.DrainPoints ?? new List<XYZ>();

            if (data.EnableDrainTolerance && data.DrainToleranceMm > 0)
            {
                data.Log(LogColorHelper.Cyan(
                    $"🔍 Checking for nearby roof shape points within {data.DrainToleranceMm}mm of selected points..."));

                finalDrainPoints = DrainDetectionHelper.DetectDrainsWithinRadius(
                    roof, finalDrainPoints, data.DrainToleranceMm, data.Log);

                finalDrainPoints = DrainDetectionHelper.RemoveDuplicates(
                    finalDrainPoints, data.DrainToleranceMm);
            }

            if (finalDrainPoints == null || finalDrainPoints.Count == 0)
            {
                FireFailure(data, "No drain points are available. Aborting.");
                return;
            }

            data.Log(LogColorHelper.Cyan($"Total drain points (full list): {finalDrainPoints.Count}"));

            // ── Build Dijkstra graph ──────────────────────────────────────────
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

            if (drainIndices.Count == 0)
            {
                FireFailure(data, "No roof vertices matched the selected drain points. Aborting.");
                return;
            }

            // ── Main slope loop ───────────────────────────────────────────────
            int    processed  = 0;
            int    skipped    = 0;
            double maxElevFt  = 0;
            double maxPathFt  = 0;
            double sumSlopePct = 0;          // for AvgSlopePercent (#11)
            var vertexDataList = new List<VertexData>();
            Stopwatch sw = Stopwatch.StartNew();

            using (Transaction tx = new Transaction(doc, "Apply AutoSlope"))
            {
                tx.Start();

                for (int i = 0; i < vertices.Count; i++)
                {
                    double pathFt = dijkstra.ComputeShortestPath(i, drainIndices);

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
                                ElevationOffsetMm = 0,
                                NearestDrainIndex = -1,
                                DirectionVector   = XYZ.Zero,
                                WasProcessed      = false
                            });
                        }
                        continue;
                    }

                    double elevFt = pathFt * slopeFactor;
                    editor.ModifySubElement(vertices[i], elevFt);

                    processed++;
                    if (elevFt > maxElevFt) maxElevFt = elevFt;
                    if (pathFt > maxPathFt) maxPathFt = pathFt;

                    // Accumulate actual slope% for this vertex (#11)
                    if (pathFt > 1e-9)
                        sumSlopePct += (elevFt / pathFt) * 100.0;

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
                        NearestDrainIndex = nearestDrainIndex,
                        DirectionVector   = directionVector,
                        WasProcessed      = true
                    });
                }

                tx.Commit();
            }

            sw.Stop();

            // ── Compute summary values ────────────────────────────────────────
            int    highest_mm  = (int)Math.Round(
                UnitUtils.ConvertFromInternalUnits(maxElevFt, UnitTypeId.Millimeters),
                MidpointRounding.AwayFromZero);
            double longest_m   = Math.Round(
                UnitUtils.ConvertFromInternalUnits(maxPathFt, UnitTypeId.Meters),
                2, MidpointRounding.AwayFromZero);
            int    durationSec = (int)Math.Round(sw.Elapsed.TotalSeconds);

            // Fix #11: average slope across processed vertices
            double avgSlopePct = processed > 0
                ? Math.Round(sumSlopePct / processed, 2)
                : 0.0;

            // Fix #6: capture once — used by both WriteAll and OnCompleted
            string runDate = DateTime.Now.ToString("dd-MM-yy HH:mm");

            // ── Write Revit parameters ────────────────────────────────────────
            AutoSlopeParameterWriter.WriteAll(
                doc, roof, data,
                highest_mm, maxPathFt,
                processed, skipped, durationSec,
                finalDrainPoints.Count,
                runDate,
                "P.04.00");

            // ── Excel export ──────────────────────────────────────────────────
            if (data.ExportConfig?.ExportToExcel == true)
            {
                string compactPath = ExcelExportHelper.ExportCompactVertexData(
                    data, vertexDataList, roof, data.SlopePercent);

                if (!string.IsNullOrEmpty(compactPath))
                {
                    data.Log(LogColorHelper.Green($"✅ Compact Excel exported to: {compactPath}"));
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
                    string detailedPath = ExcelExportHelper.ExportDetailedVertexData(
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

            // ── Log summary ───────────────────────────────────────────────────
            data.Log(LogColorHelper.Cyan("===== AutoSlope Summary ====="));
            data.Log(LogColorHelper.Green($"Applied Slope Percentage : {data.SlopePercent}%"));
            data.Log(LogColorHelper.Green($"Vertices Processed       : {processed}"));
            data.Log(LogColorHelper.Yellow($"Vertices Skipped         : {skipped}"));
            data.Log(LogColorHelper.Cyan($"Highest Elevation        : {highest_mm:0} mm"));
            data.Log(LogColorHelper.Cyan($"Longest Path             : {longest_m:0.00} m"));
            data.Log(LogColorHelper.Cyan($"Picked Drain Count       : {data.PickedDrainPoints?.Count ?? data.DrainPoints?.Count ?? 0}"));
            data.Log(LogColorHelper.Cyan($"Final Drain Count        : {finalDrainPoints.Count}"));
            data.Log(LogColorHelper.Cyan($"Avg Slope Applied        : {avgSlopePct:F2}%"));
            data.Log(LogColorHelper.Cyan($"Run Duration             : {durationSec} sec"));
            data.Log(LogColorHelper.Cyan($"Run Date                 : {runDate}"));
            if (data.EnableDrainTolerance)
                data.Log(LogColorHelper.Cyan($"Drain Tolerance          : {data.DrainToleranceMm} mm (enabled)"));
            data.Log(LogColorHelper.Green("===== AutoSlope Finished Successfully ====="));

            // ── Fire result callback ──────────────────────────────────────────
            data.OnCompleted?.Invoke(new AutoSlopeResult
            {
                Success            = true,
                VerticesProcessed  = processed,
                VerticesSkipped    = skipped,
                PickedDrainCount   = data.PickedDrainPoints?.Count ?? data.DrainPoints?.Count ?? 0,
                FinalDrainCount    = finalDrainPoints.Count,   // full drain list
                HighestElevation_mm = highest_mm,
                LongestPath_m      = longest_m,
                RunDuration_sec    = durationSec,
                RunDate            = runDate,
                AvgSlopePercent    = avgSlopePct,              // Fix #11
                Percentage2Applied = 0.0                       // Fix #11: reserved
            });
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static void FireFailure(AutoSlopePayload data, string reason)
        {
            data.Log?.Invoke(LogColorHelper.Red(reason));
            data.OnCompleted?.Invoke(new AutoSlopeResult
            {
                Success        = false,
                ErrorMessage   = reason,
                PickedDrainCount = 0,
                FinalDrainCount  = 0
            });
        }

        private static int FindNearestDrainIndex(XYZ vertexPos, List<XYZ> drainPoints)
        {
            if (drainPoints == null || drainPoints.Count == 0) return -1;

            int    nearestIndex = 0;
            double minDistance  = double.MaxValue;

            for (int i = 0; i < drainPoints.Count; i++)
            {
                if (drainPoints[i] == null) continue;
                double distance = vertexPos.DistanceTo(drainPoints[i]);
                if (distance < minDistance)
                {
                    minDistance  = distance;
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
