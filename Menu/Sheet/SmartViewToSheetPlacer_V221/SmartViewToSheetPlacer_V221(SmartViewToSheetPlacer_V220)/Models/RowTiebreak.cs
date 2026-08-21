namespace Revit26_Plugin.SmartViewToSheetPlacer.V221.Models
{
    /// <summary>
    /// Secondary sort applied to views that land in the same row band
    /// (i.e. within Row Y-tolerance of each other).
    /// Ported from APUS_V321_01 (Models/RowTiebreak.cs) — same three values.
    /// "XPosition" here refers to each view's crop-box-center U coordinate
    /// (projected onto the view's Right direction), not a section marker.
    /// </summary>
    public enum RowTiebreak
    {
        /// <summary>Sort by crop-box-center U position along the row axis (default).</summary>
        XPosition,

        /// <summary>Sort alphabetically by view name.</summary>
        Name,

        /// <summary>Sort by computed size on sheet (WidthMm * HeightMm), largest first.</summary>
        Size
    }
}
