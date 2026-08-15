// File: AutoSlopeDrainEngine.cs
// Location: Core/Engine/
// Base: ported from AutoSlopeByDrain V004 — orchestration only.
//
// UPDATED (V005, second round), per Rafi's confirmed decisions on 2026-07-22:
//   - REMOVED drain re-detection + MatchSelectedDrains (position-signature
//     matching) entirely. The Engine no longer re-reads the roof's Sketch
//     profile at Run time — it reuses the exact DrainItem objects (with
//     their LoopCurves) the user selected in the grid, carried directly via
//     payload.SelectedDrains. Rafi confirmed trading away the "roof changed
//     since grid was populated" Warning as an acceptable cost of this.
//   - ADDED: shape-edit vertex matching (X,Y -> Z) now happens HERE, per
//     selected DrainItem, against a freshly re-read SlabShapeVertex list —
//     this part must still be fresh, since shape-edit vertices genuinely can
//     move between detection time and Run time. Uses
//     DrainDetectionService.FindShapeVerticesAtLoopPoints (now public).
//     Only runs for items the user selected — unselected openings never get
//     DrainVertices populated at all, matching Rafi's confirmed decision
//     that unchecked openings are treated as ordinary roof (sloped toward
//     the nearest selected drain like any other vertex, not excluded from
//     the pathfinding graph).
//
// NEW (V005, first round), per Rafi's confirmed decisions on 2026-07-21:
//   - InsertCurveIntersectionPoints and VerifyElevationsAfterCommit toggles
//     passed through to RoofSlopeProcessorService.
//   - Excel export (ExcelExportHelper/ExcelExportService) replaces CSV
//     export entirely — single workbook, single export call (no duplicate
//     detailed/summary pair; see ExportConfig.cs for why).
//   - TotalDetectedCount / SelectedCount / ArcsCalculated / Version /
//     UnreachableVertexCount populated on the result.
//   - Parameter writer now receives an explicit version string instead of
//     a hardcoded one (see AutoSlopeDrainParameterWriter fix notes).

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlopeByDrain.V006.Core.Models;
using Revit26_Plugin.AutoSlopeByDrain.V006.Core.Parameters;
using Revit26_Plugin.AutoSlopeByDrain.V006.Core.Services;
using Revit26_Plugin.AutoSlopeByDrain.V006.Infrastructure.Helpers;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByDrain.V006.Core.Engine
{
    public static class AutoSlopeDrainEngine
    {
        private const string ToolVersion = "006";

        public static AutoSlopeDrainResult Execute(UIApplication app, AutoSlopeDrainPayload payload)
        {
            var log = payload.Log ?? (_ => { });
            var doc = app.ActiveUIDocument.Document;
            var startTime = DateTime.Now;

            try
            {
                var roof = doc.GetElement(payload.RoofId) as RoofBase;
                if (roof == null)
                {
                    return Fail("Selected roof could not be found (it may have been deleted).", log);
                }

                if (!(roof is FootPrintRoof))
                {
                    // Should not be reachable — RoofFilter restricts PickObject to
                    // FootPrintRoof only. Guarded here anyway so a mismatch fails
                    // clearly instead of silently reaching DrainDetectionService and
                    // producing a confusing "0 drains detected" result.
                    return Fail("Selected roof is not a FootPrintRoof. Sketch-based opening detection requires a sketch-based roof.", log);
                }

                // ── Re-analyze geometry with FRESH handles ─────────────────────
                log(new LogEntry(LogLevel.Info, "Re-reading roof geometry with fresh handles..."));
                var roofData = new RoofData { Roof = roof };
                var topFace = GetTopFace(roof);
                if (topFace == null)
                {
                    return Fail("Could not find top face of the roof.", log);
                }
                roofData.TopFace = topFace;

                var editor = roof.GetSlabShapeEditor();
                foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                    roofData.Vertices.Add(v);

                log(new LogEntry(LogLevel.Info, $"Found {roofData.Vertices.Count} shape editing vertices."));

                // ── Reuse selected DrainItems directly (no re-detection) ───────
                // Per Rafi's confirmed decision: the loop geometry captured during
                // the ORIGINAL detection pass (in the Command) is reused as-is here,
                // not re-extracted from the Sketch. detectedDrains is no longer
                // fetched fresh; TotalDetectedCount comes from what the payload
                // recorded at grid-population time.
                var selectedDrains = payload.SelectedDrains ?? new List<DrainItem>();

                if (selectedDrains.Count == 0)
                {
                    // Defensive backstop only — the UI disables Run when 0 drains are
                    // checked, so this path should be unreachable in normal use.
                    return Fail("No drains selected for slope application.", log);
                }

                // ── Match shape-edit vertices for SELECTED drains only ──────────
                // Deferred from detection time (per Rafi's confirmed decision) to
                // here, and only for the drains the user actually selected —
                // unselected openings never get DrainVertices populated at all, so
                // their shape-edit vertices are treated as ordinary roof points by
                // RoofSlopeProcessorService (sloped toward the nearest selected
                // drain, same as any other vertex — confirmed behavior). This part
                // MUST use a freshly re-read SlabShapeVertex list: shape-edit
                // vertices can genuinely move between detection time and Run time,
                // unlike the loop geometry itself.
                var detectionService = new DrainDetectionService();
                foreach (var drain in selectedDrains)
                {
                    var loopPoints = new List<XYZ>();
                    if (drain.LoopCurves != null)
                        foreach (var curve in drain.LoopCurves)
                            loopPoints.Add(curve.GetEndPoint(0));

                    drain.DrainVertices = detectionService.FindShapeVerticesAtLoopPoints(
                        loopPoints, roofData.Vertices, log);
                }

                int totalMatchedVertices = selectedDrains.Sum(d => d.DrainVertices?.Count ?? 0);
                log(new LogEntry(LogLevel.Info,
                    $"Matched {totalMatchedVertices} shape-edit vertex/vertices across {selectedDrains.Count} selected drain(s)."));

                log(new LogEntry(LogLevel.Info, $"Applying {payload.SlopePercent}% slope to {selectedDrains.Count} drain(s)..."));

                // ── Core slope path/elevation algorithm ────────────────────────
                var slopeService = new RoofSlopeProcessorService();
                var results = slopeService.ProcessRoofSlopes(
                    roofData,
                    selectedDrains,
                    payload.SlopePercent,
                    msg => log(new LogEntry(LogLevel.Info, msg)),
                    payload.ConnectionThresholdMeters,
                    payload.PathSampleCount,
                    payload.InsertCurveIntersectionPoints,
                    payload.VerifyElevationsAfterCommit,
                    payload.ThresholdMeters);

                int durationSec = (int)(DateTime.Now - startTime).TotalSeconds;
                var vertexData = slopeService.GetLastExportData() ?? new List<DrainVertexData>();

                int processedCount = vertexData.Count(v => v.WasProcessed);
                int skippedCount = vertexData.Count(v => !v.WasProcessed);

                var metrics = new DrainExportMetrics
                {
                    ProcessedVertices = processedCount,
                    SkippedVertices = skippedCount,
                    DrainCount = selectedDrains.Count,
                    HighestElevationMm = results.maxOffset,
                    LongestPathM = results.longestPath,
                    SlopePercent = payload.SlopePercent,
                    RunDurationSec = durationSec,
                    RunDate = DateTime.Now.ToString("dd-MM-yy HH:mm"),
                    RoofId = roof.Id.Value.ToString(),
                    RoofName = roof.Name,
                    TotalDetectedCount = payload.TotalDetectedCount,
                    SelectedCount = selectedDrains.Count,
                    ArcsCalculated = slopeService.LastArcsCalculated,
                    Version = ToolVersion,
                    UnreachableVertexCount = slopeService.LastUnreachableCount,
                    OverThresholdVertexCount = slopeService.LastOverThresholdCount
                };

                // ── Write tracking parameters ───────────────────────────────────
                var paramWriter = new AutoSlopeDrainParameterWriter();
                paramWriter.WriteAll(doc, roof, metrics, payload.SlopePercent, payload.ConnectionThresholdMeters,
                    ToolVersion, msg => log(new LogEntry(LogLevel.Info, msg)));

                // ── Excel export (single workbook: Vertex Data + Run Summary) ──
                string exportedPath = null;

                if (payload.ExportConfig != null && payload.ExportConfig.ExportToExcel)
                {
                    System.IO.Directory.CreateDirectory(payload.ExportConfig.ExportPath);

                    exportedPath = ExcelExportService.ExportWorkbook(
                        payload, vertexData, roof, payload.SlopePercent, metrics, log);

                    if (!string.IsNullOrEmpty(exportedPath))
                    {
                        log(new LogEntry(LogLevel.Success, $"✅ Excel exported to: {exportedPath}"));
                    }
                }

                log(new LogEntry(LogLevel.Success,
                    $"SUCCESS: {results.modifiedCount} vertices modified | Max offset {results.maxOffset:F1} mm | Longest path {results.longestPath:F2} m"));

                return new AutoSlopeDrainResult
                {
                    Success = true,
                    VerticesModified = results.modifiedCount,
                    VerticesProcessed = processedCount,
                    VerticesSkipped = skippedCount,
                    DrainCount = selectedDrains.Count,
                    HighestElevation_mm = results.maxOffset,
                    LongestPath_m = results.longestPath,
                    RunDuration_sec = durationSec,
                    RunDate = metrics.RunDate,
                    Status = AppConstants.Status_OK,
                    Version = ToolVersion,
                    TotalDetectedCount = payload.TotalDetectedCount,
                    SelectedCount = selectedDrains.Count,
                    ArcsCalculated = slopeService.LastArcsCalculated,
                    UnreachableVertexCount = slopeService.LastUnreachableCount,
                    OverThresholdVertexCount = slopeService.LastOverThresholdCount,
                    ExportedFilePath = exportedPath
                };
            }
            catch (Exception ex)
            {
                return Fail($"Unhandled exception: {ex.Message}", log);
            }
        }

        private static AutoSlopeDrainResult Fail(string message, Action<LogEntry> log)
        {
            log(new LogEntry(LogLevel.Error, message));
            return new AutoSlopeDrainResult
            {
                Success = false,
                ErrorMessage = message,
                Status = AppConstants.Status_Failed,
                Version = ToolVersion
            };
        }

        private static Face GetTopFace(RoofBase roof)
        {
            GeometryElement geomElem = roof.get_Geometry(new Options());
            Face topFace = null;
            double maxZ = double.MinValue;

            foreach (GeometryObject geomObj in geomElem)
            {
                if (geomObj is Solid solid)
                {
                    foreach (Face face in solid.Faces)
                    {
                        if (face == null) continue;
                        BoundingBoxUV bb = face.GetBoundingBox();
                        if (bb == null) continue;

                        UV midpointUV = new UV((bb.Min.U + bb.Max.U) / 2, (bb.Min.V + bb.Max.V) / 2);
                        XYZ midpoint = face.Evaluate(midpointUV);

                        if (midpoint != null && midpoint.Z > maxZ)
                        {
                            maxZ = midpoint.Z;
                            topFace = face;
                        }
                    }
                }
            }
            return topFace;
        }
    }
}
