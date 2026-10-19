using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Security;

namespace Revit26_Plugin.DwgStatistics.V005.Core.Services
{
    /// <summary>
    /// Writes a minimal but valid .xlsx (Office Open XML) workbook with only System.IO.Compression,
    /// so the tool adds no NuGet dependency. Each sheet gets a bold, frozen header row with an
    /// AutoFilter and column widths sized to the content. Numbers stay numbers; everything else is
    /// written as inline text.
    /// </summary>
    public sealed class SimpleXlsxWriter
    {
        private readonly List<Sheet> _sheets = new List<Sheet>();

        private sealed class Sheet
        {
            public string Name;
            public IReadOnlyList<string> Headers;
            public List<object[]> Rows;
        }

        /// <summary>Adds a sheet. Cell values may be string, int, long, double, decimal, bool, DateTime or null.</summary>
        public void AddSheet(string name, IReadOnlyList<string> headers, IEnumerable<object[]> rows)
        {
            _sheets.Add(new Sheet
            {
                Name = SafeSheetName(name, _sheets.Select(s => s.Name)),
                Headers = headers,
                Rows = rows.ToList()
            });
        }

        /// <summary>Writes the workbook to <paramref name="path"/>, replacing any existing file.</summary>
        public void Save(string path)
        {
            if (File.Exists(path)) File.Delete(path);

            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                Write(zip, "[Content_Types].xml", ContentTypes());
                Write(zip, "_rels/.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                    "</Relationships>");
                Write(zip, "xl/workbook.xml", Workbook());
                Write(zip, "xl/_rels/workbook.xml.rels", WorkbookRels());
                Write(zip, "xl/styles.xml", Styles());
                for (int i = 0; i < _sheets.Count; i++)
                    Write(zip, $"xl/worksheets/sheet{i + 1}.xml", SheetXml(_sheets[i]));
            }
        }

        private string ContentTypes()
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
            sb.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
            sb.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
            sb.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
            sb.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
            for (int i = 0; i < _sheets.Count; i++)
                sb.Append($"<Override PartName=\"/xl/worksheets/sheet{i + 1}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
            sb.Append("</Types>");
            return sb.ToString();
        }

        private string Workbook()
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>");
            for (int i = 0; i < _sheets.Count; i++)
                sb.Append($"<sheet name=\"{Esc(_sheets[i].Name)}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>");
            sb.Append("</sheets><definedNames>");
            for (int i = 0; i < _sheets.Count; i++)
            {
                var s = _sheets[i];
                if (s.Headers.Count == 0) continue;
                string range = $"'{s.Name.Replace("'", "''")}'!$A$1:${ColumnName(s.Headers.Count - 1)}${Math.Max(1, s.Rows.Count + 1)}";
                sb.Append($"<definedName name=\"_xlnm._FilterDatabase\" localSheetId=\"{i}\" hidden=\"1\">{Esc(range)}</definedName>");
            }
            sb.Append("</definedNames></workbook>");
            return sb.ToString();
        }

        private string WorkbookRels()
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
            for (int i = 0; i < _sheets.Count; i++)
                sb.Append($"<Relationship Id=\"rId{i + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i + 1}.xml\"/>");
            sb.Append($"<Relationship Id=\"rId{_sheets.Count + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
            sb.Append("</Relationships>");
            return sb.ToString();
        }

        // Style 0 = normal, 1 = bold header with a light fill, 2 = date (yyyy-mm-dd hh:mm).
        private static string Styles() =>
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<numFmts count=\"1\"><numFmt numFmtId=\"164\" formatCode=\"yyyy-mm-dd hh:mm\"/></numFmts>" +
            "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
            "<fills count=\"3\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill>" +
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFDCE3EF\"/><bgColor indexed=\"64\"/></patternFill></fill></fills>" +
            "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"3\">" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
            "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\"/>" +
            "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
            "</cellXfs>" +
            "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
            "</styleSheet>";

