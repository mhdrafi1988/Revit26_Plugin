using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.RoofEdgeSections.V001
{
    /// <summary>
    /// Outcome of a single Run() pass — counts for the completion summary line
    /// plus the created view ids (for the optional "open views" step).
    /// </summary>
    public class RunResult
    {
        public int CreatedCount { get; set; }
        public int SkippedCount { get; set; }
        public int FailedCount { get; set; }

        /// <summary>ElementIds of successfully created ViewSection elements, in creation order.</summary>
        public List<ElementId> CreatedViewIds { get; set; } = new List<ElementId>();

        public string SummaryLine =>
            $"{CreatedCount} placed | {SkippedCount} skipped | {FailedCount} failed";
    }
}
