namespace Revit26_Plugin_Revised.TaperedEpsPattern2D.V006.Core.Pattern
{
    /// <summary>The part of the roof that drains to outline edge <see cref="EdgeIndex"/>.</summary>
    public sealed class SlopeFace
    {
        public SlopeFace(int edgeIndex, Polygon2D polygon)
        {
            EdgeIndex = edgeIndex;
            Polygon = polygon;
        }

        public int EdgeIndex { get; }
        public Polygon2D Polygon { get; }
    }
}
