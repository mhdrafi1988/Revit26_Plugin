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
    /// Export: Element ID first, then one column per visible field. Editable columns are
    /// green and unlocked; read-only columns are grey and locked (sheet protected, no password).
    /// Import: reads the same layout back, keyed on the Element ID column.
    /// </summary>
    public static class ScheduleExcelService
    {
        private const string ElementIdHeader = "Element ID";
        private const string InfoSheetName = "Read Me";

        private static readonly XLColor HeaderNavy = XLColor.FromHtml("#2C3E6B");
        private static readonly XLColor EditableHeader = XLColor.FromHtml("#1E7E34");
        private static readonly XLColor EditableFill = XLColor.FromHtml("#EAF7EC");
        private static readonly XLColor ReadOnlyHeader = XLColor.FromHtml("#6B7280");
        private static readonly XLColor ReadOnlyFill = XLColor.FromHtml("#F1F2F4");
        private static readonly XLColor IdFill = XLColor.FromHtml("#E3EAF8");

        public static void Export(string filePath, string scheduleName, IReadOnlyList<string> headers,
            IReadOnlyList<ScheduleRow> rows, ISet<string> editableHeaders)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add(SanitizeSheetName(scheduleName));

            int lastRow = Math.Max(rows.Count + 1, 2);
            int lastCol = headers.Count + 1;

            // Element ID column — always locked
            ws.Cell(1, 1).Value = ElementIdHeader;
            StyleHeader(ws.Cell(1, 1), HeaderNavy);
            var idRange = ws.Range(2, 1, lastRow, 1);
            idRange.Style.Fill.BackgroundColor = IdFill;
            idRange.Style.Font.Bold = true;
            idRange.Style.Protection.Locked = true;

            for (int c = 0; c < headers.Count; c++)
            {
                int col = c + 2;
                bool editable = editableHeaders.Contains(headers[c]);
                var headerCell = ws.Cell(1, col);
                headerCell.Value = headers[c];
                StyleHeader(headerCell, editable ? EditableHeader : ReadOnlyHeader);
                headerCell.GetComment().AddText(editable
                    ? "Editable — changes are written back to Revit on import."
                    : "Read-only — ignored on import (type, calculated or built-in value).");

                var body = ws.Range(2, col, lastRow, col);
                body.Style.Fill.BackgroundColor = editable ? EditableFill : ReadOnlyFill;
                body.Style.Protection.Locked = !editable;
                if (!editable) body.Style.Font.FontColor = XLColor.FromHtml("#6B7280");
            }

            int r = 2;
            foreach (var row in rows)
            {
                ws.Cell(r, 1).Value = row.ElementId;
                for (int c = 0; c < headers.Count; c++)
                {
                    // Force text so Excel doesn't reinterpret values like "01" or "1-2".
                    var cell = ws.Cell(r, c + 2);
                    cell.Value = row.Values.TryGetValue(headers[c], out var val) ? val : string.Empty;
                    cell.Style.NumberFormat.Format = "@";
                }
                r++;
            }

            ws.Range(1, 1, lastRow, lastCol).Style.Border.InsideBorder = XLBorderStyleValues.Hair;
            ws.Range(1, 1, lastRow, lastCol).Style.Border.InsideBorderColor = XLColor.FromHtml("#D1D5DB");
            ws.SheetView.FreezeRows(1);
            ws.SheetView.FreezeColumns(1);
            ws.Range(1, 1, lastRow, lastCol).SetAutoFilter();

            foreach (var col in ws.ColumnsUsed())
            {
                col.AdjustToContents();
                if (col.Width > 60) col.Width = 60;
                if (col.Width < 10) col.Width = 10;
            }

            // No password: users can still Review → Unprotect Sheet if they really need to.
            ws.Protect()
                .AllowElement(XLSheetProtectionElements.AutoFilter)
                .AllowElement(XLSheetProtectionElements.Sort)
                .AllowElement(XLSheetProtectionElements.FormatColumns)
                .AllowElement(XLSheetProtectionElements.SelectEverything);

            AddInfoSheet(wb, scheduleName, rows.Count, headers.Count, editableHeaders.Count);
            wb.SaveAs(filePath);
        }

        private static void StyleHeader(IXLCell cell, XLColor fill)
        {
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = fill;
            cell.Style.Protection.Locked = true;
        }

        private static void AddInfoSheet(XLWorkbook wb, string scheduleName, int rowCount, int fieldCount, int editableCount)
        {
            var info = wb.Worksheets.Add(InfoSheetName);
            info.Cell(1, 1).Value = "Schedule Export / Import — V001";
            info.Cell(1, 1).Style.Font.Bold = true;
            info.Cell(1, 1).Style.Font.FontSize = 14;
            info.Cell(3, 1).Value = "Schedule";           info.Cell(3, 2).Value = scheduleName;
            info.Cell(4, 1).Value = "Exported";           info.Cell(4, 2).Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            info.Cell(5, 1).Value = "Rows";               info.Cell(5, 2).Value = rowCount;
            info.Cell(6, 1).Value = "Fields (editable)";  info.Cell(6, 2).Value = $"{fieldCount} ({editableCount})";

            info.Cell(8, 1).Value = "Colour key";
            info.Cell(8, 1).Style.Font.Bold = true;
            info.Cell(9, 1).Style.Fill.BackgroundColor = EditableFill;
            info.Cell(9, 2).Value = "Green — editable, written back on import";
            info.Cell(10, 1).Style.Fill.BackgroundColor = ReadOnlyFill;
            info.Cell(10, 2).Value = "Grey — read-only, locked and ignored on import";
            info.Cell(11, 1).Style.Fill.BackgroundColor = IdFill;
            info.Cell(11, 2).Value = "Blue — Element ID, do not change";

            info.Cell(13, 1).Value = "Only cells whose value changed are written. Do not rename the header row or the sheet order.";
            info.Column(1).Width = 20;
            info.Column(2).Width = 50;
        }

        /// <summary>
        /// Reads the first worksheet back. Row 1 is the header; rows keyed on "Element ID".
        /// </summary>
        public static (List<string> Headers, List<ScheduleRow> Rows) Import(string filePath)
        {
            using var wb = new XLWorkbook(filePath);
            var ws = wb.Worksheets.First(s => !s.Name.Equals(InfoSheetName, StringComparison.OrdinalIgnoreCase));

            var lastRowUsed = ws.LastRowUsed();
            var lastColUsed = ws.LastColumnUsed();
            if (lastRowUsed == null || lastColUsed == null)
                return (new List<string>(), new List<ScheduleRow>());

            int lastRow = lastRowUsed.RowNumber();
            int lastCol = lastColUsed.ColumnNumber();

            var columnIndexByHeader = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int c = 1; c <= lastCol; c++)
            {
                var h = ws.Cell(1, c).GetString().Trim();
                if (!string.IsNullOrEmpty(h) && !columnIndexByHeader.ContainsKey(h))
                    columnIndexByHeader[h] = c;
            }

            if (!columnIndexByHeader.TryGetValue(ElementIdHeader, out int idCol))
                throw new InvalidDataException($"Could not find an \"{ElementIdHeader}\" column in the first row of the Excel file.");

            var dataHeaders = columnIndexByHeader
                .Where(kv => kv.Value != idCol)
                .OrderBy(kv => kv.Value)
                .Select(kv => kv.Key)
                .ToList();

            var result = new List<ScheduleRow>();
            for (int r = 2; r <= lastRow; r++)
            {
                var idText = ws.Cell(r, idCol).GetString().Trim();
                if (!long.TryParse(idText, out long elementId)) continue;

                var row = new ScheduleRow { ElementId = elementId };
                foreach (var h in dataHeaders)
                    row.Values[h] = ws.Cell(r, columnIndexByHeader[h]).GetString().Trim();
                result.Add(row);
            }

            return (dataHeaders, result);
        }

        private static string SanitizeSheetName(string name)
        {
            foreach (var ch in new[] { ':', '\\', '/', '?', '*', '[', ']' })
                name = name.Replace(ch, '_');
            if (name.Equals(InfoSheetName, StringComparison.OrdinalIgnoreCase)) name += " (1)";
            return name.Length > 31 ? name.Substring(0, 31) : name;
        }
    }
}
