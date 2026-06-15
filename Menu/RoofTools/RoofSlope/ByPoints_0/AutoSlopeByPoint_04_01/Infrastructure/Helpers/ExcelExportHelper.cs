// =======================================================
// File: ExcelExportHelper.cs
// MIGRATION: EPPlus → ClosedXML (MIT licence, free for commercial use)
//
// API mapping summary:
//   ExcelPackage              → XLWorkbook
//   package.Workbook
//     .Worksheets.Add(name)   → workbook.Worksheets.Add(name)
//   sheet.Cells[row, col]     → sheet.Cell(row, col)
//   sheet.Cells["A1:L1"]
//     .Merge = true           → sheet.Range("A1:L1").Merge()
//   .Style.Font.Bold          → .Style.Font.Bold  (same)
//   .Style.Font.Size          → .Style.Font.FontSize
//   .Style.Fill.PatternType +
//   .BackgroundColor
//     .SetColor(DrawingColor) → .Style.Fill.BackgroundColor
//                                = XLColor.FromColor(DrawingColor)
//   sheet.Column(n).Width     → sheet.Column(n).Width  (same)
//   sheet.Cells[..].
//     AutoFitColumns()        → sheet.Columns().AdjustToContents()
//   package.SaveAs(FileInfo)  → workbook.SaveAs(string path)
//
// No LicenseContext needed — ClosedXML is MIT.
// =======================================================

using Autodesk.Revit.DB;
using ClosedXML.Excel;
using Revit26_Plugin.AutoSlopeByPoint_04_01.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SysColor = System.Drawing.Color;  // alias avoids clash with Autodesk.Revit.DB.Color

namespace Revit26_Plugin.AutoSlopeByPoint_04_01.Infrastructure.Helpers
{
    public static class ExcelExportHelper
    {
        // No static constructor needed — ClosedXML requires no licence setup.

        // ── Public export methods ────────────────────────────────────────────

        /// <summary>
        /// Called by the "Export Results" button in the ViewModel.
        /// Produces a two-sheet workbook (placeholder + Run Summary)
        /// to a user-chosen file path.
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
                using (var workbook = new XLWorkbook())
                {
                    // Sheet 1 — placeholder (vertex data not available here)
                    var placeholderSheet = workbook.Worksheets.Add("Vertex Data");
                    placeholderSheet.Cell(1, 1).Value =
                        "Full vertex data is in the auto-export file generated immediately after Run.";
                    placeholderSheet.Cell(1, 1).Style.Font.Italic = true;
                    placeholderSheet.Range(1, 1, 1, 5).Merge();
                    placeholderSheet.Column(1).Width = 80;

                    // Sheet 2 — run summary
                    var summarySheet = workbook.Worksheets.Add("Run Summary");
                    FillRunSummarySheet(
                        summarySheet, result, slopePercent,
                        thresholdMeters, enableDrainTolerance,
                        drainToleranceMm, exportFolderPath);

                    workbook.SaveAs(filePath);
                }

                return filePath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Excel export failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Kept for backward compatibility — delegates to ExportCompactVertexData.
        /// </summary>
        public static string ExportDetailedVertexData(
            AutoSlopePayload payload,
            List<VertexData> vertexData,
            RoofBase roof,
            List<XYZ> drainPoints,
            double slopePercent)
        {
            return ExportCompactVertexData(payload, vertexData, roof, slopePercent);
        }

        /// <summary>
        /// Produces a two-sheet workbook:
        ///   Sheet 1 "Vertex Data"  — all vertices (processed + skipped).
        ///   Sheet 2 "Run Summary"  — parameter/value run summary table.
        /// </summary>
        public static string ExportCompactVertexData(
            AutoSlopePayload payload,
            List<VertexData> vertexData,
            RoofBase roof,
            double slopePercent)
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

