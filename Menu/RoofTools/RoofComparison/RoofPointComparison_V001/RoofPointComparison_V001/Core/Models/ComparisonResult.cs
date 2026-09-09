using System.Collections.Generic;

namespace Revit26_Plugin.RoofPointComparison.V001.Core.Models
{
    /// <summary>Output of RoofComparisonEngine.Compare — the full entry list plus rolled-up metrics.</summary>
    public class ComparisonResult
    {
        public List<PointComparisonEntry> Entries { get; set; } = new List<PointComparisonEntry>();
        public ComparisonMetrics Metrics { get; set; } = new ComparisonMetrics();

        public bool Success { get; set; } = true;
        public string ErrorMessage { get; set; }
    }
}
