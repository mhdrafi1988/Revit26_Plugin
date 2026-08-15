namespace Revit26_Plugin.DwgToDetailLines.V010.Models
{
    /// <summary>
    /// Distinguishes line-geometry layers from hatch/fill layers in the
    /// Layers &amp; Hatches grid. Drives grouping (Lines section / Hatches
    /// section) and which conversion path (DetailCurve vs FilledRegion)
    /// a layer's entities are routed through.
    /// </summary>
    public enum CadEntityType
    {
        Line,
        Hatch
    }
}
