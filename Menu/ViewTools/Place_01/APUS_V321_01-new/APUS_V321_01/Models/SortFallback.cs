// File: Models/SortFallback.cs
namespace Revit26_Plugin.APUS_V321_01.Models
{
    /// <summary>
    /// How to order sections whose marker source could not be resolved
    /// (e.g. LocationCurve is null, crop box unavailable, etc).
    /// </summary>
    public enum SortFallback
    {
        /// <summary>Unresolvable sections are placed after all resolvable ones (default).</summary>
        PushToEnd,

        /// <summary>Unresolvable sections are sorted alphabetically among themselves, still pushed to end.</summary>
        SortByName
    }
}
