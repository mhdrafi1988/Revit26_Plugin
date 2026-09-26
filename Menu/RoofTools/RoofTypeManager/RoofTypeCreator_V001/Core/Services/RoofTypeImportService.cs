using System.Collections.Generic;
using ClosedXML.Excel;
using Revit26_Plugin.RoofTypeCreator.V001.Core.Models;

namespace Revit26_Plugin.RoofTypeCreator.V001.Core.Services
{
    public static class RoofTypeImportService
    {
        public static List<ImportPreviewItem> LoadPreview(string filePath, System.Collections.Generic.HashSet<string> existingNames)
        {
            var result = new List<ImportPreviewItem>();

            using (var wb = new XLWorkbook(filePath))
            {
                foreach (var ws in wb.Worksheets)
                {
                    // Only sheets with our header format
                    if (ws.Cell(1, 1).GetString() != "Type Name") continue;

                    int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
                    for (int r = 2; r <= lastRow; r++)
                    {
                        string typeName = ws.Cell(r, 1).GetString();
                        if (string.IsNullOrWhiteSpace(typeName)) continue;

                        string function       = ws.Cell(r, 3).GetString();
                        int    layerCount     = ws.Cell(r, 4).GetValue<int>();
                        double thickness      = ws.Cell(r, 5).GetValue<double>();
                        string wrapsAtInserts = ws.Cell(r, 6).GetString();
                        string wrapsAtEnds    = ws.Cell(r, 7).GetString();

                        var layers = new List<LayerData>();
                        for (int li = 0; li < layerCount && li < 10; li++)
                        {
                            int baseCol = 8 + li * 5;
                            string varCell = ws.Cell(r, baseCol + 4).GetString();
                            layers.Add(new LayerData
                            {
                                MaterialName  = ws.Cell(r, baseCol).GetString(),
                                ThicknessMm   = ws.Cell(r, baseCol + 1).GetValue<double>(),
                                LayerFunction = ws.Cell(r, baseCol + 2).GetString(),
                                Priority      = ws.Cell(r, baseCol + 3).GetValue<int>() is int p && p > 0 ? p : 1,
                                IsVariable    = varCell.Equals("Yes", System.StringComparison.OrdinalIgnoreCase)
                                             || varCell == "1" || varCell.Equals("True", System.StringComparison.OrdinalIgnoreCase)
                            });
                        }

                        result.Add(new ImportPreviewItem
                        {
                            SheetName        = ws.Name,
                            TypeName         = typeName,
                            LayerCount       = layerCount,
                            TotalThicknessMm = thickness,
                            WrapsAtInserts   = string.IsNullOrWhiteSpace(wrapsAtInserts) ? "NoInsertWrap" : wrapsAtInserts,
                            WrapsAtEnds      = string.IsNullOrWhiteSpace(wrapsAtEnds)    ? "NoWrap"       : wrapsAtEnds,
                            Status           = existingNames.Contains(typeName) ? ImportStatus.Dup : ImportStatus.New,
                            Layers           = layers
                        });
                    }
                }
            }

            return result;
        }
    }
}
