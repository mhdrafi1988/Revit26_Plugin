namespace Revit26_Plugin.SmartViewToSheetPlacer.V212.Models
{
    /// <summary>
    /// One persisted per-ViewType-group gap setting entry. ViewType is stored
    /// by its string name (not the raw enum int) so settings.json stays
    /// human-readable and stable across any future Autodesk ViewType enum
    /// re-ordering. Matched back to a ViewType on load via Enum.Parse.
    /// </summary>
    public class PersistedGapSetting
    {
        public string ViewTypeName { get; set; } = string.Empty;
        public string GapStyle { get; set; } = "Fixed";
        public double HorizontalGapMm { get; set; } = 5.0;
        public double VerticalGapMm { get; set; } = 5.0;
    }

    /// <summary>
    /// Persisted per-tool settings, stored at
    /// %AppData%\Revit26_Plugin\SmartViewToSheetPlacer\settings.json
    /// via System.Text.Json. Loaded on window open, saved on window close.
    /// Last-used Titleblock name, per-side Margins, and per-ViewType-group
    /// gap settings are remembered.
    /// V212: LastGapHorizontalMm/LastGapVerticalMm (single global pair)
    /// replaced with GapSettingsByType (list of PersistedGapSetting) —
    /// confirmed with Rafi: per-group gap style/values persist across
    /// sessions, same restore-by-match pattern as the View Type filter
    /// checkbox state.
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

        /// <summary>Last-used per-ViewType-group gap style + H/V values. One entry
        /// per ViewType the user has ever configured; matched back by ViewTypeName
        /// on load, same pattern as View Type filter checkbox persistence.</summary>
        public System.Collections.Generic.List<PersistedGapSetting> GapSettingsByType { get; set; } = new();

        /// <summary>Last-used global reading-order direction. Defaults to top-to-bottom, left-to-right.</summary>
        public string LastReadingDirection { get; set; } = "TopToBottom_LeftToRight";

        /// <summary>Last-used global row tiebreak. Defaults to XPosition.</summary>
        public string LastRowTiebreak { get; set; } = "XPosition";

        /// <summary>Last-used global row Y-tolerance in mm. Defaults to 500.</summary>
        public double LastYToleranceMm { get; set; } = 500.0;

        /// <summary>Whether Stage 5's Activity Log card was last left
        /// expanded or collapsed. Confirmed with Rafi: persisted across
        /// sessions (not just this session), restored on every future launch.
        /// V212 UPDATE: default changed to collapsed (false) — Rafi confirmed
        /// the log should start collapsed on a first-ever run, with the
        /// user's last choice remembered on every launch after that.</summary>
        public bool IsActivityLogExpanded { get; set; } = false;
    }
}