                using (var workbook = new XLWorkbook())
                {
                    // ════════════════════════════════════════════════════════
                    // SHEET 1 — Vertex Data
                    // ════════════════════════════════════════════════════════
                    var vertSheet = workbook.Worksheets.Add("Vertex Data");

                    // Title row
                    var titleCell = vertSheet.Cell(1, 1);
                    titleCell.Value = "AUTOSLOPE — VERTEX DATA";
                    titleCell.Style.Font.Bold = true;
                    titleCell.Style.Font.FontSize = 14;
                    titleCell.Style.Fill.BackgroundColor = XLColor.FromColor(SysColor.LightBlue);
                    vertSheet.Range(1, 1, 1, 12).Merge();

                    // Column headers (row 3)
                    string[] headers =
                    {
                        "VertexIndex", "DrainIndex", "WasProcessed",
                        "RoofElementId", "RoofTypeName", "BaseLevel", "LevelOffset_mm",
                        "PathLength_m", "SlopePercent",
                        "ElevCalc_mm", "ElevModel_mm", "ElevDiff_mm"
                    };

                    for (int i = 0; i < headers.Length; i++)
                    {
                        var hCell = vertSheet.Cell(3, i + 1);
                        hCell.Value = headers[i];
                        hCell.Style.Font.Bold = true;
                        hCell.Style.Fill.BackgroundColor = XLColor.FromColor(SysColor.LightGray);
                    }

                    // Highlight the three elevation column headers
                    vertSheet.Cell(3, 10).Style.Fill.BackgroundColor = XLColor.FromColor(SysColor.LightGreen);
                    vertSheet.Cell(3, 11).Style.Fill.BackgroundColor = XLColor.FromColor(SysColor.LightSkyBlue);
                    vertSheet.Cell(3, 12).Style.Fill.BackgroundColor = XLColor.FromColor(SysColor.LightYellow);

                    // Sort: processed first (longest → shortest path), then skipped
                    var sorted = vertexData
                        .Where(v => v.WasProcessed)
                        .OrderByDescending(v => v.PathLengthMeters)
                        .Concat(vertexData.Where(v => !v.WasProcessed))
                        .ToList();

                    int row = 4;
                    foreach (var v in sorted)
                    {
                        vertSheet.Cell(row, 1).Value = v.VertexIndex;
                        if (v.WasProcessed) vertSheet.Cell(row, 2).Value = v.NearestDrainIndex;
                        else vertSheet.Cell(row, 2).Value = "—";
                        vertSheet.Cell(row, 3).Value = v.WasProcessed ? "YES" : "NO";
                        vertSheet.Cell(row, 4).Value = roof.Id.Value;
                        vertSheet.Cell(row, 5).Value = roofType;
                        vertSheet.Cell(row, 6).Value = baseLevelName;
                        vertSheet.Cell(row, 7).Value = Math.Round(baseOffset, 0);
                        vertSheet.Cell(row, 8).Value = Math.Round(v.PathLengthMeters, 2);
                        vertSheet.Cell(row, 9).Value = slopePercent;
                        if (v.WasProcessed) vertSheet.Cell(row, 10).Value = Math.Round(v.ElevationOffsetMm, 0);
                        else vertSheet.Cell(row, 10).Value = "—";
                        if (v.WasProcessed) vertSheet.Cell(row, 11).Value = Math.Round(v.ElevationFromModel_mm, 0);
                        else vertSheet.Cell(row, 11).Value = "—";

                        if (v.WasProcessed)
                        {
                            double diff = Math.Round(v.ElevationDiff_mm, 0);
                            vertSheet.Cell(row, 12).Value = diff;
                            vertSheet.Cell(row, 12).Style.Fill.BackgroundColor =
                                XLColor.FromColor(diff == 0 ? SysColor.LightGreen : SysColor.Orange);
                        }
                        else
                        {
                            vertSheet.Cell(row, 12).Value = "—";
                            // Highlight entire skipped row in light yellow
                            vertSheet.Range(row, 1, row, 12).Style.Fill.BackgroundColor =
                                XLColor.FromColor(SysColor.LightYellow);
                        }

                        row++;
                    }

                    // ── Summary block below the data ─────────────────────────
                    row += 2;
                    int processed = sorted.Count(v => v.WasProcessed);
                    int skipped = sorted.Count(v => !v.WasProcessed);
                    int adjusted = sorted.Count(v => v.WasProcessed && Math.Round(v.ElevationDiff_mm, 0) != 0);

                    var summaryLabel = vertSheet.Cell(row, 1);
                    summaryLabel.Value = "SUMMARY";
                    summaryLabel.Style.Font.Bold = true;
                    summaryLabel.Style.Font.FontSize = 12;
                    row++;

                    vertSheet.Cell(row, 1).Value = "Total vertices:"; vertSheet.Cell(row, 2).Value = sorted.Count; row++;
                    vertSheet.Cell(row, 1).Value = "Processed:"; vertSheet.Cell(row, 2).Value = processed; row++;
                    vertSheet.Cell(row, 1).Value = "Skipped:"; vertSheet.Cell(row, 2).Value = skipped; row++;
                    vertSheet.Cell(row, 1).Value = "Adjusted by Revit:"; vertSheet.Cell(row, 2).Value = adjusted;
                    vertSheet.Cell(row, 3).Value = adjusted > 0 ? "⚠ check ElevDiff_mm" : "✓ none";
                    row++;

                    if (processed > 0)
                    {
                        var processedRows = sorted.Where(v => v.WasProcessed).ToList();
                        vertSheet.Cell(row, 1).Value = "Longest path (m):";
                        vertSheet.Cell(row, 2).Value = Math.Round(processedRows.Max(v => v.PathLengthMeters), 2); row++;
                        vertSheet.Cell(row, 1).Value = "Shortest path (m):";
                        vertSheet.Cell(row, 2).Value = Math.Round(processedRows.Min(v => v.PathLengthMeters), 2); row++;
                    }

                    // Bold all labels in the summary block
                    for (int r = row - processed > 0 ? 6 : 4; r < row; r++)
                        vertSheet.Cell(r, 1).Style.Font.Bold = true;

                    vertSheet.Columns().AdjustToContents();

                    // ════════════════════════════════════════════════════════
                    // SHEET 2 — Run Summary
                    // ════════════════════════════════════════════════════════
                    var summarySheet = workbook.Worksheets.Add("Run Summary");

                    var syntheticResult = new AutoSlopeResult
                    {
                        Success = true,
                        VerticesProcessed = processed,
                        VerticesSkipped = skipped,
                        RunDate = DateTime.Now.ToString("dd-MM-yy HH:mm")
                    };

                    FillRunSummarySheet(
                        summarySheet,
                        syntheticResult,
                        slopePercent,
                        (int)payload.ThresholdMeters,
                        payload.EnableDrainTolerance,
                        payload.DrainToleranceMm,
                        payload.ExportConfig.ExportPath);

                    workbook.SaveAs(filePath);
                }

