using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Revit26_Plugin.ScheduleExportImport.V007.Core.Models;

namespace Revit26_Plugin.ScheduleExportImport.V007.Core.Services
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
        // Very hidden (only visible through VBA): values at export time, and dropdown source lists.
        private const string OriginalSheetName = "_Original";
        private const string ListsSheetName = "_Lists";

        private static readonly XLColor HeaderNavy = XLColor.FromHtml("#2C3E6B");
        private static readonly XLColor EditableHeader = XLColor.FromHtml("#1E7E34");
        private static readonly XLColor EditableFill = XLColor.FromHtml("#EAF7EC");
        private static readonly XLColor ReadOnlyHeader = XLColor.FromHtml("#6B7280");
        private static readonly XLColor ReadOnlyFill = XLColor.FromHtml("#F1F2F4");
        private static readonly XLColor IdFill = XLColor.FromHtml("#E3EAF8");

        public static void Export(string filePath, string scheduleName, IReadOnlyList<string> headers,
            IReadOnlyList<ScheduleRow> rows, ISet<string> editableHeaders, ExportExtras extras = null)
        {
            extras ??= new ExportExtras();
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
                    : extras.TypeHeaders.Contains(headers[c])
                        ? "Type parameter — locked here. Unprotect the sheet to edit; on import such edits are listed unticked and change every instance of the type."
                        : "Read-only — ignored on import (calculated or built-in value).");

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
            AddDropdowns(wb, ws, headers, editableHeaders, extras.Dropdowns, lastRow);
            AddOriginalSheet(wb, headers, rows);
            wb.SaveAs(filePath);
        }

        /// <summary>
        /// Excel dropdown lists on editable columns with a fixed set of values (levels, Yes/No),
        /// so a typo like "Levl 2" can't be entered. Lists live on a very hidden sheet.
        /// </summary>
        private static void AddDropdowns(XLWorkbook wb, IXLWorksheet ws, IReadOnlyList<string> headers,
            ISet<string> editableHeaders, Dictionary<string, List<string>> dropdowns, int lastRow)
        {
            var wanted = headers.Select((h, i) => (Header: h, Col: i + 2))
                .Where(x => editableHeaders.Contains(x.Header) && dropdowns.TryGetValue(x.Header, out var v) && v.Count > 0)
                .ToList();
            if (wanted.Count == 0) return;

            var lists = wb.Worksheets.Add(ListsSheetName);
            int listCol = 1;
            foreach (var (header, col) in wanted)
            {
                var values = dropdowns[header];
                lists.Cell(1, listCol).Value = header;
                for (int i = 0; i < values.Count; i++)
                {
                    lists.Cell(i + 2, listCol).Value = values[i];
                    lists.Cell(i + 2, listCol).Style.NumberFormat.Format = "@";
                }
                var source = lists.Range(2, listCol, values.Count + 1, listCol);
                // Rows below the data too, so rows the user adds can't sneak in a typo either.
                var validation = ws.Range(2, col, Math.Max(lastRow, 2) + 200, col).CreateDataValidation();
                validation.List(source, true);
                validation.IgnoreBlanks = true;
                validation.ErrorStyle = XLErrorStyle.Stop;
                validation.ErrorTitle = header;
                validation.ErrorMessage = $"Pick a value from the list ({values.Count} option(s) from the Revit model).";
                listCol++;
            }
            lists.Visibility = XLWorksheetVisibility.VeryHidden;
        }

        /// <summary>Copy of the exported values, so import can tell "you edited this" from "someone changed the model since".</summary>
        private static void AddOriginalSheet(XLWorkbook wb, IReadOnlyList<string> headers, IReadOnlyList<ScheduleRow> rows)
        {
            var orig = wb.Worksheets.Add(OriginalSheetName);
            orig.Cell(1, 1).Value = ElementIdHeader;
            for (int c = 0; c < headers.Count; c++)
                orig.Cell(1, c + 2).Value = headers[c];
            int r = 2;
            foreach (var row in rows)
            {
                orig.Cell(r, 1).Value = row.ElementId;
                for (int c = 0; c < headers.Count; c++)
                {
                    var cell = orig.Cell(r, c + 2);
                    cell.Value = row.Values.TryGetValue(headers[c], out var val) ? val : string.Empty;
                    cell.Style.NumberFormat.Format = "@";
                }
                r++;
            }
            orig.Visibility = XLWorksheetVisibility.VeryHidden;
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
            info.Cell(1, 1).Value = "Schedule Export / Import — V005";
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
            info.Cell(14, 1).Value = "Columns with a dropdown (levels, Yes/No) only accept values that exist in the Revit model.";
            info.Cell(15, 1).Value = "If someone changes a value in Revit after this export, import shows it as a conflict instead of overwriting it.";
            info.Column(1).Width = 20;
            info.Column(2).Width = 50;
        }

        /// <summary>
        /// Reads the first worksheet back. Row 1 is the header; rows keyed on "Element ID".
        /// </summary>
        /// <summary>
        /// Reads the data sheet back (row 1 = header, rows keyed on "Element ID"), plus the hidden
        /// sheet of values as they were at export, used to spot edits made in the model since.
        /// </summary>
        public static ExcelImportFile Import(string filePath)
        {
            // Excel keeps the workbook locked while it is open, and Export opens it in Excel.
            // Read through a share-friendly stream so "edit, save, import" works without closing Excel.
            using var buffer = new MemoryStream();
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                fs.CopyTo(buffer);
            buffer.Position = 0;
            using var wb = new XLWorkbook(buffer);
            var file = new ExcelImportFile { FilePath = filePath };

            if (wb.Worksheets.TryGetWorksheet(InfoSheetName, out var infoSheet))
                file.ScheduleName = infoSheet.Cell(3, 2).GetString().Trim();

            var ws = wb.Worksheets.FirstOrDefault(s => !s.Name.Equals(InfoSheetName, StringComparison.OrdinalIgnoreCase)
                                                       && !s.Name.Equals(OriginalSheetName, StringComparison.OrdinalIgnoreCase)
                                                       && !s.Name.Equals(ListsSheetName, StringComparison.OrdinalIgnoreCase))
                     ?? throw new InvalidDataException("The workbook has no data sheet.");

            var (headers, rows) = ReadTable(ws, throwIfNoIdColumn: true);
            file.Headers = headers;
            file.Rows = rows;

            if (wb.Worksheets.TryGetWorksheet(OriginalSheetName, out var original))
            {
                var (_, originalRows) = ReadTable(original, throwIfNoIdColumn: false);
                foreach (var row in originalRows)
                    file.OriginalValues[row.ElementId] = row.Values;
            }

            return file;
        }

        private static (List<string> Headers, List<ScheduleRow> Rows) ReadTable(IXLWorksheet ws, bool throwIfNoIdColumn)
        {
            var headers = new List<string>();
            var rows = new List<ScheduleRow>();
            var lastRowUsed = ws.LastRowUsed();
            var lastColUsed = ws.LastColumnUsed();
            if (lastRowUsed == null || lastColUsed == null)
                return (headers, rows);

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
            {
                if (throwIfNoIdColumn)
                    throw new InvalidDataException($"Could not find an \"{ElementIdHeader}\" column in the first row of the Excel file.");
                return (headers, rows);
            }

            headers = columnIndexByHeader
                .Where(kv => kv.Value != idCol)
                .OrderBy(kv => kv.Value)
                .Select(kv => kv.Key)
                .ToList();

            for (int r = 2; r <= lastRow; r++)
            {
                var idText = ws.Cell(r, idCol).GetString().Trim();
                if (!long.TryParse(idText, out long elementId)) continue;

                var row = new ScheduleRow { ElementId = elementId };
                foreach (var h in headers)
                    row.Values[h] = ws.Cell(r, columnIndexByHeader[h]).GetString().Trim();
                rows.Add(row);
            }
            return (headers, rows);
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
