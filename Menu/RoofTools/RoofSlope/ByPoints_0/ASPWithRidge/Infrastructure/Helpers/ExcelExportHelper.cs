// =======================================================
// File: ExcelExportHelper.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.WithRidge
// Changes vs previous version:
//   + Ridge columns in both compact and Vertices sheet:
//       IsRidge      — YES/NO
//       RidgeDrainA  — drain index A (-1 if not ridge)
//       RidgeDrainB  — drain index B (-1 if not ridge)
//       RidgePathA_m — path length to drain A
//       RidgePathB_m — path length to drain B
//   + Ridge rows highlighted in purple in both sheets.
//   + ElevCalc_mm / ElevModel_mm / ElevDiff_mm columns
//     retained from previous update.
//   + Statistics sheet shows ridge count.
//   + Summary sheet shows ridge count and ratio tolerance.
// =======================================================

using Autodesk.Revit.DB;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using Revit26_Plugin.AutoSlopeByPoint.WithRidge.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DrawingColor = System.Drawing.Color;

namespace Revit26_Plugin.AutoSlopeByPoint.WithRidge.Infrastructure.Helpers
{
    public static class ExcelExportHelper
    {
        static ExcelExportHelper()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        // ── Public: compact export ───────────────────────────────────────────

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
                string roofId       = roof.Id.Value.ToString();
                string roofType     = roof.Name ?? "Unknown";
                string baseLevelName = "Unknown";
                double baseOffset   = 0;

                if (roof.LevelId != null && roof.LevelId != ElementId.InvalidElementId)
                {
                    Level level = roof.Document.GetElement(roof.LevelId) as Level;
                    baseLevelName = level?.Name ?? "Unknown";
                }

                Parameter offsetParam = roof.get_Parameter(BuiltInParameter.ROOF_CONSTRAINT_OFFSET_PARAM);
                if (offsetParam != null && offsetParam.HasValue)
                    baseOffset = UnitUtils.ConvertFromInternalUnits(offsetParam.AsDouble(), UnitTypeId.Millimeters);

                string slopeStr = slopePercent.ToString("0.00", CultureInfo.InvariantCulture);
                string dateStr  = DateTime.Now.ToString("dd-MM-yy");
                string fileName = $"{roofId}_{slopeStr}_{dateStr}.xlsx";
                string filePath = GetUniqueFilePath(Path.Combine(payload.ExportConfig.ExportPath, fileName));

