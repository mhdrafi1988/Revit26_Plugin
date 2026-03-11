using Autodesk.Revit.DB;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using Revit26_Plugin.AutoSlopeByPoint_04.Core.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByPoint_04.Infrastructure.Helpers
{
    public static class ExcelExportHelper
    {
        // Static constructor to set license context once
        static ExcelExportHelper()
        {
            // Fully qualified to avoid ambiguity
            OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
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
                string filePath = Path.Combine(payload.ExportConfig.ExportPath, fileName);

                using (var package = new ExcelPackage())
                {
                    // Summary Sheet
                    var summarySheet = package.Workbook.Worksheets.Add("Summary");
                    FillSummarySheet(summarySheet, payload, vertexData, roof, drainPoints, slopePercent);

                    // Drain Points Sheet
                    var drainSheet = package.Workbook.Worksheets.Add("Drain Points");
                    FillDrainPointsSheet(drainSheet, drainPoints);

                    // Vertices Sheet
                    var verticesSheet = package.Workbook.Worksheets.Add("Vertices");
                    FillVerticesSheet(verticesSheet, vertexData, roof, slopePercent);

                    // Statistics Sheet
                    var statsSheet = package.Workbook.Worksheets.Add("Statistics");
                    FillStatisticsSheet(statsSheet, vertexData);

                    // Save the file
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
                string slopeStr = slopePercent.ToString("0.00", CultureInfo.InvariantCulture);
                string dateStr = DateTime.Now.ToString("dd-MM-yy");
                string fileName = $"{roofId}_{slopeStr}_{dateStr}.xlsx";
                string filePath = Path.Combine(payload.ExportConfig.ExportPath, fileName);

                using (var package = new ExcelPackage())
                {
                    var sheet = package.Workbook.Worksheets.Add("AutoSlope Data");

                    // Title
                    sheet.Cells["A1"].Value = "AUTOSLOPE COMPACT VERTEX EXPORT";
                    sheet.Cells["A1:G1"].Merge = true;
                    sheet.Cells["A1"].Style.Font.Bold = true;
                    sheet.Cells["A1"].Style.Font.Size = 14;
                    sheet.Cells["A1"].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    sheet.Cells["A1"].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue); // <-- Fixed

                    // Headers
                    string[] headers = { "RoofElementId", "DrainElementId", "PointIndex",
                                       "PathLength_Meters", "SlopePercent", "ElevationOffset_mm", "Direction" };

                    for (int i = 0; i < headers.Length; i++)
                    {
                        sheet.Cells[3, i + 1].Value = headers[i];
                        sheet.Cells[3, i + 1].Style.Font.Bold = true;
                        sheet.Cells[3, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        sheet.Cells[3, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray); // <-- Fully qualify
                    }

                    // Data
                    var sortedVertices = vertexData
                        .Where(v => v.WasProcessed)
                        .OrderByDescending(v => v.PathLengthMeters)
                        .ToList();

                    int row = 4;
                    foreach (var vertex in sortedVertices)
                    {
                        sheet.Cells[row, 1].Value = roof.Id.Value;
                        sheet.Cells[row, 2].Value = vertex.NearestDrainIndex;
                        sheet.Cells[row, 3].Value = vertex.VertexIndex;
                        sheet.Cells[row, 4].Value = Math.Round(vertex.PathLengthMeters, 2);
                        sheet.Cells[row, 5].Value = slopePercent;
                        sheet.Cells[row, 6].Value = vertex.ElevationOffsetMm;
                        sheet.Cells[row, 7].Value = vertex.Direction;
                        row++;
                    }

                    // Summary
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
                    }

                    // Auto-fit columns
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

        private static void FillSummarySheet(ExcelWorksheet sheet, AutoSlopePayload payload,
            List<VertexData> vertexData, RoofBase roof, List<XYZ> drainPoints, double slopePercent)
        {
            int row = 1;

            // Title
            sheet.Cells[row, 1].Value = "AUTOSLOPE DETAILED VERTEX EXPORT";
            sheet.Cells[row, 1, row, 4].Merge = true;
            sheet.Cells[row, 1].Style.Font.Bold = true;
            sheet.Cells[row, 1].Style.Font.Size = 16;

            row += 2;

            // Metadata
            AddInfoRow(sheet, ref row, "Generated:", DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss"));
            AddInfoRow(sheet, ref row, "Revit Document:", roof.Document.Title);
            AddInfoRow(sheet, ref row, "Roof ElementId:", roof.Id.Value.ToString());
            AddInfoRow(sheet, ref row, "Slope Percentage:", $"{slopePercent}%");
            AddInfoRow(sheet, ref row, "Threshold Distance:", $"{payload.ThresholdMeters} meters");
            AddInfoRow(sheet, ref row, "Total Vertices:", vertexData.Count.ToString());
            AddInfoRow(sheet, ref row, "Processed Vertices:", vertexData.Count(v => v.WasProcessed).ToString());
            AddInfoRow(sheet, ref row, "Skipped Vertices:", vertexData.Count(v => !v.WasProcessed).ToString());
            AddInfoRow(sheet, ref row, "Drain Points Count:", drainPoints.Count.ToString());

            sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
        }

        private static void FillDrainPointsSheet(ExcelWorksheet sheet, List<XYZ> drainPoints)
        {
            sheet.Cells[1, 1].Value = "DrainIndex";
            sheet.Cells[1, 2].Value = "X (m)";
            sheet.Cells[1, 3].Value = "Y (m)";
            sheet.Cells[1, 4].Value = "Z (m)";

            // Style headers
            for (int i = 1; i <= 4; i++)
            {
                sheet.Cells[1, i].Style.Font.Bold = true;
                sheet.Cells[1, i].Style.Fill.PatternType = ExcelFillStyle.Solid;
                sheet.Cells[1, i].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
            }

            for (int i = 0; i < drainPoints.Count; i++)
            {
                var point = drainPoints[i];
                sheet.Cells[i + 2, 1].Value = i;
                sheet.Cells[i + 2, 2].Value = Math.Round(point.X, 3);
                sheet.Cells[i + 2, 3].Value = Math.Round(point.Y, 3);
                sheet.Cells[i + 2, 4].Value = Math.Round(point.Z, 3);
            }

            sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
        }

        private static void FillVerticesSheet(ExcelWorksheet sheet, List<VertexData> vertexData,
            RoofBase roof, double slopePercent)
        {
            string[] headers = { "RoofElementId", "DrainElementId", "PointIndex", "PathLength_Meters",
                               "SlopePercent", "ElevationOffset_mm", "Direction", "WasProcessed",
                               "Position_X", "Position_Y", "Position_Z" };

            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cells[1, i + 1].Value = headers[i];
                sheet.Cells[1, i + 1].Style.Font.Bold = true;
                sheet.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                sheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
            }

            var sortedVertices = vertexData
                .Where(v => v.WasProcessed)
                .OrderByDescending(v => v.PathLengthMeters)
                .Concat(vertexData.Where(v => !v.WasProcessed))
                .ToList();

            int row = 2;
            foreach (var vertex in sortedVertices)
            {
                sheet.Cells[row, 1].Value = roof.Id.Value;
                sheet.Cells[row, 2].Value = vertex.NearestDrainIndex;
                sheet.Cells[row, 3].Value = vertex.VertexIndex;
                sheet.Cells[row, 4].Value = Math.Round(vertex.PathLengthMeters, 2);
                sheet.Cells[row, 5].Value = slopePercent;
                sheet.Cells[row, 6].Value = vertex.ElevationOffsetMm;
                sheet.Cells[row, 7].Value = vertex.Direction;
                sheet.Cells[row, 8].Value = vertex.WasProcessed ? "YES" : "NO";
                sheet.Cells[row, 9].Value = Math.Round(vertex.Position.X, 3);
                sheet.Cells[row, 10].Value = Math.Round(vertex.Position.Y, 3);
                sheet.Cells[row, 11].Value = Math.Round(vertex.Position.Z, 3);

                // Color rows based on WasProcessed
                if (!vertex.WasProcessed)
                {
                    sheet.Cells[row, 1, row, 11].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    sheet.Cells[row, 1, row, 11].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightYellow);
                }

                row++;
            }

            sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
        }

        private static void FillStatisticsSheet(ExcelWorksheet sheet, List<VertexData> vertexData)
        {
            sheet.Cells[1, 1].Value = "Metric";
            sheet.Cells[1, 2].Value = "Value";
            sheet.Cells[1, 3].Value = "Unit";

            for (int i = 1; i <= 3; i++)
            {
                sheet.Cells[1, i].Style.Font.Bold = true;
                sheet.Cells[1, i].Style.Fill.PatternType = ExcelFillStyle.Solid;
                sheet.Cells[1, i].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
            }

            var processed = vertexData.Where(v => v.WasProcessed).ToList();

            int row = 2;
            AddStatRow(sheet, ref row, "Total Vertices", vertexData.Count, "count");
            AddStatRow(sheet, ref row, "Processed Vertices", processed.Count, "count");
            AddStatRow(sheet, ref row, "Skipped Vertices", vertexData.Count(v => !v.WasProcessed), "count");

            if (processed.Any())
            {
                AddStatRow(sheet, ref row, "Average Path Length",
                    Math.Round(processed.Average(v => v.PathLengthMeters), 2), "meters");
                AddStatRow(sheet, ref row, "Maximum Path Length",
                    Math.Round(processed.Max(v => v.PathLengthMeters), 2), "meters");
                AddStatRow(sheet, ref row, "Minimum Path Length",
                    Math.Round(processed.Min(v => v.PathLengthMeters), 2), "meters");
                AddStatRow(sheet, ref row, "Average Elevation Offset",
                    Math.Round(processed.Average(v => v.ElevationOffsetMm), 0), "mm");
                AddStatRow(sheet, ref row, "Maximum Elevation Offset",
                    Math.Round(processed.Max(v => v.ElevationOffsetMm), 0), "mm");
            }

            sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
        }

        private static void AddInfoRow(ExcelWorksheet sheet, ref int row, string label, string value)
        {
            sheet.Cells[row, 1].Value = label;
            sheet.Cells[row, 1].Style.Font.Bold = true;
            sheet.Cells[row, 2].Value = value;
            row++;
        }

        private static void AddStatRow(ExcelWorksheet sheet, ref int row, string metric, object value, string unit)
        {
            sheet.Cells[row, 1].Value = metric;
            sheet.Cells[row, 2].Value = value;
            sheet.Cells[row, 3].Value = unit;
            row++;
        }

        public static string ExportSummaryOnly(AutoSlopePayload payload, AutoSlopeMetrics metrics)
        {
            if (payload?.ExportConfig == null)
                return null;

            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"{payload.ExportConfig.FileNamePrefix}_Summary_{timestamp}.xlsx";
                string filePath = Path.Combine(payload.ExportConfig.ExportPath, fileName);

                string projectName = payload.Vm?.UIDoc?.Document?.Title ?? "Unknown Project";

                using (var package = new ExcelPackage())
                {
                    var sheet = package.Workbook.Worksheets.Add("Summary");

                    sheet.Cells[1, 1].Value = "Parameter";
                    sheet.Cells[1, 2].Value = "Value";
                    sheet.Cells[1, 3].Value = "Unit";

                    for (int i = 1; i <= 3; i++)
                    {
                        sheet.Cells[1, i].Style.Font.Bold = true;
                        sheet.Cells[1, i].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        sheet.Cells[1, i].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    }

                    int row = 2;
                    AddStatRow(sheet, ref row, "Project Name", projectName, "");
                    AddStatRow(sheet, ref row, "Roof ElementId", payload.RoofId.Value, "");
                    AddStatRow(sheet, ref row, "Run Date", DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss"), "");
                    AddStatRow(sheet, ref row, "Slope Percentage", payload.SlopePercent, "%");
                    AddStatRow(sheet, ref row, "Threshold Distance", payload.ThresholdMeters, "meters");
                    AddStatRow(sheet, ref row, "Vertices Processed", metrics.Processed, "count");
                    AddStatRow(sheet, ref row, "Vertices Skipped", metrics.Skipped, "count");
                    AddStatRow(sheet, ref row, "Drain Points", payload.DrainPoints.Count, "count");
                    AddStatRow(sheet, ref row, "Highest Elevation", Math.Round(metrics.HighestElevation, 0), "mm");
                    AddStatRow(sheet, ref row, "Longest Path", Math.Round(metrics.LongestPath, 2), "meters");

                    sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
                    package.SaveAs(new FileInfo(filePath));
                }

                return filePath;
            }
            catch (Exception ex)
            {
                payload?.Log?.Invoke($"Summary Export Error: {ex.Message}");
                return null;
            }
        }
    }
}