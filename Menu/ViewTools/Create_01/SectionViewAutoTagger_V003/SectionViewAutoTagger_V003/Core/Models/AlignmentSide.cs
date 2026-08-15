namespace Revit26_Plugin.SectionViewAutoTagger.V003
{
    /// <summary>
    /// Which side of the view crop boundary tags are aligned to.
    /// Drives both the anchor edge used by CropBoundaryHelper and the
    /// direction TagStackLayoutService stacks rows in.
    /// </summary>
    public enum AlignmentSide
    {
        Left,
        Right
    }
}
