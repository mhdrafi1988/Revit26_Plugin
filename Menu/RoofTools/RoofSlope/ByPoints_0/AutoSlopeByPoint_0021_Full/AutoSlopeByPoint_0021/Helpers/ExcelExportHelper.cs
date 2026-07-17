using Revit26_Plugin.Shared.Models;
// =======================================================
// File: ExcelExportHelper.cs
// NEW CHANGES:
//   Single combined workbook — two sheets in one file:
//
//   Sheet 1 "Vertex Data":
//     All vertices including skipped ones.
//     New columns added: VertexIndex, DrainIndex, WasProcessed.
//     Skipped rows highlighted in light yellow.
//     ElevCalc_mm, ElevModel_mm, ElevDiff_mm retained.
//     ElevDiff_mm colour-coded green/orange.
//
//   Sheet 2 "Run Summary":
//     Parameter/value table — previously a separate file
//     produced by ExportResultsSummary.
//     Now generated alongside Sheet 1 in the same workbook.
//
//   ExportResultsSummary is kept for the "Export Results"
//   button path but now delegates to ExportCompactVertexData
//   so both sheets are always produced together.
//
// Earlier fixes retained:
//   #8  GetUniqueFilePath iterative — no unbounded recursion.
//   ElevationOffset_mm split into ElevCalc/ElevModel/ElevDiff.
// =======================================================

using Autodesk.Revit.DB;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using Revit26_Plugin.AutoSlopeByPoint.V021.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DrawingColor = System.Drawing.Color;

namespace Revit26_Plugin.AutoSlopeByPoint.V021.Infrastructure.Helpers
{
    public static class ExcelExportHelper
    {
        // ── License (single authoritative location) ──────────────────────────
        static ExcelExportHelper()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        // ── Public export methods ────────────────────────────────────────────

