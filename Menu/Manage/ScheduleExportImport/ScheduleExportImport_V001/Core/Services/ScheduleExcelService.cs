using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Revit26_Plugin.ScheduleExportImport.V001.Core.Models;

namespace Revit26_Plugin.ScheduleExportImport.V001.Core.Services
{
    /// <summary>
    /// Pure Excel read/write — no Revit API dependency.
    /// Export writes a header row (Element ID first) then data rows.
    /// Import reads back the same format keyed on Element ID.
    /// </summary>
    public static class ScheduleExcelService
    {
        private const string ElementIdHeader = "Element ID";

        public static void Export(string filePath, string scheduleName, IReadOnlyList<string> headers, IReadOnlyList<ScheduleRow> rows)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add(SanitizeSheetName(scheduleName));

            // Header row
            ws.Cell(1, 1).Value = ElementIdHeader;
            for (int c = 0; c < headers.Count; c++)
                ws.Cell(1, c + 2).Value = headers[c];

            var headerRow = ws.Row(1);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#2C3E6B");
            headerRow.Style.Font.FontColor = XLColor.White;

            // Element ID column styling
            ws.Column(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#EDF2FF");
            ws.Column(1).Style.Font.Bold = true;

            // Data rows
            int excelRow = 2;
            foreach (var row in rows)
            {
                ws.Cell(excelRow, 1).Value = row.ElementId;
                for (int c = 0; c < headers.Count; c++)
                {
                    var header = headers[c];
                    ws.Cell(excelRow, c + 2).Value = row.Values.TryGetValue(header, out var val) ? val : string.Empty;
                }
                excelRow++;
            }

            // Freeze header + Element ID column
            ws.SheetView.FreezeRows(1);
            ws.SheetView.FreezeColumns(1);

            // Auto-fit columns (cap at 60 chars wide)
            foreach (var col in ws.ColumnsUsed())
            {
                col.AdjustToContents();
                if (col.Width > 60) col.Width = 60;
            }

            wb.SaveAs(filePath);
        }

        /// <summary>
        /// Reads an exported .xlsx back into a list of ScheduleRow.
        /// Row 1 is the header; subsequent rows keyed on "Element ID" column.
        /// Returns null if the file doesn't have a recognised header layout.
        /// </summary>
        public static (List<string> Headers, List<ScheduleRow> Rows) Import(string filePath)
        {
            using var wb = new XLWorkbook(filePath);
            var ws = wb.Worksheets.First();

            var usedRange = ws.RangeUsed();
            if (usedRange == null)
                return (new List<string>(), new List<ScheduleRow>());

            var allRows = usedRange.RowsUsed().ToList();
            if (allRows.Count == 0)
                return (new List<string>(), new List<ScheduleRow>());

            // Parse header row
            var headerCells = allRows[0].CellsUsed().ToList();
            int lastCol = usedRange.LastColumnUsed().ColumnNumber();

            var columnIndexByHeader = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int c = 1; c <= lastCol; c++)
            {
                var cellVal = ws.Cell(1, c).GetString().Trim();
                if (!string.IsNullOrEmpty(cellVal))
                    columnIndexByHeader[cellVal] = c;
            }

            if (!columnIndexByHeader.TryGetValue(ElementIdHeader, out int idCol))
                throw new InvalidDataException($"Could not find an \"{ElementIdHeader}\" column in the first row of the Excel file.");

            var dataHeaders = columnIndexByHeader
                .Where(kv => !kv.Key.Equals(ElementIdHeader, StringComparison.OrdinalIgnoreCase))
                .OrderBy(kv => kv.Value)
                .Select(kv => kv.Key)
                .ToList();

            var scheduleRows = new List<ScheduleRow>();
            for (int r = 2; r <= allRows.Count; r++)
            {
                var idCell = ws.Cell(r, idCol).GetString().Trim();
                if (string.IsNullOrWhiteSpace(idCell)) continue;
                if (!long.TryParse(idCell, out long elementId)) continue;

                var row = new ScheduleRow { ElementId = elementId };
                foreach (var kv in columnIndexByHeader)
                {
                    if (kv.Key.Equals(ElementIdHeader, StringComparison.OrdinalIgnoreCase)) continue;
                    row.Values[kv.Key] = ws.Cell(r, kv.Value).GetString().Trim();
                }
                scheduleRows.Add(row);
            }

            return (dataHeaders, scheduleRows);
        }

        private static string SanitizeSheetName(string name)
        {
            var invalid = new[] { ':', '\\', '/', '?', '*', '[', ']' };
            foreach (var ch in invalid)
                name = name.Replace(ch, '_');
            return name.Length > 31 ? name.Substring(0, 31) : name;
        }
    }
}
