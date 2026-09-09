namespace Revit26_Plugin.RoofEdgeElementSections.V001
{
    /// <summary>
    /// The 4 view-aligned bounding-box sides used to bucket roof boundary edges.
    /// "North" = top of the active view (view rotation), NOT Project/True North.
    /// Copied verbatim from RoofEdgeAroundSections_V004.
    /// </summary>
    public enum EdgeDirection
    {
        North,
        South,
        East,
        West
    }
}
