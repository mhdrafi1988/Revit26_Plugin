using System;
using System.Collections.Generic;
using ClosedXML.Excel;
using Autodesk.Revit.DB;
using Revit26_Plugin.RoofTypeCreator.V001.Core.Models;

namespace Revit26_Plugin.RoofTypeCreator.V001.Core.Services
{
    public static class RoofTypeExportService
    {
        private const int MaxLayers = 10;

        public static void Export(IEnumerable<RoofTypeItem> items, string filePath, Document doc)
        {
            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Roof Types");

                // ── Header row ───────────────────────────────────
                int col = 1;
                ws.Cell(1, col++).Value = "Type Name";
                ws.Cell(1, col++).Value = "Type Mark";
                ws.Cell(1, col++).Value = "Function";
                ws.Cell(1, col++).Value = "Layer Count";
                ws.Cell(1, col++).Value = "Total Thickness (mm)";
                ws.Cell(1, col++).Value = "Wraps At Inserts";
                ws.Cell(1, col++).Value = "Wraps At Ends";
                for (int li = 1; li <= MaxLayers; li++)
                {
                    ws.Cell(1, col++).Value = $"Layer {li} — Material";
                    ws.Cell(1, col++).Value = $"Layer {li} — Thickness (mm)";
                    ws.Cell(1, col++).Value = $"Layer {li} — Function";
                    ws.Cell(1, col++).Value = $"Layer {li} — Priority";
                    ws.Cell(1, col++).Value = $"Layer {li} — Variable";
                }

                var hdr = ws.Row(1);
                hdr.Style.Font.Bold            = true;
                hdr.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
                hdr.Style.Font.FontColor       = XLColor.White;

                // ── Data rows ────────────────────────────────────
                int row = 2;
                foreach (var item in items)
                {
                    col = 1;
                    ws.Cell(row, col++).Value = item.TypeName;
                    ws.Cell(row, col++).Value = item.TypeMark;
                    ws.Cell(row, col++).Value = item.Function;
                    ws.Cell(row, col++).Value = item.LayerCount;
                    ws.Cell(row, col++).Value = item.TotalThicknessMm;

                    var rt = doc.GetElement(item.TypeId) as RoofType;
                    var cs = rt?.GetCompoundStructure();
                    if (cs != null)
                    {
                        ws.Cell(row, col++).Value = cs.OpeningWrapping.ToString();
                        ws.Cell(row, col++).Value = cs.EndWrapping.ToString();

                        int varIdx = cs.VariableLayerIndex;
                        for (int li = 0; li < Math.Min(cs.LayerCount, MaxLayers); li++)
                        {
                            var layer  = cs.GetLayer(li);
                            string mat = layer.MaterialId != ElementId.InvalidElementId
                                ? (doc.GetElement(layer.MaterialId) as Material)?.Name ?? ""
                                : "";
                            double thick = Math.Round(
                                UnitUtils.ConvertFromInternalUnits(layer.Width, UnitTypeId.Millimeters), 1);

                            ws.Cell(row, col++).Value = mat;
                            ws.Cell(row, col++).Value = thick;
                            ws.Cell(row, col++).Value = layer.Function.ToString();
                            ws.Cell(row, col++).Value = layer.Priority;
                            ws.Cell(row, col++).Value = (li == varIdx) ? "Yes" : "No";
                        }
                    }
                    else
                    {
                        col += 2; // skip wraps columns when no compound structure
                    }

                    if (row % 2 == 0)
                        ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F0F4F8");

                    row++;
                }

                ws.Columns().AdjustToContents();
                ws.SheetView.FreezeRows(1);
                wb.SaveAs(filePath);
            }
        }
    }
}