                using (var package = new ExcelPackage())
                {
                    var sheet = package.Workbook.Worksheets.Add("AutoSlope Data");

                    sheet.Cells["A1"].Value = "AUTOSLOPE COMPACT VERTEX EXPORT";
                    sheet.Cells["A1:N1"].Merge = true;
                    sheet.Cells["A1"].Style.Font.Bold = true;
                    sheet.Cells["A1"].Style.Font.Size = 14;
                    sheet.Cells["A1"].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    sheet.Cells["A1"].Style.Fill.BackgroundColor.SetColor(DrawingColor.LightBlue);

                    string[] headers =
                    {
                        // cols 1-6: roof/path info
                        "RoofElementId", "RoofTypeName", "BaseLevel", "BaseOffset_mm",
                        "PathLength_Meters", "SlopePercent",
                        // cols 7-9: elevation triple
                        "ElevCalc_mm", "ElevModel_mm", "ElevDiff_mm",
                        // cols 10-14: ridge info
                        "IsRidge", "RidgeDrainA", "RidgeDrainB", "RidgePathA_m", "RidgePathB_m"
                    };

                    WriteHeaders(sheet, 3, headers, new Dictionary<int, DrawingColor>
                    {
                        { 7,  DrawingColor.LightGreen },
                        { 8,  DrawingColor.LightSkyBlue },
                        { 9,  DrawingColor.LightYellow },
                        { 10, DrawingColor.Plum },
                        { 11, DrawingColor.Plum },
                        { 12, DrawingColor.Plum },
                        { 13, DrawingColor.Plum },
                        { 14, DrawingColor.Plum }
                    });

                    var sortedVertices = vertexData
                        .Where(v => v.WasProcessed)
                        .OrderByDescending(v => v.IsRidgePoint)   // ridges first
                        .ThenByDescending(v => v.PathLengthMeters)
                        .ToList();

                    int row = 4;
                    foreach (var v in sortedVertices)
                    {
                        sheet.Cells[row, 1].Value  = roof.Id.Value;
                        sheet.Cells[row, 2].Value  = roofType;
                        sheet.Cells[row, 3].Value  = baseLevelName;
                        sheet.Cells[row, 4].Value  = Math.Round(baseOffset, 0);
                        sheet.Cells[row, 5].Value  = Math.Round(v.PathLengthMeters, 2);
                        sheet.Cells[row, 6].Value  = slopePercent;
                        sheet.Cells[row, 7].Value  = Math.Round(v.ElevationOffsetMm, 0);
                        sheet.Cells[row, 8].Value  = Math.Round(v.ElevationFromModel_mm, 0);

                        double diff = Math.Round(v.ElevationDiff_mm, 0);
                        sheet.Cells[row, 9].Value  = diff;
                        SetDiffColor(sheet.Cells[row, 9], diff);

                        sheet.Cells[row, 10].Value = v.IsRidgePoint ? "YES" : "NO";
                        sheet.Cells[row, 11].Value = v.RidgeDrainA;
                        sheet.Cells[row, 12].Value = v.RidgeDrainB;
                        sheet.Cells[row, 13].Value = v.RidgePathA_m > 0 ? Math.Round(v.RidgePathA_m, 2) : (object)"";
                        sheet.Cells[row, 14].Value = v.RidgePathB_m > 0 ? Math.Round(v.RidgePathB_m, 2) : (object)"";

                        if (v.IsRidgePoint)
                            HighlightRow(sheet, row, 1, 14, DrawingColor.FromArgb(220, 200, 255));  // light purple

                        row++;
                    }

                    // Summary block
                    row += 2;
                    int ridgeCount = sortedVertices.Count(v => v.IsRidgePoint);
                    WriteSummaryBlock(sheet, row, sortedVertices.Count, ridgeCount,
                        sortedVertices.FirstOrDefault()?.PathLengthMeters ?? 0,
                        sortedVertices.LastOrDefault(v => v.WasProcessed)?.PathLengthMeters ?? 0);

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

        // ── Public: detailed export ──────────────────────────────────────────

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
                string roofId   = roof.Id.Value.ToString();
                string slopeStr = slopePercent.ToString("0.00", CultureInfo.InvariantCulture);
                string dateStr  = DateTime.Now.ToString("dd-MM-yy");
                string fileName = $"{roofId}_{slopeStr}_{dateStr}_DETAILED.xlsx";
                string filePath = GetUniqueFilePath(Path.Combine(payload.ExportConfig.ExportPath, fileName));

                using (var package = new ExcelPackage())
                {
                    FillSummarySheet(
                        package.Workbook.Worksheets.Add("Summary"),
                        payload, vertexData, roof, drainPoints, slopePercent);

                    FillDrainPointsSheet(
                        package.Workbook.Worksheets.Add("Drain Points"),
                        drainPoints);

                    FillVerticesSheet(
                        package.Workbook.Worksheets.Add("Vertices"),
                        vertexData, roof, slopePercent);

                    FillStatisticsSheet(
                        package.Workbook.Worksheets.Add("Statistics"),
                        vertexData);

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

        // ── Summary export (results only) ────────────────────────────────────

        public static string ExportResultsSummary(
            string filePath,
            AutoSlopeResult result,
            double slopePercent,
            int thresholdMeters,
            bool enableDrainTolerance,
            int drainToleranceMm,
            bool ridgeDetectionEnabled,
            int drainGroupRadiusMm,
            int ridgeLineToleranceMm,
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

                    AddRow(sheet, ref row, "Vertices Processed",      result.VerticesProcessed);
                    AddRow(sheet, ref row, "Vertices Skipped",         result.VerticesSkipped);
                    AddRow(sheet, ref row, "Ridge Points Detected",    result.RidgePointsDetected);
                    AddRow(sheet, ref row, "Final Drain Count",        result.FinalDrainCount);
                    AddRow(sheet, ref row, "Highest Elevation (mm)",   $"{result.HighestElevation_mm:0}");
                    AddRow(sheet, ref row, "Longest Path (m)",         $"{result.LongestPath_m:0.00}");
                    AddRow(sheet, ref row, "Run Duration (sec)",       result.RunDuration_sec);
                    AddRow(sheet, ref row, "Run Date",                 result.RunDate);
                    AddRow(sheet, ref row, "Slope Percentage",         $"{slopePercent}%");
                    AddRow(sheet, ref row, "Threshold (m)",            thresholdMeters);
                    AddRow(sheet, ref row, "Drain Tolerance Enabled",  enableDrainTolerance ? "Yes" : "No");
                    AddRow(sheet, ref row, "Drain Tolerance (mm)",     enableDrainTolerance ? drainToleranceMm.ToString() : "N/A");
                    AddRow(sheet, ref row, "Ridge Detection Enabled",  ridgeDetectionEnabled ? "Yes" : "No");
                    AddRow(sheet, ref row, "Drain Group Radius (mm)",  ridgeDetectionEnabled ? drainGroupRadiusMm.ToString() : "N/A");
                    AddRow(sheet, ref row, "Ridge Line Tolerance (mm)", ridgeDetectionEnabled ? ridgeLineToleranceMm.ToString() : "N/A");
                    AddRow(sheet, ref row, "Export Folder",            exportFolderPath);

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

        // ── Private sheet fillers ────────────────────────────────────────────

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

            string baseLevelName = "Unknown";
            double baseOffset    = 0;

            if (roof.LevelId != null && roof.LevelId != ElementId.InvalidElementId)
            {
                Level level = roof.Document.GetElement(roof.LevelId) as Level;
                baseLevelName = level?.Name ?? "Unknown";
            }

            Parameter offsetParam = roof.get_Parameter(BuiltInParameter.ROOF_CONSTRAINT_OFFSET_PARAM);
            if (offsetParam != null && offsetParam.HasValue)
                baseOffset = UnitUtils.ConvertFromInternalUnits(offsetParam.AsDouble(), UnitTypeId.Millimeters);

            AddInfoRow(sheet, ref row, "Generated:",              DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss"));
            AddInfoRow(sheet, ref row, "Revit Document:",         roof.Document.Title);
            AddInfoRow(sheet, ref row, "Roof ElementId:",         roof.Id.Value.ToString());
            AddInfoRow(sheet, ref row, "Roof Type Name:",         roof.Name ?? "Unknown");
            AddInfoRow(sheet, ref row, "Base Level:",             baseLevelName);
            AddInfoRow(sheet, ref row, "Base Offset (mm):",       Math.Round(baseOffset, 0).ToString());
            AddInfoRow(sheet, ref row, "Slope Percentage:",       $"{slopePercent}%");
            AddInfoRow(sheet, ref row, "Threshold Distance:",     $"{payload.ThresholdMeters} meters");
            AddInfoRow(sheet, ref row, "Drain Tolerance Enabled:", payload.EnableDrainTolerance ? "Yes" : "No");
            if (payload.EnableDrainTolerance)
                AddInfoRow(sheet, ref row, "Drain Tolerance Radius:", $"{payload.DrainToleranceMm} mm");
            AddInfoRow(sheet, ref row, "Ridge Detection:",        payload.RidgeDetectionEnabled ? "Enabled" : "Disabled");
            if (payload.RidgeDetectionEnabled)
            {
                AddInfoRow(sheet, ref row, "Drain Group Radius (mm):",   $"{payload.DrainGroupRadiusMm}");
                AddInfoRow(sheet, ref row, "Ridge Line Tolerance (mm):", $"{payload.RidgeLineToleranceMm}");
            }
            AddInfoRow(sheet, ref row, "Total Vertices:",         vertexData.Count.ToString());
            AddInfoRow(sheet, ref row, "Processed Vertices:",     vertexData.Count(v => v.WasProcessed).ToString());
            AddInfoRow(sheet, ref row, "Skipped Vertices:",       vertexData.Count(v => !v.WasProcessed).ToString());
            AddInfoRow(sheet, ref row, "Ridge Points Detected:",  vertexData.Count(v => v.IsRidgePoint).ToString());
            AddInfoRow(sheet, ref row, "Drain Points Count:",     drainPoints.Count.ToString());

            int adjustedCount = vertexData.Count(v => v.WasProcessed && Math.Round(v.ElevationDiff_mm, 0) != 0);
            AddInfoRow(sheet, ref row, "Vertices adjusted by Revit:", adjustedCount.ToString());

            sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
        }

        private static void FillDrainPointsSheet(ExcelWorksheet sheet, List<XYZ> drainPoints)
        {
            string[] hdrs = { "DrainIndex", "X (m)", "Y (m)", "Z (m)" };
            WriteHeaders(sheet, 1, hdrs, null);

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
            string baseLevelName = "Unknown";
            double baseOffset    = 0;

            if (roof.LevelId != null && roof.LevelId != ElementId.InvalidElementId)
            {
                Level level = roof.Document.GetElement(roof.LevelId) as Level;
                baseLevelName = level?.Name ?? "Unknown";
            }

            Parameter offsetParam = roof.get_Parameter(BuiltInParameter.ROOF_CONSTRAINT_OFFSET_PARAM);
            if (offsetParam != null && offsetParam.HasValue)
                baseOffset = UnitUtils.ConvertFromInternalUnits(offsetParam.AsDouble(), UnitTypeId.Millimeters);

            string[] headers =
            {
                "RoofElementId", "RoofTypeName", "BaseLevel", "BaseOffset_mm",
                "PathLength_Meters", "SlopePercent",
                "ElevCalc_mm", "ElevModel_mm", "ElevDiff_mm",
                "IsRidge", "RidgeDrainA", "RidgeDrainB", "RidgePathA_m", "RidgePathB_m",
                "WasProcessed", "Position_X", "Position_Y", "Position_Z"
            };

            WriteHeaders(sheet, 1, headers, new Dictionary<int, DrawingColor>
            {
                { 7,  DrawingColor.LightGreen },
                { 8,  DrawingColor.LightSkyBlue },
                { 9,  DrawingColor.LightYellow },
                { 10, DrawingColor.Plum },
                { 11, DrawingColor.Plum },
                { 12, DrawingColor.Plum },
                { 13, DrawingColor.Plum },
                { 14, DrawingColor.Plum }
            });

            var sorted = vertexData
                .Where(v => v.WasProcessed)
                .OrderByDescending(v => v.IsRidgePoint)
                .ThenByDescending(v => v.PathLengthMeters)
                .Concat(vertexData.Where(v => !v.WasProcessed))
                .ToList();

            int row = 2;
            foreach (var v in sorted)
            {
                sheet.Cells[row, 1].Value  = roof.Id.Value;
                sheet.Cells[row, 2].Value  = roof.Name ?? "Unknown";
                sheet.Cells[row, 3].Value  = baseLevelName;
                sheet.Cells[row, 4].Value  = Math.Round(baseOffset, 0);
                sheet.Cells[row, 5].Value  = Math.Round(v.PathLengthMeters, 2);
                sheet.Cells[row, 6].Value  = slopePercent;
                sheet.Cells[row, 7].Value  = Math.Round(v.ElevationOffsetMm, 0);
                sheet.Cells[row, 8].Value  = Math.Round(v.ElevationFromModel_mm, 0);

                double diff = Math.Round(v.ElevationDiff_mm, 0);
                sheet.Cells[row, 9].Value  = diff;
                SetDiffColor(sheet.Cells[row, 9], diff);

                sheet.Cells[row, 10].Value = v.IsRidgePoint ? "YES" : "NO";
                sheet.Cells[row, 11].Value = v.RidgeDrainA;
                sheet.Cells[row, 12].Value = v.RidgeDrainB;
                sheet.Cells[row, 13].Value = v.RidgePathA_m > 0 ? Math.Round(v.RidgePathA_m, 2) : (object)"";
                sheet.Cells[row, 14].Value = v.RidgePathB_m > 0 ? Math.Round(v.RidgePathB_m, 2) : (object)"";
                sheet.Cells[row, 15].Value = v.WasProcessed ? "YES" : "NO";
                sheet.Cells[row, 16].Value = Math.Round(v.Position.X, 3);
                sheet.Cells[row, 17].Value = Math.Round(v.Position.Y, 3);
                sheet.Cells[row, 18].Value = Math.Round(v.Position.Z, 3);

                if (v.IsRidgePoint)
                    HighlightRow(sheet, row, 1, 18, DrawingColor.FromArgb(220, 200, 255));
                else if (!v.WasProcessed)
                    HighlightRow(sheet, row, 1, 18, DrawingColor.LightYellow);

                row++;
            }

            sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
        }

        private static void FillStatisticsSheet(ExcelWorksheet sheet, List<VertexData> vertexData)
        {
            string[] hdrs = { "Metric", "ElevCalc_mm", "ElevModel_mm", "Unit" };
            WriteHeaders(sheet, 1, hdrs, new Dictionary<int, DrawingColor>
            {
                { 2, DrawingColor.LightGreen },
                { 3, DrawingColor.LightSkyBlue }
            });

            var processed = vertexData.Where(v => v.WasProcessed).ToList();
            var ridges    = vertexData.Where(v => v.IsRidgePoint).ToList();
            int row = 2;

            AddStatRow(sheet, ref row, "Total Vertices",            vertexData.Count,                            null,  "count");
            AddStatRow(sheet, ref row, "Processed Vertices",        processed.Count,                             null,  "count");
            AddStatRow(sheet, ref row, "Skipped Vertices",          vertexData.Count(v => !v.WasProcessed),      null,  "count");
            AddStatRow(sheet, ref row, "Ridge Points Detected",     ridges.Count,                                null,  "count");
            AddStatRow(sheet, ref row, "Vertices adjusted by Revit",processed.Count(v => Math.Round(v.ElevationDiff_mm, 0) != 0), null, "count");

            if (processed.Any())
            {
                row++;
                AddStatRow(sheet, ref row, "Average Path Length",
                    Math.Round(processed.Average(v => v.PathLengthMeters), 2), null, "meters");
                AddStatRow(sheet, ref row, "Maximum Path Length",
                    Math.Round(processed.Max(v => v.PathLengthMeters), 2), null, "meters");
                AddStatRow(sheet, ref row, "Minimum Path Length",
                    Math.Round(processed.Min(v => v.PathLengthMeters), 2), null, "meters");

                row++;
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
                    Math.Round(processed.Max(v => v.ElevationOffsetMm) - processed.Min(v => v.ElevationOffsetMm), 0),
                    Math.Round(processed.Max(v => v.ElevationFromModel_mm) - processed.Min(v => v.ElevationFromModel_mm), 0), "mm");
            }

            sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
        }

        // ── Shared helpers ───────────────────────────────────────────────────

        private static void WriteHeaders(
            ExcelWorksheet sheet, int row, string[] headers,
            Dictionary<int, DrawingColor> colorOverrides)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = sheet.Cells[row, i + 1];
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;

                DrawingColor bg = DrawingColor.LightGray;
                if (colorOverrides != null && colorOverrides.TryGetValue(i + 1, out DrawingColor ov))
                    bg = ov;

                cell.Style.Fill.BackgroundColor.SetColor(bg);
            }
        }

