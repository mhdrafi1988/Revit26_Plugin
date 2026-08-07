using System.Collections.Generic;

namespace Revit26_Plugin.PlanFromScopeBox.V002.Core.Services
{
    /// <summary>One item to be packed: its printed footprint on the sheet, in millimetres.</summary>
    public sealed class PackItem
    {
        public long ScopeBoxId { get; init; }
        public string ScopeBoxName { get; init; } = string.Empty;

        /// <summary>Resolved view number (detail number), drawn in Phase 1 alongside the view
        /// name so {Increment} in both stays in sync; applied to the viewport in Phase 2.</summary>
        public string ViewNumber { get; init; } = string.Empty;

        public double FootprintWmm { get; init; }
        public double FootprintHmm { get; init; }

        /// <summary>True if this item alone exceeds the usable area - gets its own sheet.</summary>
        public bool Oversize { get; init; }
    }

    /// <summary>A placed viewport slot on a sheet: item + centre position in mm from sheet origin.</summary>
    public sealed class PackSlot
    {
        public PackItem Item { get; init; } = null!;
        public double CentreXmm { get; init; }
        public double CentreYmm { get; init; }
    }

    /// <summary>A single sheet with its packed slots.</summary>
    public sealed class PackedSheet
    {
        public List<PackSlot> Slots { get; } = new();
        public bool IsOversizeSheet { get; init; }
    }

    /// <summary>
    /// Simple shelf/row bin-packer. Lays items left-to-right, wrapping to new rows, then new
    /// sheets. Oversized items each get a dedicated sheet (centred). Usable area is supplied
    /// by the caller from <see cref="SheetLayout.Usable"/> so preview and actual packing agree.
    /// </summary>
    public sealed class SheetBinPacker
    {
        private readonly double _usableWmm;
        private readonly double _usableHmm;
        private readonly double _gutterMm;

        public SheetBinPacker(double usableWidthMm, double usableHeightMm, double gutterMm)
        {
            _usableWmm = usableWidthMm;
            _usableHmm = usableHeightMm;
            _gutterMm = gutterMm;
        }

        /// <summary>
        /// Packs the given items into sheets. Non-oversize items are shelf-packed; oversize
        /// items each receive their own centred sheet. Order is preserved.
        /// </summary>
        public List<PackedSheet> Pack(IReadOnlyList<PackItem> items)
        {
            var sheets = new List<PackedSheet>();

            PackedSheet current = null;
            double cursorX = 0, cursorY = 0, rowH = 0;

            void NewSheet()
            {
                current = new PackedSheet();
                sheets.Add(current);
                cursorX = 0; cursorY = 0; rowH = 0;
            }

            foreach (PackItem item in items)
            {
                if (item.Oversize)
                {
                    var sheet = new PackedSheet { IsOversizeSheet = true };
                    sheet.Slots.Add(new PackSlot
                    {
                        Item = item,
                        CentreXmm = _usableWmm / 2.0,
                        CentreYmm = _usableHmm / 2.0
                    });
                    sheets.Add(sheet);
                    continue;
                }

                if (current == null) NewSheet();

                // Wrap to next row if this item won't fit on the current row.
                if (cursorX + item.FootprintWmm > _usableWmm)
                {
                    cursorX = 0;
                    cursorY += rowH + _gutterMm;
                    rowH = 0;
                }

                // Wrap to a new sheet if the row won't fit vertically.
                if (cursorY + item.FootprintHmm > _usableHmm)
                    NewSheet();

                double centreX = cursorX + item.FootprintWmm / 2.0;
                double centreY = _usableHmm - (cursorY + item.FootprintHmm / 2.0); // top-down

                current.Slots.Add(new PackSlot
                {
                    Item = item,
                    CentreXmm = centreX,
                    CentreYmm = centreY
                });

                cursorX += item.FootprintWmm + _gutterMm;
                if (item.FootprintHmm > rowH) rowH = item.FootprintHmm;
            }

            return sheets;
        }
    }
}
