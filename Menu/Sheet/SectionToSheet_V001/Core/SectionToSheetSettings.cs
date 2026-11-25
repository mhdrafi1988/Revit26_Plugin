#nullable enable
namespace Revit26_Plugin.SectionToSheet.V001.Core
{
    /// <summary>Where views go once the start sheet is full.</summary>
    public enum OverflowMode
    {
        /// <summary>Create new sheets, numbered from the setup's prefix and start number.</summary>
        NewSheets,
        /// <summary>Use the picked existing sheets in order, then new sheets when they run out.</summary>
        ExistingSheets
    }

    /// <summary>What to do with a target sheet that already has views on it.</summary>
    public enum ExistingContentMode
    {
        /// <summary>Place the sections below the lowest existing view.</summary>
        StackBelow,
        /// <summary>Do not use a sheet that already has views; go to the next one.</summary>
        SkipSheet,
        /// <summary>Repack the sheet's existing views together with the sections.</summary>
        RearrangeAll
    }

    /// <summary>
    /// Saved setup of Section To Sheet (%AppData%\Revit26_Plugin\SectionToSheet_V001\settings.json).
    /// <see cref="Version"/> is the file-format version; unknown/missing fields fall back to defaults.
    /// </summary>
    public class SectionToSheetSettings
    {
        /// <summary>Settings file-format version.</summary>
        public int Version { get; set; } = 1;
        /// <summary>Last used titleblock ("Family - Type").</summary>
        public string TitleblockName { get; set; } = string.Empty;
        /// <summary>Include sections that are already on a sheet (they are moved).</summary>
        public bool IncludePlaced { get; set; } = true;
        /// <summary>Overflow mode.</summary>
        public OverflowMode Overflow { get; set; } = OverflowMode.NewSheets;
        /// <summary>Existing-content mode.</summary>
        public ExistingContentMode ExistingContent { get; set; } = ExistingContentMode.StackBelow;
        /// <summary>New sheet number prefix.</summary>
        public string SheetNumberPrefix { get; set; } = "SEC-";
        /// <summary>New sheet start number; its digit count sets the zero padding ("001").</summary>
        public string SheetNumberStart { get; set; } = "001";
        /// <summary>New sheet name; empty means "Sections - &lt;plan name&gt;".</summary>
        public string SheetName { get; set; } = string.Empty;
        /// <summary>Layout algorithm name (Section Placer's RowFillStrategy).</summary>
        public string FillStrategy { get; set; } = "MaxFill";
        /// <summary>Top margin, mm.</summary>
        public double MarginTopMm { get; set; } = 10.0;
        /// <summary>Bottom margin, mm.</summary>
        public double MarginBottomMm { get; set; } = 10.0;
        /// <summary>Left margin, mm.</summary>
        public double MarginLeftMm { get; set; } = 10.0;
        /// <summary>Right margin, mm.</summary>
        public double MarginRightMm { get; set; } = 10.0;
        /// <summary>Horizontal gap between views, mm.</summary>
        public double GapHorizontalMm { get; set; } = 5.0;
        /// <summary>Vertical gap between views, mm.</summary>
        public double GapVerticalMm { get; set; } = 5.0;
    }
}
