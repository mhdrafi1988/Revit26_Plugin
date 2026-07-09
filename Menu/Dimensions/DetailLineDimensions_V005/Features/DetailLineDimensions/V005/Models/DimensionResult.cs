using System.Collections.Generic;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.DetailLIneDimensions.V005.Models
{
    /// <summary>
    /// Outcome of a single "Generate Dimensions" run.
    /// Placed   = dimension created successfully.
    /// Skipped  = per-item failure, silently skipped, transaction continues (Warning).
    /// Failed   = whole transaction rolled back (Error) — nothing placed.
    /// </summary>
    public class DimensionResult
    {
        public int Placed { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public List<LogEntry> Entries { get; } = new();
    }
}