        private static void HighlightRow(
            ExcelWorksheet sheet, int row, int colFrom, int colTo, DrawingColor color)
        {
            for (int c = colFrom; c <= colTo; c++)
            {
                var cell = sheet.Cells[row, c];
                // Only set background if cell doesn't already have a specific diff color
                if (c == 9) continue;   // ElevDiff_mm keeps its own color
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(color);
            }
        }

        private static void SetDiffColor(ExcelRangeBase cell, double diff)
        {
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(
                diff == 0 ? DrawingColor.LightGreen : DrawingColor.Orange);
        }

        private static void WriteSummaryBlock(
            ExcelWorksheet sheet, int row, int total, int ridgeCount,
            double longestPath, double shortestPath)
        {
            sheet.Cells[row, 1].Value = "SUMMARY";
            sheet.Cells[row, 1].Style.Font.Bold = true;
            sheet.Cells[row, 1].Style.Font.Size = 12;
            sheet.Cells[row + 1, 1].Value = "Total Processed Vertices:";
            sheet.Cells[row + 1, 2].Value = total;
            sheet.Cells[row + 2, 1].Value = "Ridge Points Detected:";
            sheet.Cells[row + 2, 2].Value = ridgeCount;
            sheet.Cells[row + 3, 1].Value = "Longest Path:";
            sheet.Cells[row + 3, 2].Value = longestPath;
            sheet.Cells[row + 3, 3].Value = "m";
            sheet.Cells[row + 4, 1].Value = "Shortest Path:";
            sheet.Cells[row + 4, 2].Value = shortestPath;
            sheet.Cells[row + 4, 3].Value = "m";
        }

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

        private static string GetUniqueFilePath(string path)
        {
            if (!File.Exists(path)) return path;

            string dir      = Path.GetDirectoryName(path) ?? string.Empty;
            string stem     = Path.GetFileNameWithoutExtension(path);
            string ext      = Path.GetExtension(path);
            var match       = Regex.Match(stem, @"^(.*?)_(\d{2})$");
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
