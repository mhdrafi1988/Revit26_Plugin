namespace Revit26_Plugin_Revised.TaperedEpsPattern2D.V002.Core.Pattern
{
    public enum GridDirectionMode
    {
        /// <summary>Board length runs along the longest outline edge (tie → first edge CCW).</summary>
        LongestEdge,

        /// <summary>Board length runs along the project (internal) X axis.</summary>
        ProjectX,

        /// <summary>Board length runs parallel to the outline edge <c>Pattern2DSettings.AlignEdgeIndex</c>.</summary>
        SelectedEdge,

        /// <summary>
        /// Each slope face gets its own grid, parallel to (and starting flush on) the roof edge it
        /// drains to. Board rows on a face then have one thickness range each.
        /// </summary>
        EachEdge
    }
}
