// Services/Implementations/ExcelExportService.cs
using OfficeOpenXml;
using OfficeOpenXml.Style;
using Revit26_Plugin.AutoSlopeByDrain_21.Models;
using Revit26_Plugin.AutoSlopeByDrain_21.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByDrain_21.Services.Implementations
{
    public class ExcelExportService : IExcelExportService
    {
        public ExcelExportService()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        public string ExportToExcel(
            string folderPath,
            SlopeResult slopeResult,
            List<DrainItem> drains,
            bool includeVertexDetails,
            string roofName)
        {
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"AutoSlope_{roofName}_{timestamp}.xlsx";
            string fullPath = Path.Combine(folderPath, fileName);

            using (var package = new ExcelPackage())
            {
                // Summary sheet
                var summarySheet = package.Workbook.Worksheets.Add("Summary");
                FillSummarySheet(summarySheet, slopeResult, drains, roofName);

                // Drains sheet
                var drainsSheet = package.Workbook.Worksheets.Add("Drains");
                FillDrainsSheet(drainsSheet, drains);

                // Vertex details (if requested)
                if (includeVertexDetails)
                {
                    var verticesSheet = package.Workbook.Worksheets.Add("Vertices");
                    FillVerticesSheet(verticesSheet, slopeResult); // You would need actual vertex data
                }

                package.SaveAs(new FileInfo(fullPath));
            }
            return fullPath;
        }

        private void FillSummarySheet(ExcelWorksheet sheet, SlopeResult result, List<DrainItem> drains, string roofName)
        {
            int row = 1;
            sheet.Cells[row, 1].Value = "AutoSlope Export Report";
            sheet.Cells[row, 1, row, 2].Merge = true;
            sheet.Cells[row, 1].Style.Font.Bold = true;
            sheet.Cells[row, 1].Style.Font.Size = 14;
            row += 2;

            AddRow(sheet, ref row, "Roof Name", roofName);
            AddRow(sheet, ref row, "Export Date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            AddRow(sheet, ref row, "Slope Percentage", $"{result.SlopePercent1}%");
            AddRow(sheet, ref row, "Vertices Processed", result.VerticesProcessed);
            AddRow(sheet, ref row, "Vertices Skipped", result.VerticesSkipped);
            AddRow(sheet, ref row, "Highest Elevation (mm)", $"{result.HighestElevationMm:F1}");
            AddRow(sheet, ref row, "Longest Path (m)", $"{result.LongestPathMeters:F2}");
            AddRow(sheet, ref row, "Run Duration (sec)", $"{result.RunDuration_sec:F1}");
            AddRow(sheet, ref row, "Drains Count", drains.Count(d => d.IsSelected));
            sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
        }

        private void FillDrainsSheet(ExcelWorksheet sheet, List<DrainItem> drains)
        {
            string[] headers = { "ID", "Shape", "Size (mm)", "Width (mm)", "Height (mm)", "Center X (mm)", "Center Y (mm)", "Selected" };
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cells[1, i + 1].Value = headers[i];
                sheet.Cells[1, i + 1].Style.Font.Bold = true;
                sheet.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                sheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(Color.LightGray);
            }

            int row = 2;
            foreach (var d in drains)
            {
                sheet.Cells[row, 1].Value = d.DrainId;
                sheet.Cells[row, 2].Value = d.ShapeType;
                sheet.Cells[row, 3].Value = d.SizeCategory;
                sheet.Cells[row, 4].Value = d.Width;
                sheet.Cells[row, 5].Value = d.Height;
                sheet.Cells[row, 6].Value = d.CenterPoint.X;
                sheet.Cells[row, 7].Value = d.CenterPoint.Y;
                sheet.Cells[row, 8].Value = d.IsSelected ? "Yes" : "No";
                row++;
            }
            sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
        }

        private void FillVerticesSheet(ExcelWorksheet sheet, SlopeResult result)
        {
            // Placeholder: you would need actual vertex data from the slope processor
            sheet.Cells[1, 1].Value = "Vertex data not collected in this version.";
            sheet.Cells[1, 1].Style.Font.Italic = true;
        }

        private void AddRow(ExcelWorksheet sheet, ref int row, string label, object value)
        {
            sheet.Cells[row, 1].Value = label;
            sheet.Cells[row, 1].Style.Font.Bold = true;
            sheet.Cells[row, 2].Value = value;
            row++;
        }
    }
}