using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using WorksetManager_01.Models;

namespace WorksetManager_01.Helpers
{
    public static class ExcelExportHelper
    {
        public static string Export(List<WorksetSummaryItem> items, string filePath)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var package = new ExcelPackage();

            // --- Sheet 1: Summary ---
            var summarySheet = package.Workbook.Worksheets.Add("Workset Summary");
            WriteHeaders(summarySheet, new[]
            {
                "Workset Name", "Total Elements", "Status", "Editable", "Type Breakdown"
            });

            int row = 2;
            foreach (var item in items)
            {
                summarySheet.Cells[row, 1].Value = item.WorksetName;
                summarySheet.Cells[row, 2].Value = item.TotalElements;
                summarySheet.Cells[row, 3].Value = item.StatusLabel;
                summarySheet.Cells[row, 4].Value = item.EditableLabel;
                summarySheet.Cells[row, 5].Value = item.TypeBreakdown;

                // Highlight empty worksets
                if (item.TotalElements == 0)
                {
                    summarySheet.Cells[row, 1, row, 5].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    summarySheet.Cells[row, 1, row, 5].Style.Fill.BackgroundColor
                        .SetColor(Color.FromArgb(255, 255, 204, 204)); // light red
                }

                row++;
            }
            summarySheet.Cells[summarySheet.Dimension.Address].AutoFitColumns();

            // --- Sheet 2: Type Detail ---
            var detailSheet = package.Workbook.Worksheets.Add("Type Detail");
            WriteHeaders(detailSheet, new[] { "Workset Name", "Type Name", "Count" });

            row = 2;
            foreach (var item in items)
            {
                foreach (var kvp in item.ByTypeName)
                {
                    detailSheet.Cells[row, 1].Value = item.WorksetName;
                    detailSheet.Cells[row, 2].Value = kvp.Key;
                    detailSheet.Cells[row, 3].Value = kvp.Value;
                    row++;
                }
            }
            detailSheet.Cells[detailSheet.Dimension.Address].AutoFitColumns();

            package.SaveAs(new FileInfo(filePath));
            return filePath;
        }

        private static void WriteHeaders(ExcelWorksheet sheet, string[] headers)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = sheet.Cells[1, i + 1];
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 31, 73, 125));
                cell.Style.Font.Color.SetColor(Color.White);
            }
        }
    }
}
