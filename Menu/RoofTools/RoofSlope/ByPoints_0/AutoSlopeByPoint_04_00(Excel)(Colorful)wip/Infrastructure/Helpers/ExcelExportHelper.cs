// =======================================================
// File: ExcelExportHelper.cs
// Fixes:
//   #8  GetUniqueFilePath rewritten as an iterative loop —
//       no more unbounded recursion. Handles up to 99
//       collisions then falls back to a time-based suffix.
//   #10 ExportSummaryOnly filename: removed leading underscore
//       from "_Summary_" so the default prefix "AutoSlope_"
//       does not produce a double-underscore ("AutoSlope__Summary_").
//
// NEW CHANGES:
//   ElevationOffset_mm column split into three columns in
//   both compact and detailed (Vertices sheet) exports:
//
//     ElevCalc_mm   — elevation calculated from path × slope.
//                     This is what the engine wrote to Revit.
//     ElevModel_mm  — elevation read back from the roof vertex
//                     AFTER tx.Commit(). Reflects what Revit
//                     actually stored in the model.
//     ElevDiff_mm   — ElevModel_mm minus ElevCalc_mm.
//                     Zero = Revit accepted exactly what was written.
//                     Non-zero = Revit silently adjusted the vertex.
//
//   ElevDiff_mm cells are coloured:
//     Green  = 0 (exact match)
//     Orange = non-zero difference (Revit adjusted)
//
//   Statistics sheet updated: all three elevation metrics
//   (avg / max / min / range) are now shown for both
//   ElevCalc_mm and ElevModel_mm side by side.
//
// Note: AutoSlopeMetrics / ExportSummaryOnly are kept here
//       so the file compiles if anything still references them,
//       but see review issue #2 — they are dead code and can
//       be deleted once confirmed unused.
// =======================================================

using Autodesk.Revit.DB;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using Revit26_Plugin.AutoSlopeByPoint_04.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DrawingColor = System.Drawing.Color;

namespace Revit26_Plugin.AutoSlopeByPoint_04.Infrastructure.Helpers
{
    public static class ExcelExportHelper
    {
        // ── License (single authoritative location) ──────────────────────────
        static ExcelExportHelper()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        // ── Public export methods ────────────────────────────────────────────

        public static string ExportResultsSummary(
            string filePath,
            AutoSlopeResult result,
            double slopePercent,
            int thresholdMeters,
            bool enableDrainTolerance,
            int drainToleranceMm,
            string exportFolderPath)
        {
            if (string.IsNullOrEmpty(filePath)) return null;

            try
            {
                using (var package = new ExcelPackage())
                {
                    var sheet = package.Workbook.Worksheets.Add("AutoSlope Results");

                    sheet.Cells[1, 1].Value = "AutoSlope Results Export";
                    sheet.Cells[1, 1, 1, 2].Merge = true;
                    sheet.Cells[1, 1].Style.Font.Bold = true;
                    sheet.Cells[1, 1].Style.Font.Size = 14;

                    sheet.Cells[2, 1].Value = "Export Date:";
                    sheet.Cells[2, 1].Style.Font.Bold = true;
                    sheet.Cells[2, 2].Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                    int row = 4;
                    sheet.Cells[row, 1].Value = "Parameter";
                    sheet.Cells[row, 2].Value = "Value";
                    sheet.Cells[row, 1, row, 2].Style.Font.Bold = true;
                    sheet.Cells[row, 1, row, 2].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    sheet.Cells[row, 1, row, 2].Style.Fill.BackgroundColor.SetColor(DrawingColor.LightGray);
                    row++;

                    AddRow(sheet, ref row, "Vertices Processed", result.VerticesProcessed);
                    AddRow(sheet, ref row, "Vertices Skipped", result.VerticesSkipped);
                    AddRow(sheet, ref row, "Final Drain Count", result.FinalDrainCount);
                    AddRow(sheet, ref row, "Highest Elevation (mm)", $"{result.HighestElevation_mm:0}");
                    AddRow(sheet, ref row, "Longest Path (m)", $"{result.LongestPath_m:0.00}");
                    AddRow(sheet, ref row, "Run Duration (sec)", result.RunDuration_sec);
                    AddRow(sheet, ref row, "Run Date", result.RunDate);
                    AddRow(sheet, ref row, "Slope Percentage", $"{slopePercent}%");
                    AddRow(sheet, ref row, "Threshold (m)", thresholdMeters);
                    AddRow(sheet, ref row, "Drain Tolerance Enabled",
                        enableDrainTolerance ? "Yes" : "No");
                    AddRow(sheet, ref row, "Drain Tolerance (mm)",
                        enableDrainTolerance ? drainToleranceMm.ToString() : "N/A");
                    AddRow(sheet, ref row, "Export Folder", exportFolderPath);

                    sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
                    package.SaveAs(new FileInfo(filePath));
                }

                return filePath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Excel export failed: {ex.Message}", ex);
            }
        }

