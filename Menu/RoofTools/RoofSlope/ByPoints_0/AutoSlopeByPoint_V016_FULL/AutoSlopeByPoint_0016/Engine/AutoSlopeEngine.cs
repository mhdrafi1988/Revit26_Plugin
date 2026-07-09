// =======================================================
// File: AutoSlopeEngine.cs
// Full corrected version – all fixes applied
// =======================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlopeByPoint.V016.Core.Models;
using Revit26_Plugin.AutoSlopeByPoint.V016.Core.Parameters;
using Revit26_Plugin.AutoSlopeByPoint.V016.Infrastructure.Helpers;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByPoint.V016.Core.Engine
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
                data.Log?.Invoke(new LogEntry(LogLevel.Error,
                    "Roof slab shape editor is not available. Aborting."));
                FireFailure(data, "Roof slab shape editor is not available.");
                return;
            }

            // ── Declare variables that will be assigned later ──────────────
            Face topFace = null;
            List<Arc> boundaryArcs = null;
            List<SlabShapeVertex> vertices = null;

            // ── Reset vertices ───────────────────────────────────────────────
            using (Transaction tx = new Transaction(doc, "Reset Roof Vertices"))
            {
                tx.Start();
                foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                    editor.ModifySubElement(v, 0);
                tx.Commit();
            }

            // ── Re‑acquire vertices after reset ─────────────────────────────
            vertices = new List<SlabShapeVertex>();
            foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                vertices.Add(v);

            // ── INSERT DRAINS AS REAL VERTICES (Fix #1) ─────────────────────
            if (data.PickedDrainPoints != null && data.PickedDrainPoints.Count > 0)
            {
                double addToleranceFt = UnitUtils.ConvertToInternalUnits(2.0, UnitTypeId.Millimeters);
                bool needInsert = false;
                foreach (XYZ drain in data.PickedDrainPoints)
                {
                    bool found = false;
                    foreach (var v in vertices)
                    {
                        if (v.Position.DistanceTo(drain) <= addToleranceFt) { found = true; break; }
                    }
                    if (!found) { needInsert = true; break; }
                }

                if (needInsert)
                {
                    using (Transaction tx = new Transaction(doc, "Insert Drain Vertices"))
                    {
                        tx.Start();
                        foreach (XYZ drain in data.PickedDrainPoints)
                        {
                            bool found = false;
                            foreach (var v in vertices)
                            {
                                if (v.Position.DistanceTo(drain) <= addToleranceFt) { found = true; break; }
                            }
                            if (!found) editor.AddPoint(drain);
                        }
                        tx.Commit();
                    }

                    // Re‑acquire editor and vertices
                    editor = roof.GetSlabShapeEditor();
                    vertices = new List<SlabShapeVertex>();
                    foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                        vertices.Add(v);
                }
            }

            // ── Re‑compute top face and arcs after possible drain insertion ──
            topFace = AutoSlopeGeometry.GetTopFace(roof);
            if (topFace == null)
            {
                data.Log?.Invoke(new LogEntry(LogLevel.Error, "Top face not found. Aborting."));
                FireFailure(data, "Top face not found.");
                return;
            }
            boundaryArcs = AutoSlopeGeometry.GetBoundaryArcs(topFace);

            double slopeFactor = data.SlopePercent / 100.0;
            double thresholdFt = UnitUtils.ConvertToInternalUnits(data.ThresholdMeters, UnitTypeId.Meters);
            double curveTolFt = UnitUtils.ConvertToInternalUnits(2.0, UnitTypeId.Millimeters);

            // ── Opt-in: insert real vertices at line/arc intersection points ──
            if (data.InsertCurveIntersectionPoints)
            {
                if (boundaryArcs.Count == 0)
                {
                    data.Log?.Invoke(new LogEntry(LogLevel.Info,
                        "Curve intersection check enabled, but no arc edges found on this roof."));
                }
                else
                {
                    var positions = new List<XYZ>(vertices.Count);
                    foreach (var v in vertices) positions.Add(v.Position);

                    int insertedCount;
                    using (Transaction tx = new Transaction(doc, "Insert Curve Intersection Points"))
                    {
                        tx.Start();
                        insertedCount = CurveIntersectionHelper.InsertIntersectionPoints(
                            editor, positions, boundaryArcs, thresholdFt, curveTolFt,
                            msg => data.Log?.Invoke(new LogEntry(LogLevel.Info, msg)));
                        tx.Commit();
                    }

                    if (insertedCount > 0)
                    {
                        editor = roof.GetSlabShapeEditor();
                        if (editor == null || !editor.IsValidObject)
                        {
                            data.Log?.Invoke(new LogEntry(LogLevel.Error,
                                "Roof slab shape editor became invalid after inserting curve intersection points. Aborting."));
                            FireFailure(data, "Roof slab shape editor became invalid after inserting curve intersection points.");
                            return;
                        }

                        vertices = new List<SlabShapeVertex>();
                        foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                            vertices.Add(v);

                        topFace = AutoSlopeGeometry.GetTopFace(roof);
                        if (topFace == null)
                        {
                            data.Log?.Invoke(new LogEntry(LogLevel.Error,
                                "Top face not found after inserting curve points. Aborting."));
                            FireFailure(data, "Top face not found after inserting curve points.");
                            return;
                        }
                        boundaryArcs = AutoSlopeGeometry.GetBoundaryArcs(topFace);
                    }
                }
            }

            // ── Build final drain points ─────────────────────────────────────
            List<XYZ> finalDrainPoints = data.DrainPoints ?? new List<XYZ>();

            data.Log?.Invoke(new LogEntry(LogLevel.Info,
                $"DEBUG: Initial drain points count = {finalDrainPoints.Count}"));

            if (data.EnableDrainTolerance && data.DrainToleranceMm > 0)
            {
                data.Log?.Invoke(new LogEntry(LogLevel.Info,
                    $"🔍 Checking for nearby roof shape points within {data.DrainToleranceMm}mm of selected points..."));

                finalDrainPoints = DrainDetectionHelper.DetectDrainsWithinRadius(
                    roof, finalDrainPoints, data.DrainToleranceMm, data.Log);

                finalDrainPoints = DrainDetectionHelper.RemoveDuplicates(
                    finalDrainPoints, data.DrainToleranceMm);

                data.Log?.Invoke(new LogEntry(LogLevel.Info,
                    $"DEBUG: After tolerance expansion count = {finalDrainPoints.Count}"));
            }

            if (finalDrainPoints == null || finalDrainPoints.Count == 0)
            {
                data.Log?.Invoke(new LogEntry(LogLevel.Error,
                    "No drain points are available. Aborting."));
                FireFailure(data, "No drain points are available.");
                return;
            }

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

            data.Log?.Invoke(new LogEntry(LogLevel.Info,
                $"DEBUG: drainIndices count (vertices matching drains) = {drainIndices.Count}"));

            if (drainIndices.Count == 0)
            {
                data.Log?.Invoke(new LogEntry(LogLevel.Error,
                    "No roof vertices matched the selected drain points. Aborting."));
                FireFailure(data, "No roof vertices matched the selected drain points.");
                return;
            }

            // ── Staged pipeline ──────────────────────────────────────────────
            var staged = new StagedPathEngine(
                vertices, topFace, thresholdFt, boundaryArcs, curveTolFt,
                drainIndices, finalDrainPoints, data.MultiArcMode);
            staged.ComputeAll();

            data.Log?.Invoke(new LogEntry(LogLevel.Info,
                $"Multi-arc combination mode: {data.MultiArcMode}"));

            // ── Main slope loop ──────────────────────────────────────────────
            int processed = 0, skipped = 0;
            int directCount = 0, graphCount = 0, arcTangentCount = 0;
            double maxPathFt = 0;
            int maxPathVertexIndex = -1;
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
                    StagedPathResult result = staged.Resolve(i);
                    double pathFt = result.DistanceFt;

                    if (double.IsInfinity(pathFt) || pathFt > thresholdFt)
                    {
                        skipped++;

                        if (double.IsInfinity(pathFt))
                        {
                            data.Log?.Invoke(new LogEntry(LogLevel.Warning,
                                $"Vertex {i} skipped — unreachable: {staged.GetSkipReason(i)}"));
                        }
                        else
                        {
                            data.Log?.Invoke(new LogEntry(LogLevel.Warning,
                                $"Vertex {i} skipped — path {UnitUtils.ConvertFromInternalUnits(pathFt, UnitTypeId.Millimeters):F1} mm exceeds threshold"));
                        }

                        if (data.ExportConfig?.IncludeVertexDetails == true)
                        {
                            vertexDataList.Add(new VertexData
                            {
                                VertexIndex = i,
                                Position = vertices[i].Position,
                                PathLengthMeters = double.IsInfinity(pathFt) ? 0
                                    : UnitUtils.ConvertFromInternalUnits(pathFt, UnitTypeId.Meters),
                                ElevationOffsetMm = 0,
                                ElevationFromModel_mm = 0,
                                NearestDrainIndex = -1,
                                DirectionVector = XYZ.Zero,
                                WasProcessed = false,
                                PathMethod = result.Method.ToString(),
                                ArcTypeSummary = result.ArcTypeSummary
                            });
                        }
                        continue;
                    }

                    double elevFt = pathFt * slopeFactor;
                    editor.ModifySubElement(vertices[i], elevFt);

                    processed++;
                    switch (result.Method)
                    {
                        case PathMethod.Direct: directCount++; break;
                        case PathMethod.Graph: graphCount++; break;
                        case PathMethod.ArcTangent: arcTangentCount++; break;
                    }
                    if (pathFt > maxPathFt) { maxPathFt = pathFt; maxPathVertexIndex = i; }

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
                        WasProcessed = true,
                        PathMethod = result.Method.ToString(),
                        ArcTypeSummary = result.ArcTypeSummary
                    });
                }

                tx.Commit();
            }

            // ── DEBUG: log the actual path taken for the longest run ─────────
            if (maxPathVertexIndex >= 0)
            {
                StagedPathResult longest = staged.Resolve(maxPathVertexIndex);
                data.Log?.Invoke(new LogEntry(LogLevel.Info,
                    $"DEBUG: Longest path — vertex {maxPathVertexIndex}, total {UnitUtils.ConvertFromInternalUnits(maxPathFt, UnitTypeId.Millimeters):F1} mm, " +
                    $"method {longest.Method}{(string.IsNullOrEmpty(longest.ArcTypeSummary) ? "" : $" [{longest.ArcTypeSummary}]")}, {longest.Hops.Count} hop(s):"));
                foreach (var hop in longest.Hops)
                {
                    double hopMm = UnitUtils.ConvertFromInternalUnits(hop.LengthFt, UnitTypeId.Millimeters);
                    data.Log?.Invoke(new LogEntry(LogLevel.Info,
                        $"    {hop.Description}  {hopMm:F1} mm  [{(hop.IsArcHop ? "arc" : "line")}]"));
                }
            }

            // ── Re-read vertices from Revit after commit ─────────────────────
            double maxElevFt = 0;
            var refreshedVertices = new List<SlabShapeVertex>();
            foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                refreshedVertices.Add(v);

            data.Log?.Invoke(new LogEntry(LogLevel.Info,
                $"DEBUG: Refreshed vertex count after commit = {refreshedVertices.Count}"));

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

                    if (elevFromModelFt > maxElevFt) maxElevFt = elevFromModelFt;
                }
                else
                {
                    vd.ElevationFromModel_mm = vd.ElevationOffsetMm;
                    data.Log?.Invoke(new LogEntry(LogLevel.Warning,
                        $"WARN: Could not match refreshed vertex for index {vd.VertexIndex}, using calculated value."));
                }
            }

            data.Log?.Invoke(new LogEntry(LogLevel.Info,
                $"DEBUG: maxElevFt from model re-read = {maxElevFt:F6} ft"));

            sw.Stop();

            int highest_mm = (int)Math.Round(
                UnitUtils.ConvertFromInternalUnits(maxElevFt, UnitTypeId.Millimeters),
                MidpointRounding.AwayFromZero);
            double longest_m = Math.Round(
                UnitUtils.ConvertFromInternalUnits(maxPathFt, UnitTypeId.Meters),
                2, MidpointRounding.AwayFromZero);
            int durationSec = (int)Math.Round(sw.Elapsed.TotalSeconds);
            string runDate = DateTime.Now.ToString("dd-MM-yy HH:mm");

            const string toolVersion = "P.10.00";

            int statusCode = AutoSlopeParameterWriter.WriteAll(
                doc, roof, data,
                highest_mm, maxPathFt,
                processed, skipped, durationSec,
                finalDrainPoints.Count,
                runDate,
                toolVersion);

            string compactPath = null;
            if (data.ExportConfig?.ExportToExcel == true)
            {
                compactPath = ExcelExportService.ExportCompactVertexData(
                    data, vertexDataList, roof, data.SlopePercent,
                    toolVersion, statusCode);

                if (!string.IsNullOrEmpty(compactPath))
                {
                    data.Log?.Invoke(new LogEntry(LogLevel.Success,
                        $"✅ Compact Excel exported to: {compactPath}"));
                    data.Log?.Invoke(new LogEntry(LogLevel.Info,
                        "  • Sorted by PathLength_Meters (longest first)"));
                    data.Log?.Invoke(new LogEntry(LogLevel.Info,
                        $"  • Contains {processed} processed vertices"));

                    var longestVertex = vertexDataList
                        .Where(v => v.WasProcessed)
                        .OrderByDescending(v => v.PathLengthMeters)
                        .FirstOrDefault();

                    if (longestVertex != null)
                        data.Log?.Invoke(new LogEntry(LogLevel.Info,
                            $"  • Longest path: {longestVertex.PathLengthMeters:F2} m to drain {longestVertex.NearestDrainIndex}"));
                }

                if (data.ExportConfig.IncludeVertexDetails)
                {
                    string detailedPath = ExcelExportService.ExportDetailedVertexData(
                        data, vertexDataList, roof, finalDrainPoints, data.SlopePercent);

                    if (!string.IsNullOrEmpty(detailedPath))
                    {
                        data.Log?.Invoke(new LogEntry(LogLevel.Success,
                            $"✅ Detailed Excel exported to: {detailedPath}"));
                        data.Log?.Invoke(new LogEntry(LogLevel.Info,
                            $"  • {vertexDataList.Count} total vertices ({processed} processed, {skipped} skipped)"));
                        data.Log?.Invoke(new LogEntry(LogLevel.Info,
                            "  • Sheets: Summary, Drain Points, Vertices, Statistics"));
                    }
                }
            }

            // ── Summary ──────────────────────────────────────────────────────
            data.Log?.Invoke(new LogEntry(LogLevel.Info, "===== AutoSlope Summary ====="));
            data.Log?.Invoke(new LogEntry(LogLevel.Success, $"Applied Slope Percentage : {data.SlopePercent}%"));
            data.Log?.Invoke(new LogEntry(LogLevel.Success, $"Vertices Processed       : {processed}"));
            data.Log?.Invoke(new LogEntry(LogLevel.Warning, $"Vertices Skipped         : {skipped}"));
            data.Log?.Invoke(new LogEntry(LogLevel.Info, $"  • Direct                : {directCount}"));
            data.Log?.Invoke(new LogEntry(LogLevel.Info, $"  • Graph                 : {graphCount}"));
            data.Log?.Invoke(new LogEntry(LogLevel.Info, $"  • Arc-tangent           : {arcTangentCount}"));
            data.Log?.Invoke(new LogEntry(LogLevel.Info, $"Highest Elevation        : {highest_mm:0} mm  ← from model re-read"));
            data.Log?.Invoke(new LogEntry(LogLevel.Info, $"Longest Path             : {longest_m:0.00} m"));
            data.Log?.Invoke(new LogEntry(LogLevel.Info, $"Picked Drain Count       : {data.PickedDrainPoints?.Count ?? 0}"));
            data.Log?.Invoke(new LogEntry(LogLevel.Info, $"Final Drain Count        : {finalDrainPoints.Count}"));
            data.Log?.Invoke(new LogEntry(LogLevel.Info, $"Run Duration             : {durationSec} sec"));
            data.Log?.Invoke(new LogEntry(LogLevel.Info, $"Run Date                 : {runDate}"));
            if (data.EnableDrainTolerance)
                data.Log?.Invoke(new LogEntry(LogLevel.Info,
                    $"Drain Tolerance          : {data.DrainToleranceMm} mm (enabled)"));
            data.Log?.Invoke(new LogEntry(LogLevel.Success, "===== AutoSlope Finished Successfully ====="));

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
                Version = toolVersion,
                Status = statusCode,
                ExportedFilePath = compactPath,
                DirectCount = directCount,
                GraphCount = graphCount,
                ArcTangentCount = arcTangentCount
            });
        }

        private static void FireFailure(AutoSlopePayload data, string reason)
        {
            data.Log?.Invoke(new LogEntry(LogLevel.Error, reason));
            data.Log?.Invoke(new LogEntry(LogLevel.Error, "DEBUG: Firing failure callback"));
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
                double d = vertexPos.DistanceTo(drainPoints[i]);
                if (d < minDistance) { minDistance = d; nearestIndex = i; }
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