using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.DetailLineClosedLoop.V001.Core.Models
{
    /// <summary>
    /// Outcome of one run of the trim/merge/close pipeline — drives both the
    /// metric cards and the run-summary line in the ViewModel.
    /// </summary>
    public class ProcessResult
    {
        public bool Success { get; set; }
        public int CurvesInLoop { get; set; }
        public int MergedCount { get; set; }
        public int RemovedCount { get; set; }
        public int GapsClosedCount { get; set; }
        public int FailedCount { get; set; }
        public string ErrorMessage { get; set; }
        public CurveLoop Loop { get; set; }
        public List<ElementId> CreatedElementIds { get; set; } = new();

        /// <summary>Final (post-uniquification) name of the Group created for the new lines, or null if Group New Lines was off/skipped.</summary>
        public string GroupName { get; set; }
    }
}
