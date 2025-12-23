// ============================================================
// File: WorkflowStats.cs
// Namespace: Revit26_Plugin.Creaser_V07.Commands
// ============================================================

namespace Revit26_Plugin.Creaser_V07.Commands
{
    internal sealed class WorkflowStats
    {
        // Step 1
        public int TotalCornersDetected { get; set; }
        public int CornersSkippedAlreadyDrain { get; set; }

        // Step 2
        public int TotalDrainCandidates { get; set; }
        public int UniqueDrains { get; set; }
        public int IgnoredDrainPoints { get; set; }

        // Step 3
        public int PathsFound { get; set; }
        public int PathsFailed { get; set; }
        public double AveragePathLength { get; set; }

        // Step 4
        public int TotalSegmentsExtracted { get; set; }

        // Step 5
        public int DuplicatesRemoved { get; set; }
        public int RemainingLinesAfterDedup { get; set; }

        // Step 6
        public int LinesReorderedOk { get; set; }
        public int LinesSkippedDegenerate { get; set; }

        // Step 7
        public int DetailLinesPlaced { get; set; }
    }
}