        private static string SheetXml(Sheet sheet)
        {
            int cols = sheet.Headers.Count;
            var widths = new double[cols];
            for (int c = 0; c < cols; c++) widths[c] = Math.Max(8, sheet.Headers[c].Length + 4);
            foreach (var row in sheet.Rows)
                for (int c = 0; c < cols && c < row.Length; c++)
                    widths[c] = Math.Max(widths[c], Math.Min(60, DisplayLength(row[c]) + 2));

            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
            sb.Append("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
            sb.Append("<sheetFormatPr defaultRowHeight=\"15\"/>");
            if (cols > 0)
            {
                sb.Append("<cols>");
                for (int c = 0; c < cols; c++)
                    sb.Append($"<col min=\"{c + 1}\" max=\"{c + 1}\" width=\"{widths[c].ToString("0.##", CultureInfo.InvariantCulture)}\" customWidth=\"1\"/>");
                sb.Append("</cols>");
            }

            sb.Append("<sheetData>");
            sb.Append("<row r=\"1\">");
            for (int c = 0; c < cols; c++)
                sb.Append(Cell(c, 1, sheet.Headers[c], style: 1));
            sb.Append("</row>");

            int r = 2;
            foreach (var row in sheet.Rows)
            {
                sb.Append($"<row r=\"{r}\">");
                for (int c = 0; c < cols && c < row.Length; c++)
                    sb.Append(Cell(c, r, row[c], style: 0));
                sb.Append("</row>");
                r++;
            }
            sb.Append("</sheetData>");

            if (cols > 0)
                sb.Append($"<autoFilter ref=\"A1:{ColumnName(cols - 1)}{Math.Max(1, sheet.Rows.Count + 1)}\"/>");

            sb.Append("</worksheet>");
            return sb.ToString();
        }

        private static string Cell(int col, int row, object value, int style)
        {
            string reference = ColumnName(col) + row.ToString(CultureInfo.InvariantCulture);
            string s = style == 0 ? string.Empty : $" s=\"{style}\"";

            switch (value)
            {
                case null:
                    return string.Empty;
                case int i:
                    return $"<c r=\"{reference}\"{s}><v>{i.ToString(CultureInfo.InvariantCulture)}</v></c>";
                case long l:
                    return $"<c r=\"{reference}\"{s}><v>{l.ToString(CultureInfo.InvariantCulture)}</v></c>";
                case double d when !double.IsNaN(d) && !double.IsInfinity(d):
                    return $"<c r=\"{reference}\"{s}><v>{d.ToString("R", CultureInfo.InvariantCulture)}</v></c>";
                case decimal m:
                    return $"<c r=\"{reference}\"{s}><v>{m.ToString(CultureInfo.InvariantCulture)}</v></c>";
                case bool b:
                    return $"<c r=\"{reference}\"{s} t=\"b\"><v>{(b ? 1 : 0)}</v></c>";
                case DateTime dt when dt.Year < 1900:
                    return string.Empty; // Excel has no dates before 1900; leave the cell blank.
                case DateTime dt:
                    string ds = style == 0 ? " s=\"2\"" : s;
                    return $"<c r=\"{reference}\"{ds}><v>{dt.ToOADate().ToString("R", CultureInfo.InvariantCulture)}</v></c>";
                default:
                    string text = value.ToString() ?? string.Empty;
                    if (text.Length == 0) return string.Empty;
                    if (text.Length > 32767) text = text.Substring(0, 32767);
                    return $"<c r=\"{reference}\"{s} t=\"inlineStr\"><is><t xml:space=\"preserve\">{Esc(text)}</t></is></c>";
            }
        }

        private static int DisplayLength(object value)
        {
            switch (value)
            {
                case null: return 0;
                case DateTime _: return 16;
                default: return (value.ToString() ?? string.Empty).Length;
            }
        }

        /// <summary>0 → A, 25 → Z, 26 → AA.</summary>
        public static string ColumnName(int index)
        {
            var name = string.Empty;
            index++;
            while (index > 0)
            {
                int mod = (index - 1) % 26;
                name = (char)('A' + mod) + name;
                index = (index - 1) / 26;
            }
            return name;
        }

        // Excel sheet names: max 31 chars, none of : \ / ? * [ ], unique.
        private static string SafeSheetName(string name, IEnumerable<string> existing)
        {
            var bad = new[] { ':', '\\', '/', '?', '*', '[', ']' };
            var clean = new string((name ?? "Sheet").Select(ch => bad.Contains(ch) ? '_' : ch).ToArray()).Trim();
            if (clean.Length == 0) clean = "Sheet";
            if (clean.Length > 31) clean = clean.Substring(0, 31);

            var taken = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
            string candidate = clean;
            int n = 2;
            while (taken.Contains(candidate))
            {
                string suffix = $" ({n++})";
                candidate = (clean.Length + suffix.Length > 31 ? clean.Substring(0, 31 - suffix.Length) : clean) + suffix;
            }
            return candidate;
        }

        // XML-escape and drop characters XML 1.0 does not allow.
        private static string Esc(string text)
        {
            var filtered = new StringBuilder(text.Length);
            foreach (char ch in text)
            {
                if (ch == '\t' || ch == '\n' || ch == '\r' || (ch >= 0x20 && ch != 0xFFFE && ch != 0xFFFF))
                    filtered.Append(ch);
            }
            return SecurityElement.Escape(filtered.ToString()) ?? string.Empty;
        }

        private static void Write(ZipArchive zip, string name, string content)
        {
            var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using (var stream = entry.Open())
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                writer.Write(content);
        }
    }
}
