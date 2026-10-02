using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Revit26_Plugin.ScheduleExportImport.V002.Core.Models;

namespace Revit26_Plugin.ScheduleExportImport.V002.Core.Services
{
    public static class ImportReportService
    {
        // ── Palette ──────────────────────────────────────────────────────────
        private static readonly XLColor NavyHeader    = XLColor.FromHtml("#2C3E6B");
        private static readonly XLColor GreenFill     = XLColor.FromHtml("#DCF3E3");
        private static readonly XLColor GreenText     = XLColor.FromHtml("#15803D");
        private static readonly XLColor RedFill       = XLColor.FromHtml("#FBDADA");
        private static readonly XLColor RedText       = XLColor.FromHtml("#B91C1C");
        private static readonly XLColor OrangeFill    = XLColor.FromHtml("#FFF0CC");
        private static readonly XLColor OrangeText    = XLColor.FromHtml("#A06A00");
        private static readonly XLColor BlueFill      = XLColor.FromHtml("#E3ECFB");
        private static readonly XLColor BlueText      = XLColor.FromHtml("#1D4ED8");
        private static readonly XLColor GreyFill      = XLColor.FromHtml("#F1F2F4");
        private static readonly XLColor GreyText      = XLColor.FromHtml("#6B7280");
        private static readonly XLColor SeparatorLine = XLColor.FromHtml("#D1D5DB");

        public static string Write(string excelPath, string scheduleName, ImportAnalysis analysis, IEnumerable<ImportChange> items)
        {
            var folder = Path.GetDirectoryName(excelPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var name   = Path.GetFileNameWithoutExtension(excelPath);
            var path   = Path.Combine(folder, $"{name}_ImportReport_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.xlsx");

            var list = items.ToList();

            using var wb = new XLWorkbook();
            AddDetailSheet(wb, list);
            AddSummarySheet(wb, scheduleName, excelPath, analysis, list);

            wb.SaveAs(path);
            return path;
        }

        // ── Detail sheet ─────────────────────────────────────────────────────
        private static void AddDetailSheet(XLWorkbook wb, List<ImportChange> items)
        {
            var ws = wb.Worksheets.Add("Changes");

            // Header row
            string[] headers = { "Status", "Element ID", "Name", "Parameter", "Old value", "New value", "Note" };
            for (int c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(1, c + 1);
                cell.Value = headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = NavyHeader;
                cell.Style.Protection.Locked = true;
            }

            // Data rows
            int r = 2;
            foreach (var item in items.OrderBy(i => i.Status).ThenBy(i => i.ElementId))
            {
                ws.Cell(r, 1).Value = StatusLabel(item.Status);
                ws.Cell(r, 2).Value = item.ElementId;
                ws.Cell(r, 3).Value = item.ElementName;
                ws.Cell(r, 4).Value = item.ParameterName;
                ws.Cell(r, 5).Value = item.OldValue;
                ws.Cell(r, 6).Value = item.NewValue;
                ws.Cell(r, 7).Value = item.Message;

                var (fill, text) = RowColour(item.Status);
                var row = ws.Range(r, 1, r, headers.Length);
                row.Style.Fill.BackgroundColor = fill;
                row.Style.Font.FontColor = text;
                r++;
            }

            int lastRow = Math.Max(r - 1, 2);
            var table = ws.Range(1, 1, lastRow, headers.Length);
            table.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
            table.Style.Border.InsideBorderColor = SeparatorLine;
            table.SetAutoFilter();
            ws.SheetView.FreezeRows(1);

            // Column widths
            int[] widths = { 16, 12, 24, 22, 26, 26, 44 };
            for (int c = 0; c < widths.Length; c++)
                ws.Column(c + 1).Width = widths[c];
        }

        // ── Summary sheet ────────────────────────────────────────────────────
        private static void AddSummarySheet(XLWorkbook wb, string scheduleName, string excelPath,
            ImportAnalysis analysis, List<ImportChange> items)
        {
            var ws = wb.Worksheets.Add("Summary");
            ws.TabColor = NavyHeader;

            ws.Cell(1, 1).Value = "Schedule Import Report — V002";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;

            int r = 3;
            void Meta(string label, object value)
            {
                ws.Cell(r, 1).Value = label;
                ws.Cell(r, 1).Style.Font.Bold = true;
                ws.Cell(r, 2).Value = value?.ToString() ?? string.Empty;
                r++;
            }

            Meta("Date",      DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Meta("Schedule",  scheduleName);
            Meta("File",      excelPath);
            r++;

            Meta("Rows in file",          analysis.RowsInFile);
            Meta("Elements matched",      analysis.ElementsMatched);
            Meta("Unchanged values",      analysis.UnchangedValues);
            Meta("Duplicate rows merged", analysis.DuplicateRowsMerged);
            r++;

            // Per-status counts with colour chips
            ws.Cell(r, 1).Value = "Status breakdown";
            ws.Cell(r, 1).Style.Font.Bold = true;
            r++;

            foreach (var g in items.GroupBy(i => i.Status).OrderBy(g => g.Key))
            {
                var (fill, text) = RowColour(g.Key);
                ws.Cell(r, 1).Value = StatusLabel(g.Key);
                ws.Cell(r, 1).Style.Fill.BackgroundColor = fill;
                ws.Cell(r, 1).Style.Font.FontColor = text;
                ws.Cell(r, 1).Style.Font.Bold = true;
                ws.Cell(r, 2).Value = g.Count();
                r++;
            }

            ws.Column(1).Width = 26;
            ws.Column(2).Width = 14;
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private static (XLColor fill, XLColor text) RowColour(ImportChangeStatus status) => status switch
        {
            ImportChangeStatus.Applied        => (GreenFill,  GreenText),
            ImportChangeStatus.Failed         => (RedFill,    RedText),
            ImportChangeStatus.Duplicate      => (RedFill,    RedText),
            ImportChangeStatus.NotFound       => (OrangeFill, OrangeText),
            ImportChangeStatus.MissingFromFile=> (OrangeFill, OrangeText),
            ImportChangeStatus.Change         => (BlueFill,   BlueText),
            _                                 => (GreyFill,   GreyText),
        };

        private static string StatusLabel(ImportChangeStatus status) => status switch
        {
            ImportChangeStatus.Change         => "Change",
            ImportChangeStatus.NotFound       => "Not found",
            ImportChangeStatus.Duplicate      => "Duplicate ID",
            ImportChangeStatus.ReadOnly       => "Read-only",
            ImportChangeStatus.TypeParameter  => "Type param",
            ImportChangeStatus.MissingFromFile=> "Missing from file",
            ImportChangeStatus.Applied        => "Applied",
            ImportChangeStatus.Failed         => "Failed",
            _                                 => status.ToString()
        };
    }
}
