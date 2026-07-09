// File: AutoSlopeDrainEngine.cs
// Location: Core/Engine/
// Mirrors AutoSlopeByPoint's AutoSlopeEngine — orchestration only.
// Core algorithms it calls (DrainDetectionService, GraphBuilderService,
// PathSolverService, RoofSlopeProcessorService) are UNCHANGED from the
// CSV version; only this orchestration layer is new.

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlopeByDrain.V003.Core.Models;
using Revit26_Plugin.AutoSlopeByDrain.V003.Core.Parameters;
using Revit26_Plugin.AutoSlopeByDrain.V003.Core.Services;
using Revit26_Plugin.AutoSlopeByDrain.V003.Infrastructure.Helpers;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByDrain.V003.Core.Engine
{
    public static class AutoSlopeDrainEngine
    {
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

                // ── Re-analyze geometry with FRESH handles ─────────────────────
                // (SlabShapeEditor handles from the initial detection pass are not
                // reused across the ExternalEvent boundary — see AutoSlopeDrainPayload.)
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

                // ── Re-detect drains (core detection logic — UNCHANGED) ────────
                var detectionService = new DrainDetectionService();
                var detectedDrains = detectionService.DetectDrainsFromRoof(roof, topFace, roofData.Vertices);
                roofData.DetectedDrains = detectedDrains;

                if (detectedDrains.Count != payload.ExpectedDrainCount)
                {
                    log(new LogEntry(LogLevel.Warning,
                        $"Roof geometry appears to have changed since the grid was populated " +
                        $"(was {payload.ExpectedDrainCount} drains, now {detectedDrains.Count}). " +
                        "Matching your selection by position — please verify results."));
                }

                var selectedDrains = MatchSelectedDrains(payload.SelectedDrainSignatures, detectedDrains, log);

                if (selectedDrains.Count == 0)
                {
                    return Fail("No drains selected for slope application.", log);
                }

                log(new LogEntry(LogLevel.Info, $"Applying {payload.SlopePercent}% slope to {selectedDrains.Count} drain(s)..."));

                // ── Core slope path/elevation algorithm — UNCHANGED ────────────
                var slopeService = new RoofSlopeProcessorService();
                var results = slopeService.ProcessRoofSlopes(
                    roofData,
                    selectedDrains,
                    payload.SlopePercent,
                    msg => log(new LogEntry(LogLevel.Info, msg)),
                    payload.ConnectionThresholdMeters,
                    payload.PathSampleCount);

                int durationSec = (int)(DateTime.Now - startTime).TotalSeconds;
                var vertexData = slopeService.GetLastExportData() ?? new List<DrainVertexData>();

                var metrics = new DrainExportMetrics
                {
                    ProcessedVertices = vertexData.Count(v => v.WasProcessed),
                    SkippedVertices = vertexData.Count(v => !v.WasProcessed),
                    DrainCount = selectedDrains.Count,
                    HighestElevationMm = results.maxOffset,
                    LongestPathM = results.longestPath,
                    SlopePercent = payload.SlopePercent,
                    RunDurationSec = durationSec,
                    RunDate = DateTime.Now.ToString("dd-MM-yy HH:mm"),
                    RoofId = roof.Id.Value.ToString(),
                    RoofName = roof.Name
                };

                // ── Write tracking parameters (unchanged logic, new namespace) ─
                var paramWriter = new AutoSlopeDrainParameterWriter();
                paramWriter.WriteAll(doc, roof, metrics, payload.SlopePercent, payload.ConnectionThresholdMeters,
                    msg => log(new LogEntry(LogLevel.Info, msg)));

                // ── CSV export (always includes vertex detail + summary) ──────
                string detailedPath = null;
                string summaryPath = null;

                if (payload.ExportConfig != null && payload.ExportConfig.ExportToCsv)
                {
                    System.IO.Directory.CreateDirectory(payload.ExportConfig.ExportPath);

                    detailedPath = CsvExportHelper.ExportDetailedVertexData(
                        payload.ExportConfig, vertexData, roof, selectedDrains, payload.SlopePercent,
                        msg => log(new LogEntry(LogLevel.Info, msg)));

                    summaryPath = CsvExportHelper.ExportSummaryOnly(
                        payload.ExportConfig, metrics, roof, payload.SlopePercent,
                        msg => log(new LogEntry(LogLevel.Info, msg)));
                }

                log(new LogEntry(LogLevel.Success,
                    $"SUCCESS: {results.modifiedCount} vertices modified | Max offset {results.maxOffset:F1} mm | Longest path {results.longestPath:F2} m"));

                return new AutoSlopeDrainResult
                {
                    Success = true,
                    VerticesModified = results.modifiedCount,
                    DrainCount = selectedDrains.Count,
                    HighestElevation_mm = results.maxOffset,
                    LongestPath_m = results.longestPath,
                    RunDuration_sec = durationSec,
                    RunDate = metrics.RunDate,
                    Status = AppConstants.Status_OK,
                    ExportedDetailedFilePath = detailedPath,
                    ExportedSummaryFilePath = summaryPath
                };
            }
            catch (Exception ex)
            {
                return Fail($"Unhandled exception: {ex.Message}", log);
            }
        }

        /// <summary>
        /// Matches user-selected drains (captured as signatures before the ExternalEvent boundary)
        /// against a freshly re-detected drain list, by center-point distance + size — NOT by
        /// index, so a roof edit made while the modeless window was open can't silently apply
        /// the slope calculation to the wrong opening.
        /// </summary>
        private static List<DrainItem> MatchSelectedDrains(
            List<DrainSelectionSignature> signatures, List<DrainItem> detectedDrains, Action<LogEntry> log)
        {
            const double centerToleranceFeet = 50.0 / 304.8; // 50 mm
            const double sizeToleranceMm = 15.0;

            var matched = new List<DrainItem>();

            foreach (var sig in signatures ?? new List<DrainSelectionSignature>())
            {
                DrainItem best = null;
                double bestDist = double.MaxValue;

                foreach (var candidate in detectedDrains)
                {
                    if (candidate.CenterPoint == null) continue;

                    double dx = candidate.CenterPoint.X - sig.CenterX;
                    double dy = candidate.CenterPoint.Y - sig.CenterY;
                    double dz = candidate.CenterPoint.Z - sig.CenterZ;
                    double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);

                    bool sizeMatches = Math.Abs(candidate.Width - sig.Width) < sizeToleranceMm
                                     && Math.Abs(candidate.Height - sig.Height) < sizeToleranceMm;

                    if (sizeMatches && dist < centerToleranceFeet && dist < bestDist)
                    {
                        best = candidate;
                        bestDist = dist;
                    }
                }

                if (best != null)
                {
                    matched.Add(best);
                }
                else
                {
                    log(new LogEntry(LogLevel.Warning,
                        $"Selected drain near ({sig.CenterX * 304.8:F0}, {sig.CenterY * 304.8:F0}) mm " +
                        "was not found after re-detection (roof may have changed) — skipped."));
                }
            }

            return matched;
        }

        private static AutoSlopeDrainResult Fail(string message, Action<LogEntry> log)
        {
            log(new LogEntry(LogLevel.Error, message));
            return new AutoSlopeDrainResult
            {
                Success = false,
                ErrorMessage = message,
                Status = AppConstants.Status_Failed
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
