namespace Revit26_Plugin.TaperedEpsPattern2D.V001.Core.Pattern
{
    /// <summary>All inputs of the 2D pattern engine. Lengths in metres, thickness in mm.</summary>
    public sealed record Pattern2DSettings
    {
        public const double MinBoardSizeM = 0.25;
        public const double MaxBoardSizeM = 1.00;

        /// <summary>Board size along the grid direction (m).</summary>
        public double BoardLengthM { get; init; } = 1.0;

        /// <summary>Board size across the grid direction (m).</summary>
        public double BoardWidthM { get; init; } = 1.0;

        public double SlopePercent { get; init; } = 1.0;
        public double MinThicknessMm { get; init; } = 20.0;

        public GridDirectionMode GridDirection { get; init; } = GridDirectionMode.LongestEdge;
        public GridOriginMode GridOrigin { get; init; } = GridOriginMode.BoundingBoxCorner;

        public bool DrawGrid { get; init; } = true;
        public bool DrawHipRidge { get; init; } = true;
        public bool DrawContours { get; init; } = true;
        public double ContourIntervalMm { get; init; } = 10.0;
        public LabelMode Labels { get; init; } = LabelMode.None;

        /// <summary>Segments shorter than this are dropped (m). The Revit layer raises it to ShortCurveTolerance.</summary>
        public double MinSegmentLengthM { get; init; } = GeometryTolerance.MinSegmentLength;

        public bool AnyLayerEnabled => DrawGrid || DrawHipRidge || DrawContours || Labels != LabelMode.None;
    }
}
