using Autodesk.Revit.DB;

namespace Revit26_Plugin.SmartViewToSheetPlacer.V212.Models
{
    /// <summary>
    /// Represents a titleblock family/type available for new sheets, along with
    /// its real usable placement area (family symbol bounding box, minus the
    /// user-specified margin on all sides). UsableWidthMm/UsableHeightMm are
    /// computed once when the titleblock is selected in Stage 1 and reused
    /// as-is by the packing algorithm in Stage 2.
    /// </summary>
    public class TitleblockOption
    {
        /// <summary>FamilySymbol ElementId for this titleblock type.</summary>
        public ElementId FamilySymbolId { get; }

        /// <summary>Display name, e.g. "A1 Metric - WAMI Standard".</summary>
        public string Name { get; }

        /// <summary>Full sheet width in mm, read from the FamilySymbol's bounding box.</summary>
        public double SheetWidthMm { get; }

        /// <summary>Full sheet height in mm, read from the FamilySymbol's bounding box.</summary>
        public double SheetHeightMm { get; }

        /// <summary>Usable width in mm (SheetWidthMm minus Left+Right margins).</summary>
        public double UsableWidthMm { get; private set; }

        /// <summary>Usable height in mm (SheetHeightMm minus Top+Bottom margins).</summary>
        public double UsableHeightMm { get; private set; }

        /// <summary>Top margin (mm) last applied via ApplyMargins. Used by the Sheet Preview
        /// canvas to draw the usable-area rectangle at the correct offset from the sheet edge.</summary>
        public double MarginTopMm { get; private set; }

        /// <summary>Left margin (mm) last applied via ApplyMargins.</summary>
        public double MarginLeftMm { get; private set; }

        public TitleblockOption(
            ElementId familySymbolId,
            string name,
            double sheetWidthMm,
            double sheetHeightMm)
        {
            FamilySymbolId = familySymbolId;
            Name = name;
            SheetWidthMm = sheetWidthMm;
            SheetHeightMm = sheetHeightMm;
        }

        /// <summary>
        /// Recomputes UsableWidthMm/UsableHeightMm from the current SheetWidthMm/
        /// SheetHeightMm using four independent margins (mm), one per side.
        /// Call this whenever the user changes any Margin field in Stage 1.
        /// </summary>
        public void ApplyMargins(double topMm, double bottomMm, double leftMm, double rightMm)
        {
            UsableWidthMm = System.Math.Max(0, SheetWidthMm - (leftMm + rightMm));
            UsableHeightMm = System.Math.Max(0, SheetHeightMm - (topMm + bottomMm));
            MarginTopMm = topMm;
            MarginLeftMm = leftMm;
        }

        public override string ToString() => Name;
    }
}
