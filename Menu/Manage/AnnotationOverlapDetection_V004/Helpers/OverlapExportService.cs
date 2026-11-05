using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ClosedXML.Excel;
using Revit26_Plugin.AnnotationOverlapDetection.V004.Models;

namespace Revit26_Plugin.AnnotationOverlapDetection.V004.Helpers
{
    /// <summary>
    /// v4.3.0: saves the overlap results to .xlsx or .csv. Pure file I/O, no Revit API.
    /// Columns follow the results grid order; new columns must only ever be appended.
    /// </summary>
    internal static class OverlapExportService
    {
        /// <summary>Export format version, written to the Excel "Info" sheet.</summary>
        public const int FormatVersion = 1;

        private static readonly string[] Headers =
        {
            "ID 1", "Type", "X (mm)", "Y (mm)", "Aligned", "V-Dist (mm)", "H-Dist (mm)", "ID 2",
            "Overlap W (paper mm)", "Overlap H (paper mm)", "View"
        };

        /// <summary>Settings and context written next to the results.</summary>
        public sealed class ExportInfo
        {
            public string DocumentTitle { get; init; }
            public string ToolVersion { get; init; }
            public string Scope { get; init; }
            public bool CheckAcrossTypes { get; init; }
            public bool IncludeLeaders { get; init; }
            public double MinOverlapPaperMm { get; init; }
        }

        /// <summary>
        /// Writes the results to <paramref name="path"/>; the extension (.xlsx or .csv) picks the format.
        /// </summary>
        /// <exception cref="IOException">The file is open in another program or cannot be written.</exception>
        public static void Export(string path, IReadOnlyList<OverlapResult> results, ExportInfo info)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("No file chosen.", nameof(path));

            if (string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase))
                WriteCsv(path, results);
            else
                WriteXlsx(path, results, info);
        }

        private static object[] Row(OverlapResult r) => new object[]
        {
            r.ElementId1, r.AnnotationType, r.X, r.Y, r.GridAligned, r.VerticalDistance, r.HorizontalDistance,
            r.ElementId2, r.OverlapWidthPaper, r.OverlapHeightPaper, r.ViewName ?? string.Empty
        };

        private static void WriteCsv(string path, IReadOnlyList<OverlapResult> results)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", Headers.Select(Escape)));
            foreach (var r in results)
                sb.AppendLine(string.Join(",", Row(r).Select(v => Escape(Format(v)))));

            // UTF-8 with BOM so Excel shows non-ASCII view names correctly.
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }

        private static void WriteXlsx(string path, IReadOnlyList<OverlapResult> results, ExportInfo info)
        {
            using var wb = new XLWorkbook();

            var ws = wb.Worksheets.Add("Overlaps");
            for (int c = 0; c < Headers.Length; c++)
                ws.Cell(1, c + 1).Value = Headers[c];

            for (int i = 0; i < results.Count; i++)
            {
                object[] row = Row(results[i]);
                for (int c = 0; c < row.Length; c++)
                    ws.Cell(i + 2, c + 1).Value = XLCellValue.FromObject(row[c]);
            }

            var header = ws.Range(1, 1, 1, Headers.Length);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#EDEDED");
            ws.Column(3).Style.NumberFormat.Format = "0.0000";
            ws.Column(4).Style.NumberFormat.Format = "0.0000";
            for (int c = 6; c <= 7; c++) ws.Column(c).Style.NumberFormat.Format = "0.00";
            for (int c = 9; c <= 10; c++) ws.Column(c).Style.NumberFormat.Format = "0.00";
            ws.SheetView.FreezeRows(1);
            if (results.Count > 0)
                ws.Range(1, 1, results.Count + 1, Headers.Length).SetAutoFilter();
            ws.Columns().AdjustToContents();

            var infoWs = wb.Worksheets.Add("Info");
            var lines = new List<(string, object)>
            {
                ("Export format version", FormatVersion),
                ("Tool", "Annotation Overlap Detection"),
                ("Tool version", info?.ToolVersion ?? string.Empty),
                ("Document", info?.DocumentTitle ?? string.Empty),
                ("Exported", DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)),
                ("Scope", info?.Scope ?? string.Empty),
                ("Check across types", info?.CheckAcrossTypes == true ? "Yes" : "No"),
                ("Include leaders", info?.IncludeLeaders == true ? "Yes" : "No"),
                ("Min overlap (paper mm)", info?.MinOverlapPaperMm ?? 0),
                ("Overlaps", results.Count)
            };
            for (int i = 0; i < lines.Count; i++)
            {
                infoWs.Cell(i + 1, 1).Value = lines[i].Item1;
                infoWs.Cell(i + 1, 2).Value = XLCellValue.FromObject(lines[i].Item2);
            }
            infoWs.Column(1).Style.Font.Bold = true;
            infoWs.Columns().AdjustToContents();

            wb.SaveAs(path);
        }

        private static string Format(object v) => v switch
        {
            double d => d.ToString("0.####", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => v?.ToString() ?? string.Empty
        };

        private static string Escape(string s)
        {
            s ??= string.Empty;
            // Guard against spreadsheet formula injection from view names.
            if (s.Length > 0 && "=+-@".IndexOf(s[0]) >= 0 && !double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                s = "'" + s;
            return s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
                ? "\"" + s.Replace("\"", "\"\"") + "\""
                : s;
        }
    }
}
