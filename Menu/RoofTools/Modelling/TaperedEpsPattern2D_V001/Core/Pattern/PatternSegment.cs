namespace Revit26_Plugin_Revised.TaperedEpsPattern2D.V001.Core.Pattern
{
    /// <summary>One line to be drawn as a detail line, tagged with its layer.</summary>
    public sealed class PatternSegment
    {
        public PatternSegment(PatternLayer layer, Segment2D segment, double? thicknessMm = null)
        {
            Layer = layer;
            Segment = segment;
            ThicknessMm = thicknessMm;
        }

        public PatternLayer Layer { get; }
        public Segment2D Segment { get; }

        /// <summary>Contour level for contour segments, otherwise null.</summary>
        public double? ThicknessMm { get; }
    }
}
