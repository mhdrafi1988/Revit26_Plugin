// =======================================================
// File: AutoSlopeEngine.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.MultiCopies28
// Changes vs V028:
//   - Execute(app, payload) replaced by ApplySlope(app, payload):
//     no internal Transaction.Start/Commit — the caller
//     (MultiSlopeVariantEngine) already has a Transaction/SubTransaction
//     open per roof copy, and Revit does not allow starting a Transaction
//     while one is already open on the document. Every point where the
//     original relied on tx.Commit() to trigger regeneration before
//     re-reading geometry now calls doc.Regenerate() explicitly instead,
//     while remaining inside the caller's open transaction — the
//     SlabShapeEditor handle is still re-fetched afterward exactly as
//     before, since Regenerate() invalidates it the same way a commit did.
//   - Returns AutoSlopeResult directly instead of invoking
//     payload.OnCompleted (removed from AutoSlopePayload) — the caller
//     needs the result synchronously to decide SubTransaction commit
//     vs rollback for this copy.
//   - AutoSlopeParameterWriter.WriteAll is called via its transaction-free
//     WriteAllCore overload for the same reason.
// =======================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlopeByPoint.MultiCopies28.Core.Models;
using Revit26_Plugin.AutoSlopeByPoint.MultiCopies28.Core.Parameters;
using Revit26_Plugin.AutoSlopeByPoint.MultiCopies28.Infrastructure.Helpers;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByPoint.MultiCopies28.Core.Engine
{
    public static class AutoSlopeEngine
    {
        /// <summary>
        /// Applies slope to the roof named in data.RoofId and returns the result.
        /// Assumes the caller already has a Transaction (or SubTransaction, opened
        /// inside an outer Transaction) active on the document — this method never
        /// opens or commits one itself.
        /// </summary>
        public static AutoSlopeResult ApplySlope(UIApplication app, AutoSlopePayload data)
        {
            Document doc = app.ActiveUIDocument.Document;

            // ── Guard: roof ─────────────────────────────────────────────────
            RoofBase roof = doc.GetElement(data.RoofId) as RoofBase;
            if (roof == null)
                return Failure(data, "Roof element not found. Aborting.");

            // ── Guard: slab shape editor ────────────────────────────────────
            SlabShapeEditor editor = roof.GetSlabShapeEditor();
            if (editor == null || !editor.IsValidObject)
                return Failure(data, "Roof slab shape editor is not available. Aborting.");

            // ── Reset vertices ───────────────────────────────────────────────
            foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                editor.ModifySubElement(v, 0);

            // Regenerate now (in place of the original "Reset Roof Vertices" tx
            // commit) so vertex positions below reflect the reset.
            doc.Regenerate();

            // ── Collect vertices ─────────────────────────────────────────────
            var vertices = new List<SlabShapeVertex>();
            foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                vertices.Add(v);

            double slopeFactor = data.SlopePercent / 100.0;
            double thresholdFt = UnitUtils.ConvertToInternalUnits(data.ThresholdMeters, UnitTypeId.Meters);

            // ── Guard: top face ──────────────────────────────────────────────
            Face topFace = AutoSlopeGeometry.GetTopFace(roof);
            if (topFace == null)
                return Failure(data, "Top face not found. Aborting.");

            // ── Opt-in: insert real vertices at line/arc intersection points ──
            double curveTolFt = UnitUtils.ConvertToInternalUnits(2.0, UnitTypeId.Millimeters); // ~1-2mm tolerance
            List<Arc> boundaryArcs = null;
            if (data.InsertCurveIntersectionPoints)
            {
                boundaryArcs = AutoSlopeGeometry.GetBoundaryArcs(topFace);

                if (boundaryArcs.Count == 0)
                {
                    data.Log?.Invoke(new LogEntry(LogLevel.Info,
                        "Curve intersection check enabled, but no arc edges found on this roof."));
                }
                else
                {
                    var positions = new List<XYZ>(vertices.Count);
                    foreach (var v in vertices) positions.Add(v.Position);

                    List<Line> boundaryLines = AutoSlopeGeometry.GetBoundaryLines(topFace);

                    int insertedCount = CurveIntersectionHelper.InsertIntersectionPoints(
                        editor, boundaryLines, positions, boundaryArcs, curveTolFt,
                        msg => data.Log?.Invoke(new LogEntry(LogLevel.Info, msg)));

                    if (insertedCount > 0)
                    {
                        // Regenerate in place of the original commit — the
                        // SlabShapeEditor handle is invalidated by Regenerate()
                        // the same way it was by a transaction commit, so it
                        // must be re-fetched before touching it again.
                        doc.Regenerate();

                        editor = roof.GetSlabShapeEditor();
                        if (editor == null || !editor.IsValidObject)
                            return Failure(data, "Slab shape editor became invalid after inserting curve points. Aborting.");

                        // Re-collect vertices (now includes the newly inserted points)
                        // and re-acquire topFace + arcs, since geometry changed underneath.
                        vertices = new List<SlabShapeVertex>();
                        foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                            vertices.Add(v);

                        topFace = AutoSlopeGeometry.GetTopFace(roof);
                        if (topFace == null)
                            return Failure(data, "Top face not found after inserting curve points. Aborting.");
                        boundaryArcs = AutoSlopeGeometry.GetBoundaryArcs(topFace);
                    }
                }
            }

            // ── Build final drain points ─────────────────────────────────────
            List<XYZ> finalDrainPoints = data.DrainPoints ?? new List<XYZ>();

            if (data.EnableDrainTolerance && data.DrainToleranceMm > 0)
            {
                data.Log?.Invoke(new LogEntry(LogLevel.Info,
                    $"🔍 Checking for nearby roof shape points within {data.DrainToleranceMm}mm of selected points..."));

                finalDrainPoints = DrainDetectionHelper.DetectDrainsWithinRadius(
                    roof, finalDrainPoints, data.DrainToleranceMm, data.Log);

                finalDrainPoints = DrainDetectionHelper.RemoveDuplicates(
                    finalDrainPoints, data.DrainToleranceMm);
            }

            if (finalDrainPoints == null || finalDrainPoints.Count == 0)
                return Failure(data, "No drain points are available. Aborting.");

            // ── Build Dijkstra graph ─────────────────────────────────────────
            var dijkstra = new DijkstraPathEngine(vertices, topFace, thresholdFt, boundaryArcs, curveTolFt);

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
                return Failure(data, "No roof vertices matched the selected drain points. Aborting.");

            // ── OPTIMIZATION: Single multi-source Dijkstra ───────────────────
            double[] distances = dijkstra.ComputeAllDistances(drainIndices);

            // ── Main slope loop ──────────────────────────────────────────────
            int processed = 0, skipped = 0;
            double maxPathFt = 0;
            var vertexDataList = new List<VertexData>();
            Stopwatch sw = Stopwatch.StartNew();

            double drainBaselineZFt = drainIndices.Count > 0
                ? drainIndices.Average(idx => vertices[idx].Position.Z)
                : 0;

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
                            VertexIndex       = i,
                            Position          = vertices[i].Position,
                            PathLengthMeters  = double.IsInfinity(pathFt) ? 0
                                : UnitUtils.ConvertFromInternalUnits(pathFt, UnitTypeId.Meters),
                            ElevationOffsetMm      = 0,
                            ElevationFromModel_mm  = 0,
                            NearestDrainIndex      = -1,
                            DirectionVector        = XYZ.Zero,
                            WasProcessed           = false
                        });
                    }
                    continue;
                }

                double elevFt = pathFt * slopeFactor;
                editor.ModifySubElement(vertices[i], elevFt);

                processed++;
                if (pathFt > maxPathFt) maxPathFt = pathFt;

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
                    ElevationFromModel_mm = 0,
                    NearestDrainIndex = nearestDrainIndex,
                    DirectionVector   = directionVector,
                    WasProcessed      = true
                });
            }

            // ── Circle Markers ────────────────────────────────────────────────
            View activeView = app.ActiveUIDocument?.ActiveView;
            CircleMarkerService.PlacementCounts markerCounts = CircleMarkerService.PlaceMarkers(
                doc,
                activeView,
                finalDrainPoints,
                vertexDataList,
                data.DrainMarkerGroup,
                data.HighestPointMarkerGroup,
                data.AllowedOffsetMarkerGroup,
                data.AllowedOffsetThresholdMm,
                data.EnableDrainTolerance ? data.DrainToleranceMm : 0,
                data.Log);

            // Regenerate in place of the original "Apply AutoSlope" tx commit,
            // so the re-read below reflects the vertex edits above.
            doc.Regenerate();

            // ── Re-read vertices from Revit after regenerate ──────────────────
            double maxElevFt = 0;
            var refreshedVertices = new List<SlabShapeVertex>();
            foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                refreshedVertices.Add(v);

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

            sw.Stop();

            int    highest_mm  = (int)Math.Round(
                UnitUtils.ConvertFromInternalUnits(maxElevFt, UnitTypeId.Millimeters),
                MidpointRounding.AwayFromZero);
            double longest_m   = Math.Round(
                UnitUtils.ConvertFromInternalUnits(maxPathFt, UnitTypeId.Meters),
                2, MidpointRounding.AwayFromZero);
            int    durationSec = (int)Math.Round(sw.Elapsed.TotalSeconds);
            string runDate     = DateTime.Now.ToString("dd-MM-yy HH:mm");

            const string toolVersion = "MCOP28.01.00";

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
                        $"  • Contains {processed} processed vertices"));
                }

                if (data.ExportConfig.IncludeVertexDetails)
                {
                    string detailedPath = ExcelExportService.ExportDetailedVertexData(
                        data, vertexDataList, roof, finalDrainPoints, data.SlopePercent);

                    if (!string.IsNullOrEmpty(detailedPath))
                    {
                        data.Log?.Invoke(new LogEntry(LogLevel.Success,
                            $"✅ Detailed Excel exported to: {detailedPath}"));
                    }
                }
            }

            data.Log?.Invoke(new LogEntry(LogLevel.Success,
                $"Slope {data.SlopePercent}% applied — {processed} vertices processed, {skipped} skipped, " +
                $"highest {highest_mm}mm, longest path {longest_m:0.00}m."));

            return new AutoSlopeResult
            {
                Success           = true,
                VerticesProcessed = processed,
                VerticesSkipped   = skipped,
                PickedDrainCount  = data.PickedDrainPoints?.Count ?? 0,
                FinalDrainCount   = finalDrainPoints.Count,
                HighestElevation_mm = highest_mm,
                LongestPath_m     = longest_m,
                RunDuration_sec   = durationSec,
                RunDate           = runDate,
                CurvesCalculated  = boundaryArcs?.Count ?? 0,
                Version           = toolVersion,
                Status            = statusCode,
                ExportedFilePath  = compactPath,
                DrainCirclesPlaced   = markerCounts?.DrainCirclesPlaced ?? 0,
                HighestCirclesPlaced = markerCounts?.HighestCirclesPlaced ?? 0,
                OffsetCirclesPlaced  = markerCounts?.OffsetCirclesPlaced ?? 0
            };
        }

        private static AutoSlopeResult Failure(AutoSlopePayload data, string reason)
        {
            data.Log?.Invoke(new LogEntry(LogLevel.Error, reason));
            return new AutoSlopeResult
            {
                Success      = false,
                ErrorMessage = reason,
                PickedDrainCount = 0,
                FinalDrainCount  = 0
            };
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
