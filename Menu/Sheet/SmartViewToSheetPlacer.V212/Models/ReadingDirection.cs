namespace Revit26_Plugin.SmartViewToSheetPlacer.V212.Models
{
    /// <summary>
    /// Primary/secondary axis order used to build reading order across views
    /// within a ViewType group. Four preset combinations.
    /// Ported from APUS_V321_01 (Models/ReadingDirection.cs) — same four
    /// values, same semantics, applied here to Views instead of section
    /// markers.
    /// </summary>
    public enum ReadingDirection
    {
        /// <summary>Top to bottom, then left to right within each row band.</summary>
        TopToBottom_LeftToRight,

        /// <summary>Top to bottom, then right to left within each row band.</summary>
        TopToBottom_RightToLeft,

        /// <summary>Bottom to top, then left to right within each row band.</summary>
        BottomToTop_LeftToRight,

        /// <summary>Bottom to top, then right to left within each row band.</summary>
        BottomToTop_RightToLeft
    }
}
