// File: Models/MarkerSource.cs
namespace Revit26_Plugin.APUS_V321_01.Models
{
    /// <summary>
    /// Which point represents a section for reading-order purposes.
    /// </summary>
    public enum MarkerSource
    {
        /// <summary>Midpoint of the section's location curve (default).</summary>
        SectionCurveMidpoint,

        /// <summary>Center of the section view's crop box.</summary>
        CropBoxCenter,

        /// <summary>The view's own origin point.</summary>
        ViewOrigin
    }
}
