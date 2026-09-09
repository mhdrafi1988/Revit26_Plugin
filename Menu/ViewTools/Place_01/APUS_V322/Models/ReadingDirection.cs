// File: Models/ReadingDirection.cs
namespace Revit26_Plugin.APUS.V322.Models
{
    /// <summary>
    /// Primary/secondary axis order used to build reading order across
    /// section markers in the active plan view. Four preset combinations.
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