        public static string ExportDetailedVertexData(
            AutoSlopePayload payload,
            List<VertexData> vertexData,
            RoofBase roof,
            List<XYZ> drainPoints,
            double slopePercent)
        {
            if (payload?.ExportConfig == null || !payload.ExportConfig.ExportToExcel || vertexData == null)
                return null;

            try
            {
                string roofId = roof.Id.Value.ToString();
                string slopeStr = slopePercent.ToString("0.00", CultureInfo.InvariantCulture);
                string dateStr = DateTime.Now.ToString("dd-MM-yy");
                string fileName = $"{roofId}_{slopeStr}_{dateStr}_DETAILED.xlsx";
                string filePath = GetUniqueFilePath(Path.Combine(payload.ExportConfig.ExportPath, fileName));

                using (var package = new ExcelPackage())
                {
                    var summarySheet = package.Workbook.Worksheets.Add("Summary");
                    FillSummarySheet(summarySheet, payload, vertexData, roof, drainPoints, slopePercent);

                    var drainSheet = package.Workbook.Worksheets.Add("Drain Points");
                    FillDrainPointsSheet(drainSheet, drainPoints);

                    var verticesSheet = package.Workbook.Worksheets.Add("Vertices");
                    FillVerticesSheet(verticesSheet, vertexData, roof, slopePercent);

                    var statsSheet = package.Workbook.Worksheets.Add("Statistics");
                    FillStatisticsSheet(statsSheet, vertexData);

                    package.SaveAs(new FileInfo(filePath));
                }

                return filePath;
            }
            catch (Exception ex)
            {
                payload?.Log?.Invoke($"Detailed Excel Export Error: {ex.Message}");
                return null;
            }
        }

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
                    baseOffset = UnitUtils.ConvertFromInternalUnits(offsetParam.AsDouble(), UnitTypeId.Millimeters);

                string slopeStr = slopePercent.ToString("0.00", CultureInfo.InvariantCulture);
                string dateStr = DateTime.Now.ToString("dd-MM-yy");
                string fileName = $"{roofId}_{slopeStr}_{dateStr}.xlsx";
                string filePath = GetUniqueFilePath(Path.Combine(payload.ExportConfig.ExportPath, fileName));

