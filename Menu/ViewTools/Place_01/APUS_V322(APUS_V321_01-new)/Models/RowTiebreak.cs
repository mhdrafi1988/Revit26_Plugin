// File: Models/RowTiebreak.cs
namespace Revit26_Plugin.APUS.V322.Models
{
    /// <summary>
    /// Secondary sort applied to sections that land in the same row band
    /// (i.e. within Row Y-tolerance of each other).
    /// </summary>
    public enum RowTiebreak
    {
        /// <summary>Sort by marker X position along the row axis (default).</summary>
        XPosition,

        /// <summary>Sort alphabetically by section/view name.</summary>
        Name,

        /// <summary>Sort by rendered footprint size, largest first.</summary>
        Size
    }
}
