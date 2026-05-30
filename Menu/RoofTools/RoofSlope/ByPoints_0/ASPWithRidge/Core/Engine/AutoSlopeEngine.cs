// =======================================================
// File: AutoSlopeEngine.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.WithRidge
// Changes vs original:
//
//   PERFORMANCE: Dijkstra now runs once per drain (outward),
//   not once per vertex. BuildDrainDistanceTable() is called
//   once after graph construction. Per-vertex elevation uses
//   O(1) table lookups.
//
//   RIDGE DETECTION: RidgeDetector.BuildContext() runs once
//   after the distance table is built — clusters drains into
//   groups, finds the farthest pair, derives the XY ridge line.
//   Per-vertex: RidgeDetector.EvaluateVertex() checks XY
//   perpendicular distance to that line. Ridge members use
//   the path to the nearest drain in the farther group.
//
//   POST-COMMIT RE-READ: After tx.Commit(), SlabShapeVertices
//   are re-read to populate ElevationFromModel_mm on each
//   VertexData and to recompute maxElevFt from actual model
//   values (not the in-loop accumulator).
//
//   LOGGING: Ridge points are logged to the UI panel during
//   the run (per spec Q6).
// =======================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlopeByPoint.WithRidge.Core.Models;
using Revit26_Plugin.AutoSlopeByPoint.WithRidge.Core.Parameters;
using Revit26_Plugin.AutoSlopeByPoint.WithRidge.Infrastructure.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByPoint.WithRidge.Core.Engine
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
                FireFailure(data, "Roof slab shape editor is not available. Aborting.");
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
                FireFailure(data, "Top face not found. Aborting.");
                return;
            }

            // ── Build final drain points ─────────────────────────────────────
            List<XYZ> finalDrainPoints = data.DrainPoints ?? new List<XYZ>();

            data.Log(LogColorHelper.Cyan($"DEBUG: Initial drain points count = {finalDrainPoints.Count}"));

            if (data.EnableDrainTolerance && data.DrainToleranceMm > 0)
            {
                data.Log(LogColorHelper.Cyan(
                    $"Checking for nearby roof shape points within {data.DrainToleranceMm}mm..."));

                finalDrainPoints = DrainDetectionHelper.DetectDrainsWithinRadius(
                    roof, finalDrainPoints, data.DrainToleranceMm, data.Log);

                finalDrainPoints = DrainDetectionHelper.RemoveDuplicates(
                    finalDrainPoints, data.DrainToleranceMm);

                data.Log(LogColorHelper.Cyan($"DEBUG: After tolerance expansion count = {finalDrainPoints.Count}"));
            }

            if (finalDrainPoints == null || finalDrainPoints.Count == 0)
            {
                FireFailure(data, "No drain points are available. Aborting.");
                return;
            }

            // ── Match drain points to vertex indices ─────────────────────────
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

            data.Log(LogColorHelper.Cyan($"DEBUG: drainIndices count = {drainIndices.Count}"));

            if (drainIndices.Count == 0)
            {
                FireFailure(data, "No roof vertices matched the selected drain points. Aborting.");
                return;
            }

            // ── Build drain position lookup (drainIndex → XYZ) ───────────────
            var drainPositions = new Dictionary<int, XYZ>();
            foreach (int di in drainIndices)
                drainPositions[di] = vertices[di].Position;

            // ── Build graph + distance table (one Dijkstra per drain) ─────────
            data.Log(LogColorHelper.Cyan("Building Dijkstra graph..."));
            var dijkstra = new DijkstraPathEngine(vertices, topFace, thresholdFt);

            data.Log(LogColorHelper.Cyan($"Building distance table for {drainIndices.Count} drains..."));
            dijkstra.BuildDrainDistanceTable(drainIndices);
            data.Log(LogColorHelper.Cyan("Distance table ready."));

            // ── Build ridge context (drain grouping + ridge line) ─────────────
            RidgeDetector.RidgeContext ridgeCtx = null;
            if (data.RidgeDetectionEnabled && drainIndices.Count >= 2)
            {
                double groupRadiusFt = UnitUtils.ConvertToInternalUnits(
                    data.DrainGroupRadiusMm, UnitTypeId.Millimeters);

                // All roof vertex positions — used to project roof boundary
                // onto the ridge local frame and determine rectangle width.
                var allVertexPositions = vertices.Select(v => v.Position).ToList();

                ridgeCtx = RidgeDetector.BuildContext(
                    drainIndices, drainPositions, allVertexPositions, groupRadiusFt);

                if (ridgeCtx != null)
                    data.Log(LogColorHelper.Cyan(
                        $"Ridge line: drain {ridgeCtx.DrainNearA_Idx} → drain {ridgeCtx.DrainNearB_Idx} " +
                        $"(groups {ridgeCtx.GroupA_Indices.Count} + {ridgeCtx.GroupB_Indices.Count} drains)"));
                else
                    data.Log(LogColorHelper.Yellow(
                        "Ridge detection enabled but only one drain group found — skipping ridge logic."));
            }

            // Drain Z baseline for post-commit elevation offset calculation
            double drainBaselineZFt = drainIndices.Average(idx => vertices[idx].Position.Z);

            // ── Main loop ────────────────────────────────────────────────────
            int processed = 0;
            int skipped = 0;
            int ridgeCount = 0;
            double maxPathFt = 0;
            var vertexDataList = new List<VertexData>();
            Stopwatch sw = Stopwatch.StartNew();

            using (Transaction tx = new Transaction(doc, "Apply AutoSlope With Ridge"))
            {
                tx.Start();

                for (int i = 0; i < vertices.Count; i++)
                {
                    // Skip drain vertices themselves (elevation = 0)
                    if (drainIndices.Contains(i))
                    {
                        vertexDataList.Add(new VertexData
                        {
                            VertexIndex = i,
                            Position = vertices[i].Position,
                            PathLengthMeters = 0,
                            ElevationOffsetMm = 0,
                            ElevationFromModel_mm = 0,
                            NearestDrainIndex = i,
                            DirectionVector = XYZ.Zero,
                            WasProcessed = true,
                            IsRidgePoint = false,
                            RidgeDrainA = -1,
                            RidgeDrainB = -1
                        });
                        processed++;
                        continue;
                    }

                    // Get paths to all drains from table (O(1))
                    var pathsByDrain = dijkstra.GetPathToAllDrains(i);

                    // Find shortest reachable path for threshold check
                    double shortestFt = pathsByDrain.Values
                        .Where(v => !double.IsInfinity(v))
                        .DefaultIfEmpty(double.PositiveInfinity)
                        .Min();

                    if (double.IsInfinity(shortestFt) || shortestFt > thresholdFt)
                    {
                        skipped++;

                        if (data.ExportConfig?.IncludeVertexDetails == true)
                        {
                            vertexDataList.Add(new VertexData
                            {
                                VertexIndex = i,
                                Position = vertices[i].Position,
                                PathLengthMeters = double.IsInfinity(shortestFt)
                                    ? 0
                                    : UnitUtils.ConvertFromInternalUnits(shortestFt, UnitTypeId.Meters),
                                ElevationOffsetMm = 0,
                                ElevationFromModel_mm = 0,
                                NearestDrainIndex = -1,
                                DirectionVector = XYZ.Zero,
                                WasProcessed = false,
                                IsRidgePoint = false,
                                RidgeDrainA = -1,
                                RidgeDrainB = -1
                            });
                        }
                        continue;
                    }

                    // ── Ridge detection ──────────────────────────────────────
                    double pathToUse_ft;
                    bool isRidge = false;
                    int ridgeDrainA = -1, ridgeDrainB = -1;
                    double ridgePathA_m = 0, ridgePathB_m = 0;
                    int assignedDrain;

                    if (data.RidgeDetectionEnabled && ridgeCtx != null)
                    {
                        var ridgeResult = RidgeDetector.EvaluateVertex(
                            vertices[i].Position,
                            pathsByDrain,
                            ridgeCtx,
                            thresholdFt);

                        if (ridgeResult.IsRidge)
                        {
                            isRidge = true;
                            ridgeCount++;
                            pathToUse_ft = ridgeResult.PathToUse_ft;
                            ridgeDrainA  = ridgeResult.DrainA;
                            ridgeDrainB  = ridgeResult.DrainB;
                            ridgePathA_m = UnitUtils.ConvertFromInternalUnits(ridgeResult.PathA_ft, UnitTypeId.Meters);
                            ridgePathB_m = UnitUtils.ConvertFromInternalUnits(ridgeResult.PathB_ft, UnitTypeId.Meters);
                            assignedDrain = ridgeDrainA;

                            data.Log(LogColorHelper.Yellow(
                                $"RIDGE vertex {i}: drains [{ridgeDrainA},{ridgeDrainB}] " +
                                $"pathNear={ridgePathA_m:F2}m pathFar={ridgePathB_m:F2}m " +
                                $"→ elev from {UnitUtils.ConvertFromInternalUnits(pathToUse_ft, UnitTypeId.Meters):F2}m"));
                        }
                        else
                        {
                            // Q7: geometric drain assignment
                            int geoDrain = ridgeResult.GeometricDrainIndex;
                            pathToUse_ft = (geoDrain >= 0 && pathsByDrain.TryGetValue(geoDrain, out double gp))
                                ? gp
                                : shortestFt;
                            assignedDrain = geoDrain >= 0 ? geoDrain : -1;
                        }
                    }
                    else
                    {
                        // Ridge detection disabled — use shortest path (original behaviour)
                        pathToUse_ft = shortestFt;
                        assignedDrain = pathsByDrain
                            .Where(kvp => !double.IsInfinity(kvp.Value))
                            .OrderBy(kvp => kvp.Value)
                            .Select(kvp => kvp.Key)
                            .FirstOrDefault();
                    }

                    double elevFt = pathToUse_ft * slopeFactor;
                    editor.ModifySubElement(vertices[i], elevFt);

                    processed++;
                    if (pathToUse_ft > maxPathFt) maxPathFt = pathToUse_ft;

                    XYZ directionVector = assignedDrain >= 0
                        ? CalculateDirectionVector(vertices[i].Position, vertices[assignedDrain].Position)
                        : XYZ.Zero;

                    vertexDataList.Add(new VertexData
                    {
                        VertexIndex = i,
                        Position = vertices[i].Position,
                        PathLengthMeters = UnitUtils.ConvertFromInternalUnits(pathToUse_ft, UnitTypeId.Meters),
                        ElevationOffsetMm = UnitUtils.ConvertFromInternalUnits(elevFt, UnitTypeId.Millimeters),
                        ElevationFromModel_mm = 0,  // populated after commit
                        NearestDrainIndex = assignedDrain,
                        DirectionVector = directionVector,
                        WasProcessed = true,
                        IsRidgePoint = isRidge,
                        RidgeDrainA = ridgeDrainA,
                        RidgeDrainB = ridgeDrainB,
                        RidgePathA_m = ridgePathA_m,
                        RidgePathB_m = ridgePathB_m
                    });
                }

                tx.Commit();
            }

            // ── Post-commit re-read of actual vertex elevations ───────────────
            double maxElevFt = 0;

            var refreshedVertices = new List<SlabShapeVertex>();
            foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                refreshedVertices.Add(v);

            data.Log(LogColorHelper.Cyan($"DEBUG: Refreshed vertex count = {refreshedVertices.Count}"));

            // Match refreshed vertices to original by XY proximity
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
                        $"WARN: Could not match refreshed vertex for index {vd.VertexIndex}."));
                }
            }

            sw.Stop();

            // ── Summary values ───────────────────────────────────────────────
            int highest_mm = (int)Math.Round(
                UnitUtils.ConvertFromInternalUnits(maxElevFt, UnitTypeId.Millimeters),
                MidpointRounding.AwayFromZero);
            double longest_m = Math.Round(
                UnitUtils.ConvertFromInternalUnits(maxPathFt, UnitTypeId.Meters),
                2, MidpointRounding.AwayFromZero);
            int durationSec = (int)Math.Round(sw.Elapsed.TotalSeconds);
            string runDate = DateTime.Now.ToString("dd-MM-yy HH:mm");

            // ── Write Revit parameters ───────────────────────────────────────
            AutoSlopeParameterWriter.WriteAll(
                doc, roof, data,
                highest_mm, maxPathFt,
                processed, skipped, durationSec,
                finalDrainPoints.Count,
                ridgeCount,
                runDate,
                "P.WithRidge.01");

            // ── Excel export ─────────────────────────────────────────────────
            if (data.ExportConfig?.ExportToExcel == true)
            {
                string compactPath = ExcelExportHelper.ExportCompactVertexData(
                    data, vertexDataList, roof, data.SlopePercent);

                if (!string.IsNullOrEmpty(compactPath))
                    data.Log(LogColorHelper.Green($"✅ Compact Excel: {compactPath}"));

                if (data.ExportConfig.IncludeVertexDetails)
                {
                    string detailedPath = ExcelExportHelper.ExportDetailedVertexData(
                        data, vertexDataList, roof, finalDrainPoints, data.SlopePercent);

                    if (!string.IsNullOrEmpty(detailedPath))
                        data.Log(LogColorHelper.Green($"✅ Detailed Excel: {detailedPath}"));
                }
            }

            // ── Log summary ──────────────────────────────────────────────────
            data.Log(LogColorHelper.Cyan("===== AutoSlope Summary ====="));
            data.Log(LogColorHelper.Green($"Slope Percentage         : {data.SlopePercent}%"));
            data.Log(LogColorHelper.Green($"Vertices Processed       : {processed}"));
            data.Log(LogColorHelper.Yellow($"Vertices Skipped         : {skipped}"));
            data.Log(LogColorHelper.Yellow($"Ridge Points Detected    : {ridgeCount}"));
            data.Log(LogColorHelper.Cyan($"Highest Elevation        : {highest_mm:0} mm  ← from model"));
            data.Log(LogColorHelper.Cyan($"Longest Path             : {longest_m:0.00} m"));
            data.Log(LogColorHelper.Cyan($"Drain Count              : {finalDrainPoints.Count}"));
            data.Log(LogColorHelper.Cyan($"Run Duration             : {durationSec} sec"));
            data.Log(LogColorHelper.Cyan($"Run Date                 : {runDate}"));
            if (data.RidgeDetectionEnabled)
            {
                data.Log(LogColorHelper.Cyan($"Drain Group Radius       : {data.DrainGroupRadiusMm} mm"));
                data.Log(LogColorHelper.Cyan($"Ridge Rectangle          : roof-boundary projected"));
            }
            data.Log(LogColorHelper.Green("===== AutoSlope Finished ====="));

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
                RidgePointsDetected = ridgeCount
            });
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static void FireFailure(AutoSlopePayload data, string reason)
        {
            data.Log?.Invoke(LogColorHelper.Red(reason));
            data.OnCompleted?.Invoke(new AutoSlopeResult
            {
                Success = false,
                ErrorMessage = reason
            });
        }

        private static XYZ CalculateDirectionVector(XYZ fromPoint, XYZ toPoint)
        {
            if (fromPoint.DistanceTo(toPoint) < 0.001) return XYZ.Zero;
            return (toPoint - fromPoint).Normalize();
        }
    }
}
