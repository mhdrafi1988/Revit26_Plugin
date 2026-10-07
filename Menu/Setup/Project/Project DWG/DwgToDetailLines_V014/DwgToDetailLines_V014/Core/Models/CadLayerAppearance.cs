namespace Revit26_Plugin.DwgToDetailLines.V014.Core.Models
{
    /// <summary>
    /// Colour, lineweight and pattern of a CAD layer or a Revit line style,
    /// as used by <see cref="Services.LineStyleMatcher"/> to pair the two (ADDED in V014).
    /// </summary>
    /// <param name="R">Red component (0–255).</param>
    /// <param name="G">Green component (0–255).</param>
    /// <param name="B">Blue component (0–255).</param>
    /// <param name="LineWeight">Projection lineweight number (1–16), or 0 when unknown.</param>
    /// <param name="IsSolid">True when the line pattern is solid.</param>
    public record CadLayerAppearance(byte R, byte G, byte B, int LineWeight, bool IsSolid);
}