                using (var package = new ExcelPackage())
                {
                    var sheet = package.Workbook.Worksheets.Add("AutoSlope Data");

                    // Title row — widened to 9 columns to cover new elevation columns
                    sheet.Cells["A1"].Value = "AUTOSLOPE COMPACT VERTEX EXPORT";
                    sheet.Cells["A1:I1"].Merge = true;
                    sheet.Cells["A1"].Style.Font.Bold = true;
                    sheet.Cells["A1"].Style.Font.Size = 14;
                    sheet.Cells["A1"].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    sheet.Cells["A1"].Style.Fill.BackgroundColor.SetColor(DrawingColor.LightBlue);

                    // NEW: ElevationOffset_mm replaced by three columns:
                    //   ElevCalc_mm  — from path × slope (what was written)
                    //   ElevModel_mm — read back from Revit after commit
                    //   ElevDiff_mm  — difference (0 = Revit accepted exactly)
                    string[] headers =
                    {
                        "RoofElementId", "RoofTypeName", "BaseLevel", "BaseOffset_mm",
                        "PathLength_Meters", "SlopePercent",
                        "ElevCalc_mm", "ElevModel_mm", "ElevDiff_mm"
                    };

                    for (int i = 0; i < headers.Length; i++)
                    {
                        sheet.Cells[3, i + 1].Value = headers[i];
                        sheet.Cells[3, i + 1].Style.Font.Bold = true;
                        sheet.Cells[3, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        sheet.Cells[3, i + 1].Style.Fill.BackgroundColor.SetColor(DrawingColor.LightGray);
                    }

                    // Colour the three new header cells to make them stand out
                    sheet.Cells[3, 7].Style.Fill.BackgroundColor.SetColor(DrawingColor.LightGreen);
                    sheet.Cells[3, 8].Style.Fill.BackgroundColor.SetColor(DrawingColor.LightSkyBlue);
                    sheet.Cells[3, 9].Style.Fill.BackgroundColor.SetColor(DrawingColor.LightYellow);

                    var sortedVertices = vertexData
                        .Where(v => v.WasProcessed)
                        .OrderByDescending(v => v.PathLengthMeters)
                        .ToList();

                    int row = 4;
                    foreach (var vertex in sortedVertices)
                    {
                        sheet.Cells[row, 1].Value = roof.Id.Value;
                        sheet.Cells[row, 2].Value = roofType;
                        sheet.Cells[row, 3].Value = baseLevelName;
                        sheet.Cells[row, 4].Value = Math.Round(baseOffset, 0);
                        sheet.Cells[row, 5].Value = Math.Round(vertex.PathLengthMeters, 2);
                        sheet.Cells[row, 6].Value = slopePercent;
                        sheet.Cells[row, 7].Value = Math.Round(vertex.ElevationOffsetMm, 0);
                        sheet.Cells[row, 8].Value = Math.Round(vertex.ElevationFromModel_mm, 0);

                        double diff = Math.Round(vertex.ElevationDiff_mm, 0);
                        sheet.Cells[row, 9].Value = diff;

                        // Colour ElevDiff_mm: green = exact match, orange = Revit adjusted
                        sheet.Cells[row, 9].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        sheet.Cells[row, 9].Style.Fill.BackgroundColor.SetColor(
                            diff == 0 ? DrawingColor.LightGreen : DrawingColor.Orange);

                        row++;
                    }

                    row += 2;
                    sheet.Cells[row, 1].Value = "SUMMARY";
                    sheet.Cells[row, 1].Style.Font.Bold = true;
                    sheet.Cells[row, 1].Style.Font.Size = 12;
                    sheet.Cells[row + 1, 1].Value = "Total Processed Vertices:";
                    sheet.Cells[row + 1, 2].Value = sortedVertices.Count;

                    if (sortedVertices.Any())
                    {
                        sheet.Cells[row + 2, 1].Value = "Longest Path:";
                        sheet.Cells[row + 2, 2].Value = sortedVertices.First().PathLengthMeters;
                        sheet.Cells[row + 2, 3].Value = "m";
                        sheet.Cells[row + 3, 1].Value = "Shortest Path:";
                        sheet.Cells[row + 3, 2].Value = sortedVertices.Last().PathLengthMeters;
                        sheet.Cells[row + 3, 3].Value = "m";

                        int adjustedCount = sortedVertices.Count(v => Math.Round(v.ElevationDiff_mm, 0) != 0);
                        sheet.Cells[row + 4, 1].Value = "Vertices adjusted by Revit:";
                        sheet.Cells[row + 4, 2].Value = adjustedCount;
                        sheet.Cells[row + 4, 3].Value = adjustedCount > 0 ? "⚠ check ElevDiff_mm column" : "✓ none";
                    }

                    sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
                    package.SaveAs(new FileInfo(filePath));
                }

                return filePath;
            }
            catch (Exception ex)
            {
                payload?.Log?.Invoke($"Compact Excel Export Error: {ex.Message}");
                return null;
            }
        }