        /// <summary>
        /// Called by the "Export Results" button in the ViewModel.
        /// Produces the same combined two-sheet workbook as the
        /// auto-export after Run, using a user-chosen file path.
        /// </summary>
        public static string ExportResultsSummary(
            string filePath,
            AutoSlopeResult result,
            double slopePercent,
            int thresholdMeters,
            bool enableDrainTolerance,
            int drainToleranceMm,
            string exportFolderPath)
        {
            if (string.IsNullOrEmpty(filePath) || result == null) return null;

            try
            {
                using (var package = new ExcelPackage())
                {
                    // Sheet 1 — vertex data not available from this call path,
                    // so produce a clear placeholder telling the user to use
                    // the auto-export file for full vertex data.
                    var placeholderSheet = package.Workbook.Worksheets.Add("Vertex Data");
                    placeholderSheet.Cells[1, 1].Value =
                        "Full vertex data is in the auto-export file " +
                        "generated immediately after Run.";
                    placeholderSheet.Cells[1, 1].Style.Font.Italic = true;
                    placeholderSheet.Cells[1, 1, 1, 5].Merge = true;
                    placeholderSheet.Column(1).Width = 80;

                    // Sheet 2 — run summary
                    var summarySheet = package.Workbook.Worksheets.Add("Run Summary");
                    FillRunSummarySheet(
                        summarySheet, result, slopePercent,
                        thresholdMeters, enableDrainTolerance,
                        drainToleranceMm, exportFolderPath);

                    package.SaveAs(new FileInfo(filePath));
                }

                return filePath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Excel export failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Previously produced a separate detailed workbook.
        /// Now merged into ExportCompactVertexData as Sheet 1.
        /// This method is kept so existing call sites compile —
        /// it simply delegates to ExportCompactVertexData.
        /// </summary>
        public static string ExportDetailedVertexData(
            AutoSlopePayload payload,
            List<VertexData> vertexData,
            RoofBase roof,
            List<XYZ> drainPoints,
            double slopePercent,
            List<RidgePairResult> ridgePairResults = null,
            List<RidgeJunctionResult> ridgeJunctionResults = null)
        {
            // drainPoints not needed now — drain index is stored per-vertex
            return ExportCompactVertexData(payload, vertexData, roof, slopePercent,
                ridgePairResults: ridgePairResults, ridgeJunctionResults: ridgeJunctionResults);
        }

        /// <summary>
        /// Produces a two-sheet workbook:
        ///   Sheet 1 "Vertex Data"  — all vertices (processed + skipped),
        ///                            with VertexIndex, DrainIndex, WasProcessed.
        ///   Sheet 2 "Run Summary"  — parameter/value run summary table.
        /// </summary>
        public static string ExportCompactVertexData(
            AutoSlopePayload payload,
            List<VertexData> vertexData,
            RoofBase roof,
            double slopePercent,
            string version = "P.10.00",
            int status = 1,
            List<RidgePairResult> ridgePairResults = null,
            List<RidgeJunctionResult> ridgeJunctionResults = null)
        {
            if (payload?.ExportConfig == null || !payload.ExportConfig.ExportToExcel || vertexData == null)
                return null;

            try
            {
                string roofId = roof.Id.Value.ToString();
                string slopeStr = slopePercent.ToString("0.00", CultureInfo.InvariantCulture);
                string dateStr = DateTime.Now.ToString("dd-MM-yy");
                string fileName = $"{roofId}_{slopeStr}_{dateStr}.xlsx";
                string filePath = GetUniqueFilePath(Path.Combine(payload.ExportConfig.ExportPath, fileName));

                // ── Roof meta ────────────────────────────────────────────────
                string roofType = roof.Name ?? "Unknown";
                string baseLevelName = "Unknown";
                double baseOffset = 0;

                if (roof.LevelId != null && roof.LevelId != ElementId.InvalidElementId)
                {
                    Level level = roof.Document.GetElement(roof.LevelId) as Level;
                    baseLevelName = level?.Name ?? "Unknown";
                }

                Parameter offsetParam = roof.get_Parameter(BuiltInParameter.ROOF_CONSTRAINT_OFFSET_PARAM);
                if (offsetParam != null && offsetParam.HasValue)
                    baseOffset = UnitUtils.ConvertFromInternalUnits(
                        offsetParam.AsDouble(), UnitTypeId.Millimeters);

                using (var package = new ExcelPackage())
                {
                    // ════════════════════════════════════════════════════════
                    // SHEET 1 — Vertex Data
                    // ════════════════════════════════════════════════════════
                    var vertSheet = package.Workbook.Worksheets.Add("Vertex Data");

                    // Title
                    vertSheet.Cells["A1"].Value = "AUTOSLOPE — VERTEX DATA";
                    vertSheet.Cells["A1:L1"].Merge = true;
                    vertSheet.Cells["A1"].Style.Font.Bold = true;
                    vertSheet.Cells["A1"].Style.Font.Size = 14;
                    vertSheet.Cells["A1"].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    vertSheet.Cells["A1"].Style.Fill.BackgroundColor.SetColor(DrawingColor.LightBlue);

                    // Column headers
                    // Columns: VertexIndex | DrainIndex | WasProcessed |
                    //          RoofElementId | RoofTypeName | BaseLevel | LevelOffset_mm |
                    //          PathLength_m | SlopePercent |
                    //          ElevCalc_mm | ElevModel_mm | ElevDiff_mm | IsRidgePoint |
                    //          GoverningGroup
                    // IsRidgePoint (V021): "YES (Pair N)" if this vertex was selected by
                    // Ridge Point Detection, blank otherwise. See the separate "Ridge
                    // Points" sheet for full per-pair detail.
                    // GoverningGroup (V021): only meaningful for ridge points — the
                    // farther adjacent drain group whose Dijkstra distance drove this
                    // vertex's elevation.
                    string[] headers =
                    {
                        "VertexIndex", "DrainIndex", "WasProcessed",
                        "RoofElementId", "RoofTypeName", "BaseLevel", "LevelOffset_mm",
                        "PathLength_m", "SlopePercent",
                        "ElevCalc_mm", "ElevModel_mm", "ElevDiff_mm", "IsRidgePoint",
                        "GoverningGroup"
                    };

                    for (int i = 0; i < headers.Length; i++)
                    {
                        var hCell = vertSheet.Cells[3, i + 1];
                        hCell.Value = headers[i];
                        hCell.Style.Font.Bold = true;
                        hCell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        hCell.Style.Fill.BackgroundColor.SetColor(DrawingColor.LightGray);
                    }

                    // Highlight the three elevation headers
                    vertSheet.Cells[3, 10].Style.Fill.BackgroundColor.SetColor(DrawingColor.LightGreen);
                    vertSheet.Cells[3, 11].Style.Fill.BackgroundColor.SetColor(DrawingColor.LightSkyBlue);
                    vertSheet.Cells[3, 12].Style.Fill.BackgroundColor.SetColor(DrawingColor.LightYellow);

                    // Sort: processed first (longest path → shortest), then skipped
                    var sorted = vertexData
                        .Where(v => v.WasProcessed)
                        .OrderByDescending(v => v.PathLengthMeters)
                        .Concat(vertexData.Where(v => !v.WasProcessed))
                        .ToList();

                    int row = 4;
                    foreach (var v in sorted)
                    {
                        vertSheet.Cells[row, 1].Value = v.VertexIndex;
                        // DrainIndex: -1 for skipped, show as "—" for clarity
                        vertSheet.Cells[row, 2].Value = v.WasProcessed ? (object)v.NearestDrainIndex : "—";
                        vertSheet.Cells[row, 3].Value = v.WasProcessed ? "YES" : "NO";
                        vertSheet.Cells[row, 4].Value = roof.Id.Value;
                        vertSheet.Cells[row, 5].Value = roofType;
                        vertSheet.Cells[row, 6].Value = baseLevelName;
                        vertSheet.Cells[row, 7].Value = Math.Round(baseOffset, 0);
                        vertSheet.Cells[row, 8].Value = Math.Round(v.PathLengthMeters, 2);
                        vertSheet.Cells[row, 9].Value = slopePercent;
                        vertSheet.Cells[row, 10].Value = v.WasProcessed ? (object)Math.Round(v.ElevationOffsetMm, 0) : "—";
                        vertSheet.Cells[row, 11].Value = v.WasProcessed ? (object)Math.Round(v.ElevationFromModel_mm, 0) : "—";

                        if (v.WasProcessed)
                        {
                            double diff = Math.Round(v.ElevationDiff_mm, 0);
                            vertSheet.Cells[row, 12].Value = diff;
                            vertSheet.Cells[row, 12].Style.Fill.PatternType = ExcelFillStyle.Solid;
                            vertSheet.Cells[row, 12].Style.Fill.BackgroundColor.SetColor(
                                diff == 0 ? DrawingColor.LightGreen : DrawingColor.Orange);
                        }
                        else
                        {
                            vertSheet.Cells[row, 12].Value = "—";
                            // Highlight entire skipped row in light yellow
                            vertSheet.Cells[row, 1, row, 12].Style.Fill.PatternType = ExcelFillStyle.Solid;
                            vertSheet.Cells[row, 1, row, 12].Style.Fill.BackgroundColor
                                .SetColor(DrawingColor.LightYellow);
                        }

                        // V021: mark ridge-point rows and cross-reference their pair/junction number.
                        if (v.IsRidgePoint)
                        {
                            vertSheet.Cells[row, 13].Value = v.IsJunctionPoint
                                ? $"YES (Junction {v.RidgeJunctionIndex})"
                                : $"YES (Pair {v.RidgePairIndex})";
                            vertSheet.Cells[row, 13].Style.Fill.PatternType = ExcelFillStyle.Solid;
                            vertSheet.Cells[row, 13].Style.Fill.BackgroundColor.SetColor(
                                v.IsJunctionPoint ? DrawingColor.Gold : DrawingColor.Plum);

                            // V021 — which drain group governed this ridge point's elevation.
                            vertSheet.Cells[row, 14].Value = v.RidgeReferenceGroupIndex >= 0
                                ? $"G{v.RidgeReferenceGroupIndex + 1}"
                                : "—";
                        }
                        else
                        {
                            vertSheet.Cells[row, 13].Value = "";
                            vertSheet.Cells[row, 14].Value = "";
                        }

                        row++;
                    }

                    // Summary block below the data
                    row += 2;
                    int processed = sorted.Count(v => v.WasProcessed);
                    int skipped = sorted.Count(v => !v.WasProcessed);
                    int adjusted = sorted.Count(v => v.WasProcessed && Math.Round(v.ElevationDiff_mm, 0) != 0);

                    vertSheet.Cells[row, 1].Value = "SUMMARY";
                    vertSheet.Cells[row, 1].Style.Font.Bold = true;
                    vertSheet.Cells[row, 1].Style.Font.Size = 12;
                    row++;
                    vertSheet.Cells[row, 1].Value = "Total vertices:"; vertSheet.Cells[row, 2].Value = sorted.Count; row++;
                    vertSheet.Cells[row, 1].Value = "Processed:"; vertSheet.Cells[row, 2].Value = processed; row++;
                    vertSheet.Cells[row, 1].Value = "Skipped:"; vertSheet.Cells[row, 2].Value = skipped; row++;
                    vertSheet.Cells[row, 1].Value = "Adjusted by Revit:"; vertSheet.Cells[row, 2].Value = adjusted;
                    vertSheet.Cells[row, 3].Value = adjusted > 0 ? "⚠ check ElevDiff_mm" : "✓ none"; row++;

                    if (processed > 0)
                    {
                        var processedRows = sorted.Where(v => v.WasProcessed).ToList();
                        vertSheet.Cells[row, 1].Value = "Longest path (m):";
                        vertSheet.Cells[row, 2].Value = Math.Round(processedRows.Max(v => v.PathLengthMeters), 2); row++;
                        vertSheet.Cells[row, 1].Value = "Shortest path (m):";
                        vertSheet.Cells[row, 2].Value = Math.Round(processedRows.Min(v => v.PathLengthMeters), 2); row++;
                    }

                    vertSheet.Cells[vertSheet.Dimension.Address].AutoFitColumns();

                    // ════════════════════════════════════════════════════════
                    // SHEET 2 — Run Summary
                    // ════════════════════════════════════════════════════════
                    var summarySheet = package.Workbook.Worksheets.Add("Run Summary");

                    // Build a lightweight AutoSlopeResult from the vertexData
                    // so FillRunSummarySheet can use the shared helper.
                    var syntheticResult = new AutoSlopeResult
                    {
                        Success = true,
                        VerticesProcessed = processed,
                        VerticesSkipped = skipped,
                        RunDate = DateTime.Now.ToString("dd-MM-yy HH:mm"),
                        Version = version,
                        Status = status
                    };

                    FillRunSummarySheet(
                        summarySheet,
                        syntheticResult,
                        slopePercent,
                        (int)payload.ThresholdMeters,
                        payload.EnableDrainTolerance,
                        payload.DrainToleranceMm,
                        payload.ExportConfig.ExportPath);

                    // ════════════════════════════════════════════════════════
                    // SHEET 3 — Ridge Points (V021, only when the feature ran
                    // and produced at least one pair result; sheet omitted
                    // entirely otherwise so existing exports are unaffected).
                    // ════════════════════════════════════════════════════════
                    if (payload.EnableRidgePointDetection && ridgePairResults != null && ridgePairResults.Count > 0)
                    {
                        FillRidgePointsSheet(package.Workbook.Worksheets.Add("Ridge Points"), ridgePairResults);
                    }

                    // ════════════════════════════════════════════════════════
                    // SHEET 4 — Ridge Junctions (V021 3rd pass, multi-group 3+
                    // Voronoi vertex ridge points; sheet omitted entirely if no
                    // junctions were found/candidates existed, same as Sheet 3).
                    // ════════════════════════════════════════════════════════
                    if (payload.EnableRidgePointDetection && ridgeJunctionResults != null && ridgeJunctionResults.Count > 0)
                    {
                        FillRidgeJunctionsSheet(package.Workbook.Worksheets.Add("Ridge Junctions"), ridgeJunctionResults);
                    }

                    package.SaveAs(new FileInfo(filePath));
                }

                return filePath;
            }
            catch (Exception ex)
            {
                payload?.Log?.Invoke(new Revit26_Plugin.Shared.Models.LogEntry(Revit26_Plugin.Shared.Models.LogLevel.Error, $"Excel Export Error: {ex.Message}"));
                return null;
            }
        }
        // ── Private sheet-fill helpers ───────────────────────────────────────

        /// <summary>
        /// Fills a sheet with a two-column Parameter / Value run summary table.
        /// Used by both the auto-export (Sheet 2) and the Export Results button.
        /// </summary>
        // ── Private sheet-fill helpers ───────────────────────────────────────

        /// <summary>
        /// Fills a sheet with a two-column Parameter / Value run summary table.
        /// Used by both the auto-export (Sheet 2) and the Export Results button.
        /// </summary>
        private static void FillRunSummarySheet(
            ExcelWorksheet sheet,
            AutoSlopeResult result,
            double slopePercent,
            int thresholdMeters,
            bool enableDrainTolerance,
            int drainToleranceMm,
            string exportFolderPath)
        {
            // Title
            sheet.Cells[1, 1].Value = "AUTOSLOPE — RUN SUMMARY";
            sheet.Cells[1, 1, 1, 2].Merge = true;
            sheet.Cells[1, 1].Style.Font.Bold = true;
            sheet.Cells[1, 1].Style.Font.Size = 14;
            sheet.Cells[1, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            sheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(DrawingColor.LightBlue);

            sheet.Cells[2, 1].Value = "Export Date:";
            sheet.Cells[2, 1].Style.Font.Bold = true;
            sheet.Cells[2, 2].Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // Header row
            int row = 4;
            sheet.Cells[row, 1].Value = "Parameter";
            sheet.Cells[row, 2].Value = "Value";
            sheet.Cells[row, 1, row, 2].Style.Font.Bold = true;
            sheet.Cells[row, 1, row, 2].Style.Fill.PatternType = ExcelFillStyle.Solid;
            sheet.Cells[row, 1, row, 2].Style.Fill.BackgroundColor.SetColor(DrawingColor.LightGray);
            row++;

            AddInfoRow(sheet, ref row, "Run Date", result.RunDate ?? DateTime.Now.ToString("dd-MM-yy HH:mm"));
            AddInfoRow(sheet, ref row, "Version", result.Version ?? "N/A");
            AddInfoRow(sheet, ref row, "Status", StatusToText(result.Status));
            AddInfoRow(sheet, ref row, "Slope Percentage", $"{slopePercent}%");
            AddInfoRow(sheet, ref row, "Threshold (m)", thresholdMeters.ToString());
            AddInfoRow(sheet, ref row, "Drain Tolerance Enabled", enableDrainTolerance ? "Yes" : "No");
            AddInfoRow(sheet, ref row, "Drain Tolerance (mm)", enableDrainTolerance ? drainToleranceMm.ToString() : "N/A");
            row++; // blank separator
            AddInfoRow(sheet, ref row, "Vertices Processed", result.VerticesProcessed.ToString());
            AddInfoRow(sheet, ref row, "Vertices Skipped", result.VerticesSkipped.ToString());
            AddInfoRow(sheet, ref row, "Picked Drain Count", result.PickedDrainCount.ToString());
            AddInfoRow(sheet, ref row, "Final Drain Count", result.FinalDrainCount.ToString());
            row++; // blank separator
            AddInfoRow(sheet, ref row, "Highest Elevation (mm)", $"{result.HighestElevation_mm:0}");
            AddInfoRow(sheet, ref row, "Longest Path (m)", $"{result.LongestPath_m:0.00}");
            AddInfoRow(sheet, ref row, "Run Duration (sec)", result.RunDuration_sec.ToString());
            row++; // blank separator
            AddInfoRow(sheet, ref row, "Export Folder", exportFolderPath);

            sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
        }

        /// <summary>
        /// V021: one row per adjacent drain-group pair processed by Ridge Point
        /// Detection — which groups, center distance, corridor width used, how
        /// many vertices were found within it, and the full list of matched
        /// vertex indices (comma-separated — count is no longer capped at 2,
        /// per the corridor-based search). Skipped pairs (no vertex found within
        /// the corridor) are included and flagged, per the confirmed
        /// "skip silently, log info" behavior — visible here even though they
        /// only produced an Info-level log line at run time.
        /// </summary>
        private static void FillRidgePointsSheet(ExcelWorksheet sheet, List<RidgePairResult> ridgePairResults)
        {
            sheet.Cells[1, 1].Value = "AUTOSLOPE — RIDGE POINTS (V021)";
            sheet.Cells[1, 1, 1, 8].Merge = true;
            sheet.Cells[1, 1].Style.Font.Bold = true;
            sheet.Cells[1, 1].Style.Font.Size = 14;
            sheet.Cells[1, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            sheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(DrawingColor.LightBlue);

            string[] headers =
            {
                "PairIndex", "GroupA", "GroupB", "CenterDistance_m",
                "EdgeTolerance_mm", "RidgeLineSource", "MatchedVertexCount", "MatchedVertexIndices"
            };

            int headerRow = 3;
            for (int i = 0; i < headers.Length; i++)
            {
                var hCell = sheet.Cells[headerRow, i + 1];
                hCell.Value = headers[i];
                hCell.Style.Font.Bold = true;
                hCell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                hCell.Style.Fill.BackgroundColor.SetColor(DrawingColor.LightGray);
            }

            int row = headerRow + 1;
            foreach (var pair in ridgePairResults.OrderBy(p => p.PairIndex))
            {
                sheet.Cells[row, 1].Value = pair.PairIndex;
                sheet.Cells[row, 2].Value = $"G{pair.GroupAIndex + 1}";
                sheet.Cells[row, 3].Value = $"G{pair.GroupBIndex + 1}";
                sheet.Cells[row, 4].Value = Math.Round(
                    UnitUtils.ConvertFromInternalUnits(pair.CenterDistanceFt, UnitTypeId.Meters), 2);
                sheet.Cells[row, 5].Value = Math.Round(
                    UnitUtils.ConvertFromInternalUnits(pair.CorridorWidthFt, UnitTypeId.Millimeters), 0);
                sheet.Cells[row, 6].Value = pair.UsedVoronoiEdge ? "Voronoi" : "Fallback";
                sheet.Cells[row, 7].Value = pair.MatchedVertexIndices.Count;
                sheet.Cells[row, 8].Value = pair.MatchedVertexIndices.Count > 0
                    ? string.Join(", ", pair.MatchedVertexIndices)
                    : "—";

                if (pair.Skipped)
                {
                    sheet.Cells[row, 1, row, 8].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    sheet.Cells[row, 1, row, 8].Style.Fill.BackgroundColor.SetColor(DrawingColor.LightYellow);
                }

                row++;
            }

            row += 1;
            int resolvedPairs = ridgePairResults.Count(p => !p.Skipped);
            int skippedPairs = ridgePairResults.Count(p => p.Skipped);
            int totalPoints = ridgePairResults.Sum(p => p.ResolvedCount);
            int voronoiPairs = ridgePairResults.Count(p => p.UsedVoronoiEdge);

            sheet.Cells[row, 1].Value = "SUMMARY";
            sheet.Cells[row, 1].Style.Font.Bold = true;
            row++;
            AddInfoRow(sheet, ref row, "Pairs Processed", ridgePairResults.Count.ToString());
            AddInfoRow(sheet, ref row, "Pairs Resolved (>=1 vertex found)", resolvedPairs.ToString());
            AddInfoRow(sheet, ref row, "Pairs Skipped (no vertex near ridge line)", skippedPairs.ToString());
            AddInfoRow(sheet, ref row, "Total Ridge Points", totalPoints.ToString());
            AddInfoRow(sheet, ref row, "Pairs Using Real Voronoi Edge", voronoiPairs.ToString());

            sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
        }

        /// <summary>
        /// V021 (3rd pass): one row per multi-group (3+) Voronoi junction —
        /// which groups meet there, the junction point, circumradius (processing
        /// order), how many roof vertices matched, and their indices. Mirrors
        /// FillRidgePointsSheet's structure for consistency.
        /// </summary>
        private static void FillRidgeJunctionsSheet(ExcelWorksheet sheet, List<RidgeJunctionResult> ridgeJunctionResults)
        {
            sheet.Cells[1, 1].Value = "AUTOSLOPE — RIDGE JUNCTIONS (V021, 3+ GROUPS)";
            sheet.Cells[1, 1, 1, 7].Merge = true;
            sheet.Cells[1, 1].Style.Font.Bold = true;
            sheet.Cells[1, 1].Style.Font.Size = 14;
            sheet.Cells[1, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            sheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(DrawingColor.LightBlue);

            string[] headers =
            {
                "JunctionIndex", "Groups", "JunctionPoint_XYZ", "CircumRadius_m",
                "EdgeTolerance_mm", "MatchedVertexCount", "MatchedVertexIndices"
            };

            int headerRow = 3;
            for (int i = 0; i < headers.Length; i++)
            {
                var hCell = sheet.Cells[headerRow, i + 1];
                hCell.Value = headers[i];
                hCell.Style.Font.Bold = true;
                hCell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                hCell.Style.Fill.BackgroundColor.SetColor(DrawingColor.LightGray);
            }

            int row = headerRow + 1;
            foreach (var junction in ridgeJunctionResults.OrderBy(j => j.JunctionIndex))
            {
                sheet.Cells[row, 1].Value = junction.JunctionIndex;
                sheet.Cells[row, 2].Value = string.Join(",", junction.GroupIndices.Select(gi => $"G{gi + 1}"));
                sheet.Cells[row, 3].Value = junction.JunctionPoint != null
                    ? $"({junction.JunctionPoint.X:F3}, {junction.JunctionPoint.Y:F3}, {junction.JunctionPoint.Z:F3})"
                    : "—";
                sheet.Cells[row, 4].Value = Math.Round(
                    UnitUtils.ConvertFromInternalUnits(junction.CircumRadiusFt, UnitTypeId.Meters), 2);
                sheet.Cells[row, 5].Value = Math.Round(
                    UnitUtils.ConvertFromInternalUnits(junction.ToleranceFt, UnitTypeId.Millimeters), 0);
                sheet.Cells[row, 6].Value = junction.MatchedVertexIndices.Count;
                sheet.Cells[row, 7].Value = junction.MatchedVertexIndices.Count > 0
                    ? string.Join(", ", junction.MatchedVertexIndices)
                    : "—";

                if (junction.Skipped)
                {
                    sheet.Cells[row, 1, row, 7].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    sheet.Cells[row, 1, row, 7].Style.Fill.BackgroundColor.SetColor(DrawingColor.LightYellow);
                }

                row++;
            }

            row += 1;
            int resolvedJunctions = ridgeJunctionResults.Count(j => !j.Skipped);
            int skippedJunctions = ridgeJunctionResults.Count(j => j.Skipped);
            int totalJunctionPoints = ridgeJunctionResults.Sum(j => j.ResolvedCount);

            sheet.Cells[row, 1].Value = "SUMMARY";
            sheet.Cells[row, 1].Style.Font.Bold = true;
            row++;
            AddInfoRow(sheet, ref row, "Junctions Processed", ridgeJunctionResults.Count.ToString());
            AddInfoRow(sheet, ref row, "Junctions Resolved (>=1 vertex found)", resolvedJunctions.ToString());
            AddInfoRow(sheet, ref row, "Junctions Skipped (no vertex near junction point)", skippedJunctions.ToString());
            AddInfoRow(sheet, ref row, "Total Junction Ridge Points", totalJunctionPoints.ToString());

            sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
        }

        // ── Row helper ───────────────────────────────────────────────────────

        private static string StatusToText(int status)
        {
            switch (status)
            {
                case AppConstants.Status_OK:      return "OK";
                case AppConstants.Status_Partial: return "Partial";
                case AppConstants.Status_Failed:  return "Failed";
                default:                          return "Unknown";
            }
        }

        private static void AddInfoRow(ExcelWorksheet sheet, ref int row, string label, string value)
        {
            sheet.Cells[row, 1].Value = label;
            sheet.Cells[row, 1].Style.Font.Bold = true;
            sheet.Cells[row, 2].Value = value;
            row++;
        }

        // ── Fix #8: iterative unique-file-path — no unbounded recursion ──────
        private static string GetUniqueFilePath(string path)
        {
            if (!File.Exists(path)) return path;

            string dir = Path.GetDirectoryName(path) ?? string.Empty;
            string stem = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);

            var match = Regex.Match(stem, @"^(.*?)_(\d{2})$");
            string baseStem = match.Success ? match.Groups[1].Value : stem;

            for (int i = 1; i <= 99; i++)
            {
                string candidate = Path.Combine(dir, $"{baseStem}_{i:D2}{ext}");
                if (!File.Exists(candidate)) return candidate;
            }

            return Path.Combine(dir, $"{baseStem}_{DateTime.Now:HHmmss}{ext}");
        }
    }
}