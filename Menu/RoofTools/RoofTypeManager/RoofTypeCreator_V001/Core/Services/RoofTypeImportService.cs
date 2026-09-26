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

            using (var wb = XLWorkbook.OpenReadOnly(filePath))
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

                        string function  = ws.Cell(r, 3).GetString();
                        int    layerCount = ws.Cell(r, 4).GetValue<int>();
                        double thickness  = ws.Cell(r, 5).GetValue<double>();

                        var layers = new List<LayerData>();
                        for (int li = 0; li < layerCount && li < 10; li++)
                        {
                            int baseCol = 6 + li * 3;
                            layers.Add(new LayerData
                            {
                                MaterialName  = ws.Cell(r, baseCol).GetString(),
                                ThicknessMm   = ws.Cell(r, baseCol + 1).GetValue<double>(),
                                LayerFunction = ws.Cell(r, baseCol + 2).GetString()
                            });
                        }

                        result.Add(new ImportPreviewItem
                        {
                            SheetName        = ws.Name,
                            TypeName         = typeName,
                            LayerCount       = layerCount,
                            TotalThicknessMm = thickness,
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
