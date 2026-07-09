// File: AutoSlopeDrainResult.cs
// Location: Core/Models/
// Plain result object returned by AutoSlopeDrainEngine. No UI/WPF references.

namespace Revit26_Plugin.AutoSlopeByDrain.V004.Core.Models
{
    public class AutoSlopeDrainResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }

        public int VerticesModified { get; set; }
        public int DrainCount { get; set; }
        public double HighestElevation_mm { get; set; }
        public double LongestPath_m { get; set; }
        public int RunDuration_sec { get; set; }
        public string RunDate { get; set; }

        /// <summary>Parameter-write status code: 1=OK, 2=Partial, 3=Failed.</summary>
        public int Status { get; set; }

        /// <summary>Path of the detailed vertex CSV, or null if export was skipped/failed.</summary>
        public string ExportedDetailedFilePath { get; set; }

        /// <summary>Path of the summary CSV, or null if export was skipped/failed.</summary>
        public string ExportedSummaryFilePath { get; set; }
    }
}
