using System;
using System.Collections.Generic;
using Revit26_Plugin.Shared.Models; // LogLevel — shared enum, not redefined here

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA003.Core.Models
{
    /// <summary>
    /// Single skipped/failed element entry for the processing log and error report.
    /// </summary>
    public class ProcessingError
    {
        public long ElementId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public LogLevel Level { get; set; } = LogLevel.Warning;
    }

    /// <summary>
    /// Aggregate result of a full "Create Detail Lines" run, backing the metrics
    /// cards at the top of the window (Elements Found / Processed / Lines Created /
    /// Skipped / Errors). Populated by the (Phase 2+) processing engine; Phase 1
    /// only wires up the bindable properties on MainViewModel.
    /// </summary>
    public class ProcessingResult
    {
        public int ElementsFound { get; set; }
        public int ElementsProcessed { get; set; }
        public int DetailLinesCreated { get; set; }
        public int ElementsSkipped { get; set; }
        public int CriticalErrors { get; set; }
        public TimeSpan Duration { get; set; }
        public List<ProcessingError> Errors { get; set; } = new();
    }
}
