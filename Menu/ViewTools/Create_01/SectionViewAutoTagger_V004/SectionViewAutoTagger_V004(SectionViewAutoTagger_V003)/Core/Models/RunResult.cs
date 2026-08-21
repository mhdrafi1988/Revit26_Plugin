using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.SectionViewAutoTagger.V004
{
    /// <summary>
    /// Aggregate outcome of a full batch Run across all worklist entries.
    /// Backs the completion summary line ("X placed | Y skipped | Z failed").
    /// </summary>
    public class RunResult
    {
        public IReadOnlyList<TagResult> Results { get; }

        public int PlacedCount => Results.Count(r => r.Status == TagResultStatus.Placed);
        public int SkippedCount => Results.Count(r => r.Status == TagResultStatus.Skipped);
        public int FailedCount => Results.Count(r => r.Status == TagResultStatus.Failed);

        public RunResult(IReadOnlyList<TagResult> results)
        {
            Results = results;
        }

        public override string ToString()
            => $"{PlacedCount} placed | {SkippedCount} skipped | {FailedCount} failed";
    }
}
