namespace Revit26_Plugin.PlanFromScopeBox.V003.Core.Models
{
    /// <summary>Outcome classification for a single scope-box -> plan/sheet operation.</summary>
    public enum PlanOutcome
    {
        /// <summary>Plan created and placed on a shared sheet normally.</summary>
        Placed,

        /// <summary>Plan created and placed on its own dedicated sheet due to oversize (Warning).</summary>
        PlacedOversize,

        /// <summary>Skipped - could not create or place (logged as Warning, run continues).</summary>
        Skipped
    }

    /// <summary>
    /// Result of processing one scope box: which view/sheet were produced and the outcome.
    /// Populated for EVERY processed row (M2) so summary metrics derive from a single source
    /// of truth rather than separately incremented counters.
    /// </summary>
    public sealed class PlanCreationResult
    {
        public string ScopeBoxName { get; init; } = string.Empty;
        public PlanOutcome Outcome { get; init; }

        /// <summary>Created view id (0 if none).</summary>
        public long ViewId { get; init; }

        /// <summary>Created view name (empty if none).</summary>
        public string ViewName { get; init; } = string.Empty;

        /// <summary>Created / target sheet number. Filled in during sheet placement, so
        /// this is mutable (set after the result row is first created in Phase 1).</summary>
        public string SheetNumber { get; set; } = string.Empty;

        /// <summary>Human-readable reason, used for Warning/skip log lines.</summary>
        public string Reason { get; init; } = string.Empty;
    }
}
