// =======================================================
// File: AutoSlopeEngine.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.V025
// Changes vs V06:
//   - LogColorHelper removed entirely.
//   - All data.Log() calls now emit new LogEntry(LogLevel.X, "...")
//     so colour is driven by LogLevelToColorConverter in the UI.
// =======================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlopeByPoint.V025.Core.Models;
using Revit26_Plugin.AutoSlopeByPoint.V025.Core.Parameters;
using Revit26_Plugin.AutoSlopeByPoint.V025.Infrastructure.Helpers;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByPoint.V025.Core.Engine
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
                data.Log?.Invoke(new LogEntry(LogLevel.Error, "Top face not found. Aborting."));
                FireFailure(data, "Top face not found.");
                return;
            }

            // ── Opt-in: insert real vertices at line/arc intersection points ──
            // (entry/exit points where a straight roof-shape edge partially
            // overlaps a boundary or opening arc). Only runs if the user has
            // enabled "Insert intersection points on curves" in the UI.
            // curveTolFt is declared here (not inside the block below) so the
            // SAME tolerance is later passed into DijkstraPathEngine — previously
            // that call used the constructor's default (~1mm) instead of this
            // ~2mm value, so a drain vertex matched via a looser drain-tolerance
            // could fail the arc "on-curve" check and miss the same-arc bypass.
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

                    // Only the roof's real physical edges are tested against the arcs —
                    // not arbitrary vertex pairs — so points are inserted only where
                    // actually required.
                    List<Line> boundaryLines = AutoSlopeGeometry.GetBoundaryLines(topFace);

                    int insertedCount;
                    using (Transaction tx = new Transaction(doc, "Insert Curve Intersection Points"))
                    {
                        tx.Start();
                        insertedCount = CurveIntersectionHelper.InsertIntersectionPoints(
                            editor, boundaryLines, positions, boundaryArcs, curveTolFt,
                            msg => data.Log?.Invoke(new LogEntry(LogLevel.Info, msg)));
                        tx.Commit();
                    }

                    if (insertedCount > 0)
                    {
                        // NOTE: no explicit doc.Regenerate() here — tx.Commit() above already
                        // regenerated the document. Calling Regenerate() again here runs with
                        // no transaction open and throws "Modification of the document is
                        // forbidden ... no open transaction."

                        // The commit above already regenerated the document, which invalidates
                        // the SlabShapeEditor handle acquired before it — re-fetch a fresh one
                        // before touching it again, per this suite's own rule on this.
                        editor = roof.GetSlabShapeEditor();
                        if (editor == null || !editor.IsValidObject)
                        {
                            data.Log?.Invoke(new LogEntry(LogLevel.Error,
                                "Slab shape editor became invalid after inserting curve points. Aborting."));
                            FireFailure(data, "Slab shape editor became invalid after inserting curve points.");
                            return;
                        }

                        // Re-collect vertices (now includes the newly inserted points)
                        // and re-acquire topFace + arcs, since geometry changed underneath.
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

            data.Log?.Invoke(new LogEntry(LogLevel.Info,
                $"DEBUG: drainIndices count (vertices matching drains) = {drainIndices.Count}"));

            if (drainIndices.Count == 0)
            {
                data.Log?.Invoke(new LogEntry(LogLevel.Error,
                    "No roof vertices matched the selected drain points. Aborting."));
                FireFailure(data, "No roof vertices matched the selected drain points.");
                return;
            }

            // ── OPTIMIZATION: Single multi-source Dijkstra ───────────────────
            double[] distances = dijkstra.ComputeAllDistances(drainIndices);

            // ── Diagnostic: on-arc vertex path breakdown ─────────────────────
            // Read-only logging pass, no document modification — safe outside
            // any transaction. Reports which vertices sit on an arc, their
            // resolved path length to the nearest drain, and whether that path
            // used same-arc traversal, a tangent-route detour, straight chords,
            // or a mix of these across its hops.
            int onArcCount = dijkstra.OnArcVertexCount;
            data.Log?.Invoke(new LogEntry(LogLevel.Info,
                $"ARC-DEBUG: {onArcCount} vertex(es) classified as on-arc (tol = curveTolFt)."));

            for (int i = 0; i < vertices.Count; i++)
            {
                if (!dijkstra.IsOnArc(i)) continue;

                double pathFtArc = distances[i];
                string pathDesc = dijkstra.DescribePathEdgeTypes(i);
                string pathStr = double.IsInfinity(pathFtArc)
                    ? "no path found"
                    : $"{UnitUtils.ConvertFromInternalUnits(pathFtArc, UnitTypeId.Meters):F3} m";

                data.Log?.Invoke(new LogEntry(LogLevel.Info,
                    $"ARC-DEBUG: vertex {i} @ ({vertices[i].Position.X:F3}, {vertices[i].Position.Y:F3}) — path = {pathStr} — route: {pathDesc}"));
            }

            // ── Main slope loop ──────────────────────────────────────────────
            int processed = 0, skipped = 0;
            double maxPathFt = 0;
            var vertexDataList = new List<VertexData>();
            Stopwatch sw = Stopwatch.StartNew();

            double drainBaselineZFt = drainIndices.Count > 0
                ? drainIndices.Average(idx => vertices[idx].Position.Z)
                : 0;

            CircleMarkerService.PlacementCounts markerCounts = null;

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

                // ── Circle Markers (V025) ────────────────────────────────────
                // Runs inside this same transaction (one commit, one undo step
                // for the whole Run) — see CircleMarkerService header comment
                // for why Highest Point uses ElevationOffsetMm here.
                View activeView = app.ActiveUIDocument?.ActiveView;
                markerCounts = CircleMarkerService.PlaceMarkers(
                    doc,
                    activeView,
                    finalDrainPoints,
                    vertexDataList,
                    data.DrainMarkerGroup,
                    data.HighestPointMarkerGroup,
                    data.AllowedOffsetMarkerGroup,
                    data.AllowedOffsetThresholdMm,
                    data.Log);

                tx.Commit();
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

            int    highest_mm  = (int)Math.Round(
                UnitUtils.ConvertFromInternalUnits(maxElevFt, UnitTypeId.Millimeters),
                MidpointRounding.AwayFromZero);
            double longest_m   = Math.Round(
                UnitUtils.ConvertFromInternalUnits(maxPathFt, UnitTypeId.Meters),
                2, MidpointRounding.AwayFromZero);
            int    durationSec = (int)Math.Round(sw.Elapsed.TotalSeconds);
            string runDate     = DateTime.Now.ToString("dd-MM-yy HH:mm");

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
            data.Log?.Invoke(new LogEntry(LogLevel.Info,    "===== AutoSlope Summary ====="));
            data.Log?.Invoke(new LogEntry(LogLevel.Success, $"Applied Slope Percentage : {data.SlopePercent}%"));
            data.Log?.Invoke(new LogEntry(LogLevel.Success, $"Vertices Processed       : {processed}"));
            data.Log?.Invoke(new LogEntry(LogLevel.Warning, $"Vertices Skipped         : {skipped}"));
            data.Log?.Invoke(new LogEntry(LogLevel.Info,    $"Highest Elevation        : {highest_mm:0} mm  ← from model re-read"));
            data.Log?.Invoke(new LogEntry(LogLevel.Info,    $"Longest Path             : {longest_m:0.00} m"));
            data.Log?.Invoke(new LogEntry(LogLevel.Info,    $"Picked Drain Count       : {data.PickedDrainPoints?.Count ?? 0}"));
            data.Log?.Invoke(new LogEntry(LogLevel.Info,    $"Final Drain Count        : {finalDrainPoints.Count}"));
            data.Log?.Invoke(new LogEntry(LogLevel.Info,    $"Run Duration             : {durationSec} sec"));
            data.Log?.Invoke(new LogEntry(LogLevel.Info,    $"Run Date                 : {runDate}"));
            if (data.EnableDrainTolerance)
                data.Log?.Invoke(new LogEntry(LogLevel.Info,
                    $"Drain Tolerance          : {data.DrainToleranceMm} mm (enabled)"));
            data.Log?.Invoke(new LogEntry(LogLevel.Info,
                $"Circles Placed            : {markerCounts?.DrainCirclesPlaced ?? 0} drain, " +
                $"{markerCounts?.HighestCirclesPlaced ?? 0} highest, " +
                $"{markerCounts?.OffsetCirclesPlaced ?? 0} allowed-offset"));
            data.Log?.Invoke(new LogEntry(LogLevel.Success, "===== AutoSlope Finished Successfully ====="));

            data.OnCompleted?.Invoke(new AutoSlopeResult
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
            });
        }

        private static void FireFailure(AutoSlopePayload data, string reason)
        {
            data.Log?.Invoke(new LogEntry(LogLevel.Error, reason));
            data.Log?.Invoke(new LogEntry(LogLevel.Error, "DEBUG: Firing failure callback"));
            data.OnCompleted?.Invoke(new AutoSlopeResult
            {
                Success      = false,
                ErrorMessage = reason,
                PickedDrainCount = 0,
                FinalDrainCount  = 0
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
