using System;
using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;

namespace Revit26_Plugin.WorksetRenamer.FX03.ViewModels
{
    public struct ExcelMappingRow
    {
        public string OldName;
        public string NewName;
    }

    /// <summary>
    /// Reads a two-column Old Name / New Name mapping from the first worksheet
    /// of an .xlsx file — Column A = Old Name, Column B = New Name. No header
    /// text is required; a header row (e.g. "Old Name" / "New Name") in row 1
    /// is auto-detected by keyword and skipped so it isn't read as data.
    /// </summary>
    public static class ExcelMappingReader
    {
        private static readonly HashSet<string> HeaderKeywordsOld =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "old name", "oldname", "old", "current name", "name" };

        private static readonly HashSet<string> HeaderKeywordsNew =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "new name", "newname", "new" };

        public static List<ExcelMappingRow> Read(string filePath)
        {
            var result = new List<ExcelMappingRow>();

            using (var wb = new XLWorkbook(filePath))
            {
                var ws = wb.Worksheets.First();
                var usedRows = ws.RangeUsed()?.RowsUsed().ToList() ?? new List<IXLRangeRow>();

                bool first = true;
                foreach (var row in usedRows)
                {
                    string oldName = row.Cell(1).GetString().Trim();
                    string newName = row.Cell(2).GetString().Trim();

                    if (first)
                    {
                        first = false;
                        if (HeaderKeywordsOld.Contains(oldName) && HeaderKeywordsNew.Contains(newName))
                            continue; // skip header row
                    }

                    if (string.IsNullOrWhiteSpace(oldName) && string.IsNullOrWhiteSpace(newName))
                        continue; // skip fully blank row

                    result.Add(new ExcelMappingRow { OldName = oldName, NewName = newName });
                }
            }

            return result;
        }
    }
}
