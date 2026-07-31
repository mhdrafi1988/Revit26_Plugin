namespace Revit26_Plugin.SmartViewToSheetPlacer.V204.Models
{
    /// <summary>
    /// Persisted per-tool settings, stored at
    /// %AppData%\Revit26_Plugin\SmartViewToSheetPlacer\settings.json
    /// via System.Text.Json. Loaded on window open, saved on window close.
    /// Last-used Titleblock name, per-side Margins, and Gap values are remembered.
    /// </summary>
    public class SmartViewToSheetPlacerSettings
    {
        /// <summary>Name of the last-used titleblock type (matched by name on reload; falls back to first available if not found).</summary>
        public string? LastTitleblockName { get; set; }

        /// <summary>Last-used top margin value in mm. Defaults to 10 if not yet saved.</summary>
        public double LastMarginTopMm { get; set; } = 10.0;

        /// <summary>Last-used bottom margin value in mm. Defaults to 10 if not yet saved.</summary>
        public double LastMarginBottomMm { get; set; } = 10.0;

        /// <summary>Last-used left margin value in mm. Defaults to 10 if not yet saved.</summary>
        public double LastMarginLeftMm { get; set; } = 10.0;

        /// <summary>Last-used right margin value in mm. Defaults to 10 if not yet saved.</summary>
        public double LastMarginRightMm { get; set; } = 10.0;

        /// <summary>Last-used horizontal gap between views in mm. Defaults to 5 if not yet saved.</summary>
        public double LastGapHorizontalMm { get; set; } = 5.0;

        /// <summary>Last-used vertical gap between views (rows) in mm. Defaults to 5 if not yet saved.</summary>
        public double LastGapVerticalMm { get; set; } = 5.0;
    }
}
