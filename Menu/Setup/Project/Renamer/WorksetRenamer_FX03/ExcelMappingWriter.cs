using System.Collections.Generic;
using ClosedXML.Excel;

namespace Revit26_Plugin.WorksetRenamer.FX03.ViewModels
{
    /// <summary>
    /// Writes the model's current user worksets to a two-column Old Name / New
    /// Name workbook — Column A = current name, Column B = same name (so the
    /// user can edit just the names they want changed and re-import as-is).
    /// </summary>
    public static class ExcelMappingWriter
    {
        public static void Write(string filePath, IEnumerable<string> currentWorksetNames)
        {
            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Worksets");
                ws.Cell(1, 1).Value = "Old Name";
                ws.Cell(1, 2).Value = "New Name";
                ws.Row(1).Style.Font.Bold = true;

                int row = 2;
                foreach (var name in currentWorksetNames)
                {
                    ws.Cell(row, 1).Value = name;
                    ws.Cell(row, 2).Value = name;
                    row++;
                }

                ws.Column(1).AdjustToContents();
                ws.Column(2).AdjustToContents();

                wb.SaveAs(filePath);
            }
        }
    }
}
