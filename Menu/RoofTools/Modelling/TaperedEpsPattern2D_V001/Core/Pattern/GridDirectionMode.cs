namespace Revit26_Plugin_Revised.TaperedEpsPattern2D.V001.Core.Pattern
{
    public enum GridDirectionMode
    {
        /// <summary>Board length runs along the longest outline edge (tie → first edge CCW).</summary>
        LongestEdge,

        /// <summary>Board length runs along the project (internal) X axis.</summary>
        ProjectX
    }
}