                return filePath;
            }
            catch (Exception ex)
            {
                payload?.Log?.Invoke($"Excel Export Error: {ex.Message}");
                return null;
            }
        }

        // ── Private sheet-fill helpers ───────────────────────────────────────

        /// <summary>
        /// Fills a sheet with a two-column Parameter / Value run summary table.
        /// Used by both the auto-export (Sheet 2) and the Export Results button.
        /// </summary>
        private static void FillRunSummarySheet(
            IXLWorksheet sheet,
            AutoSlopeResult result,
            double slopePercent,
            int thresholdMeters,
            bool enableDrainTolerance,
            int drainToleranceMm,
            string exportFolderPath)
        {
            // Title
            var titleCell = sheet.Cell(1, 1);
            titleCell.Value = "AUTOSLOPE — RUN SUMMARY";
            titleCell.Style.Font.Bold = true;
            titleCell.Style.Font.FontSize = 14;
            titleCell.Style.Fill.BackgroundColor = XLColor.FromColor(SysColor.LightBlue);
            sheet.Range(1, 1, 1, 2).Merge();

            // Export date
            sheet.Cell(2, 1).Value = "Export Date:";
            sheet.Cell(2, 1).Style.Font.Bold = true;
            sheet.Cell(2, 2).Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // Header row
            int row = 4;
            sheet.Cell(row, 1).Value = "Parameter";
            sheet.Cell(row, 2).Value = "Value";
            sheet.Range(row, 1, row, 2).Style.Font.Bold = true;
            sheet.Range(row, 1, row, 2).Style.Fill.BackgroundColor = XLColor.FromColor(SysColor.LightGray);
            row++;

            AddInfoRow(sheet, ref row, "Run Date", result.RunDate ?? DateTime.Now.ToString("dd-MM-yy HH:mm"));
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

            sheet.Columns().AdjustToContents();
        }

        // ── Row helper ───────────────────────────────────────────────────────

        private static void AddInfoRow(IXLWorksheet sheet, ref int row, string label, string value)
        {
            sheet.Cell(row, 1).Value = label;
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Cell(row, 2).Value = value;
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