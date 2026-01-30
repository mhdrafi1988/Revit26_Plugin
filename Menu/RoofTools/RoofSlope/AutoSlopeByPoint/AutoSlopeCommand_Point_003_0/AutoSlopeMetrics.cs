namespace Revit26_Plugin.AutoSlopeByPoint.Engines
{
    public class AutoSlopeMetrics
    {
        public int TotalVertices = 0;
        public int TopVertices = 0;
        public int ProcessedVertices = 0;
        public int SkippedVertices = 0;

        public double LongestPath = 0.0;

        public double MaxElevation = 0.0;
        public double MinElevation = double.MaxValue;

        public double GraphBuildTime = 0.0;
        public double PathComputeTime = 0.0;
        public double SlopeApplyTime = 0.0;
        public double TotalTime = 0.0;
    }
}