        // ── Private sheet-fill helpers ───────────────────────────────────────

        private static void FillSummarySheet(
            ExcelWorksheet sheet, AutoSlopePayload payload,
            List<VertexData> vertexData, RoofBase roof,
            List<XYZ> drainPoints, double slopePercent)
        {
            int row = 1;

            sheet.Cells[row, 1].Value = "AUTOSLOPE DETAILED VERTEX EXPORT";
            sheet.Cells[row, 1, row, 4].Merge = true;
            sheet.Cells[row, 1].Style.Font.Bold = true;
            sheet.Cells[row, 1].Style.Font.Size = 16;
            row += 2;

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
                baseOffset = UnitUtils.ConvertFromInternalUnits(offsetParam.AsDouble(), UnitTypeId.Millimeters);

            AddInfoRow(sheet, ref row, "Generated:", DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss"));
            AddInfoRow(sheet, ref row, "Revit Document:", roof.Document.Title);
            AddInfoRow(sheet, ref row, "Roof ElementId:", roof.Id.Value.ToString());
            AddInfoRow(sheet, ref row, "Roof Type Name:", roofType);
            AddInfoRow(sheet, ref row, "Base Level:", baseLevelName);
            AddInfoRow(sheet, ref row, "Base Offset (mm):", Math.Round(baseOffset, 0).ToString());
            AddInfoRow(sheet, ref row, "Slope Percentage:", $"{slopePercent}%");
            AddInfoRow(sheet, ref row, "Threshold Distance:", $"{payload.ThresholdMeters} meters");
            AddInfoRow(sheet, ref row, "Drain Tolerance Enabled:", payload.EnableDrainTolerance ? "Yes" : "No");
            if (payload.EnableDrainTolerance)
                AddInfoRow(sheet, ref row, "Drain Tolerance Radius:", $"{payload.DrainToleranceMm} mm");
            AddInfoRow(sheet, ref row, "Total Vertices:", vertexData.Count.ToString());
            AddInfoRow(sheet, ref row, "Processed Vertices:", vertexData.Count(v => v.WasProcessed).ToString());
            AddInfoRow(sheet, ref row, "Skipped Vertices:", vertexData.Count(v => !v.WasProcessed).ToString());
            AddInfoRow(sheet, ref row, "Drain Points Count:", drainPoints.Count.ToString());

            // NEW: show count of vertices where Revit adjusted the elevation
            int adjustedCount = vertexData.Count(v => v.WasProcessed && Math.Round(v.ElevationDiff_mm, 0) != 0);
            AddInfoRow(sheet, ref row, "Vertices adjusted by Revit:", adjustedCount.ToString());

            sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
        }

        private static void FillDrainPointsSheet(ExcelWorksheet sheet, List<XYZ> drainPoints)
        {
            string[] hdrs = { "DrainIndex", "X (m)", "Y (m)", "Z (m)" };
            for (int i = 0; i < hdrs.Length; i++)
            {
                sheet.Cells[1, i + 1].Value = hdrs[i];
                sheet.Cells[1, i + 1].Style.Font.Bold = true;
                sheet.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                sheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(DrawingColor.LightGray);
            }

            for (int i = 0; i < drainPoints.Count; i++)
            {
                var p = drainPoints[i];
                sheet.Cells[i + 2, 1].Value = i;
                sheet.Cells[i + 2, 2].Value = Math.Round(p.X, 3);
                sheet.Cells[i + 2, 3].Value = Math.Round(p.Y, 3);
                sheet.Cells[i + 2, 4].Value = Math.Round(p.Z, 3);
            }

            sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
        }

        private static void FillVerticesSheet(
            ExcelWorksheet sheet, List<VertexData> vertexData,
            RoofBase roof, double slopePercent)
        {
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
                baseOffset = UnitUtils.ConvertFromInternalUnits(offsetParam.AsDouble(), UnitTypeId.Millimeters);

            // NEW: ElevationOffset_mm replaced by ElevCalc_mm / ElevModel_mm / ElevDiff_mm
            string[] headers =
            {
                "RoofElementId", "RoofTypeName", "BaseLevel", "BaseOffset_mm",
                "PathLength_Meters", "SlopePercent",
                "ElevCalc_mm", "ElevModel_mm", "ElevDiff_mm",
                "WasProcessed", "Position_X", "Position_Y", "Position_Z"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cells[1, i + 1].Value = headers[i];
                sheet.Cells[1, i + 1].Style.Font.Bold = true;
                sheet.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                sheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(DrawingColor.LightGray);
            }

            // Colour new elevation header cells
            sheet.Cells[1, 7].Style.Fill.BackgroundColor.SetColor(DrawingColor.LightGreen);
            sheet.Cells[1, 8].Style.Fill.BackgroundColor.SetColor(DrawingColor.LightSkyBlue);
            sheet.Cells[1, 9].Style.Fill.BackgroundColor.SetColor(DrawingColor.LightYellow);

            var sorted = vertexData
                .Where(v => v.WasProcessed).OrderByDescending(v => v.PathLengthMeters)
                .Concat(vertexData.Where(v => !v.WasProcessed))
                .ToList();

            int row = 2;
            foreach (var v in sorted)
            {
                sheet.Cells[row, 1].Value = roof.Id.Value;
                sheet.Cells[row, 2].Value = roofType;
                sheet.Cells[row, 3].Value = baseLevelName;
                sheet.Cells[row, 4].Value = Math.Round(baseOffset, 0);
                sheet.Cells[row, 5].Value = Math.Round(v.PathLengthMeters, 2);
                sheet.Cells[row, 6].Value = slopePercent;
                sheet.Cells[row, 7].Value = Math.Round(v.ElevationOffsetMm, 0);
                sheet.Cells[row, 8].Value = Math.Round(v.ElevationFromModel_mm, 0);

                double diff = Math.Round(v.ElevationDiff_mm, 0);
                sheet.Cells[row, 9].Value = diff;
                sheet.Cells[row, 9].Style.Fill.PatternType = ExcelFillStyle.Solid;
                sheet.Cells[row, 9].Style.Fill.BackgroundColor.SetColor(
                    diff == 0 ? DrawingColor.LightGreen : DrawingColor.Orange);

                sheet.Cells[row, 10].Value = v.WasProcessed ? "YES" : "NO";
                sheet.Cells[row, 11].Value = Math.Round(v.Position.X, 3);
                sheet.Cells[row, 12].Value = Math.Round(v.Position.Y, 3);
                sheet.Cells[row, 13].Value = Math.Round(v.Position.Z, 3);

                if (!v.WasProcessed)
                {
                    sheet.Cells[row, 1, row, 13].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    sheet.Cells[row, 1, row, 13].Style.Fill.BackgroundColor.SetColor(DrawingColor.LightYellow);
                }
                row++;
            }

            sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
        }

        private static void FillStatisticsSheet(ExcelWorksheet sheet, List<VertexData> vertexData)
        {
            // NEW: four columns — Metric, Calculated (ElevCalc), Model (ElevModel), Unit
            string[] hdrs = { "Metric", "ElevCalc_mm", "ElevModel_mm", "Unit" };
            for (int i = 0; i < hdrs.Length; i++)
            {
                sheet.Cells[1, i + 1].Value = hdrs[i];
                sheet.Cells[1, i + 1].Style.Font.Bold = true;
                sheet.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                sheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(DrawingColor.LightGray);
            }
            sheet.Cells[1, 2].Style.Fill.BackgroundColor.SetColor(DrawingColor.LightGreen);
            sheet.Cells[1, 3].Style.Fill.BackgroundColor.SetColor(DrawingColor.LightSkyBlue);

            var processed = vertexData.Where(v => v.WasProcessed).ToList();
            int row = 2;

            AddStatRow(sheet, ref row, "Total Vertices", vertexData.Count, null, "count");
            AddStatRow(sheet, ref row, "Processed Vertices", processed.Count, null, "count");
            AddStatRow(sheet, ref row, "Skipped Vertices", vertexData.Count(v => !v.WasProcessed), null, "count");

            int adjustedCount = processed.Count(v => Math.Round(v.ElevationDiff_mm, 0) != 0);
            AddStatRow(sheet, ref row, "Vertices adjusted by Revit", adjustedCount, null, "count");

            if (processed.Any())
            {
                row++; // blank separator

                AddStatRow(sheet, ref row, "Average Path Length",
                    Math.Round(processed.Average(v => v.PathLengthMeters), 2), null, "meters");
                AddStatRow(sheet, ref row, "Maximum Path Length",
                    Math.Round(processed.Max(v => v.PathLengthMeters), 2), null, "meters");
                AddStatRow(sheet, ref row, "Minimum Path Length",
                    Math.Round(processed.Min(v => v.PathLengthMeters), 2), null, "meters");

                row++; // blank separator

                // NEW: show both calc and model values side by side
                AddStatRow(sheet, ref row, "Average Elevation Offset",
                    Math.Round(processed.Average(v => v.ElevationOffsetMm), 0),
                    Math.Round(processed.Average(v => v.ElevationFromModel_mm), 0), "mm");
                AddStatRow(sheet, ref row, "Maximum Elevation Offset",
                    Math.Round(processed.Max(v => v.ElevationOffsetMm), 0),
                    Math.Round(processed.Max(v => v.ElevationFromModel_mm), 0), "mm");
                AddStatRow(sheet, ref row, "Minimum Elevation Offset",
                    Math.Round(processed.Min(v => v.ElevationOffsetMm), 0),
                    Math.Round(processed.Min(v => v.ElevationFromModel_mm), 0), "mm");
                AddStatRow(sheet, ref row, "Elevation Offset Range",
                    Math.Round(processed.Max(v => v.ElevationOffsetMm)
                             - processed.Min(v => v.ElevationOffsetMm), 0),
                    Math.Round(processed.Max(v => v.ElevationFromModel_mm)
                             - processed.Min(v => v.ElevationFromModel_mm), 0), "mm");
            }

            sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
        }

        // ── Row helpers ──────────────────────────────────────────────────────

        private static void AddRow(ExcelWorksheet sheet, ref int row, string label, object value)
        {
            sheet.Cells[row, 1].Value = label;
            sheet.Cells[row, 1].Style.Font.Bold = true;
            sheet.Cells[row, 2].Value = value;
            row++;
        }

        private static void AddInfoRow(ExcelWorksheet sheet, ref int row, string label, string value)
        {
            sheet.Cells[row, 1].Value = label;
            sheet.Cells[row, 1].Style.Font.Bold = true;
            sheet.Cells[row, 2].Value = value;
            row++;
        }

        // Updated to accept both calc and model values (model is nullable for non-elevation rows)
        private static void AddStatRow(ExcelWorksheet sheet, ref int row,
            string metric, object calcValue, object modelValue, string unit)
        {
            sheet.Cells[row, 1].Value = metric;
            sheet.Cells[row, 2].Value = calcValue;
            if (modelValue != null)
                sheet.Cells[row, 3].Value = modelValue;
            sheet.Cells[row, 4].Value = unit;
            row++;
        }

        // ── Fix #8: iterative unique-file-path — no unbounded recursion ──────
        private static string GetUniqueFilePath(string path)
        {
            if (!File.Exists(path)) return path;

            string dir = Path.GetDirectoryName(path) ?? string.Empty;
            string stem = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);

            // Strip any existing _NN suffix so we always start from the base name
            var match = Regex.Match(stem, @"^(.*?)_(\d{2})$");
            string baseStem = match.Success ? match.Groups[1].Value : stem;

            for (int i = 1; i <= 99; i++)
            {
                string candidate = Path.Combine(dir, $"{baseStem}_{i:D2}{ext}");
                if (!File.Exists(candidate)) return candidate;
            }

            // Fallback: use a time-based suffix — virtually guaranteed to be unique
            return Path.Combine(dir, $"{baseStem}_{DateTime.Now:HHmmss}{ext}");
        }
    }
}
