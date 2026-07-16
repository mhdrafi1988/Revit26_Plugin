using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.Tools.ViewSheetPlacer
{
    /// <summary>A viewport-sized rectangle to be packed onto a sheet.</summary>
    public sealed class PackItem
    {
        public ElementId ViewId { get; init; }
        public string ViewName { get; init; } = string.Empty;
        public double Width { get; set; }   // feet, as measured on the sheet
        public double Height { get; set; }  // feet

        /// <summary>Assigned centre point on the target sheet (set by the packer).</summary>
        public XYZ TargetCenter { get; set; } = XYZ.Zero;
    }

    /// <summary>One packed sheet: the items assigned to it with centres resolved.</summary>
    public sealed class PackedSheet
    {
        public List<PackItem> Items { get; } = new();
    }

    /// <summary>
    /// Grid (shelf) packer. Sorts largest-first, fills left→right / top→bottom,
    /// wraps to a new row, then to a new sheet when the usable area is exhausted.
    /// Deliberately isolated so the algorithm can be replaced later.
    /// </summary>
    public static class BinPacker
    {
        /// <param name="usable">Usable rectangle on the sheet (feet), bottom-left origin.</param>
        public static List<PackedSheet> Pack(
            IReadOnlyList<PackItem> items, UV usableMin, UV usableMax, double gap)
        {
            double left = usableMin.U, bottom = usableMin.V;
            double width = usableMax.U - usableMin.U;
            double height = usableMax.V - usableMin.V;
            double top = usableMax.V;

            var sorted = items
                .OrderByDescending(i => Math.Max(i.Width, i.Height))
                .ToList();

            var sheets = new List<PackedSheet>();
            var current = NewSheet(sheets);

            double cursorX = left;
            double rowTop = top;
            double rowMaxH = 0.0;

            foreach (var item in sorted)
            {
                double w = item.Width;
                double h = item.Height;

                // Wrap to a new row if this item overruns the right edge.
                if (cursorX > left && cursorX + w > left + width + 1e-6)
                {
                    rowTop -= rowMaxH + gap;
                    cursorX = left;
                    rowMaxH = 0.0;
                }

                // Move to a new sheet if the row would drop below the bottom edge.
                if (rowTop - h < bottom - 1e-6 && current.Items.Count > 0)
                {
                    current = NewSheet(sheets);
                    cursorX = left;
                    rowTop = top;
                    rowMaxH = 0.0;
                }

                double cx = cursorX + w / 2.0;
                double cy = rowTop - h / 2.0;
                item.TargetCenter = new XYZ(cx, cy, 0.0);
                current.Items.Add(item);

                cursorX += w + gap;
                rowMaxH = Math.Max(rowMaxH, h);
            }

            return sheets;
        }

        private static PackedSheet NewSheet(List<PackedSheet> sheets)
        {
            var s = new PackedSheet();
            sheets.Add(s);
            return s;
        }
    }
}
