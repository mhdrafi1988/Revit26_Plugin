using Autodesk.Revit.DB;

namespace Revit26_Plugin.SectionViewAutoTagger.V004
{
    /// <summary>
    /// Outcome for one element's tag placement attempt.
    /// ASSUMPTION: Skipped = expected/benign (no tag family, untaggable
    /// category) → logs as Warning, never blocks the batch. Failed =
    /// unexpected exception during IndependentTag.Create() → logs as Error.
    /// </summary>
    public enum TagResultStatus
    {
        Placed,
        Skipped,
        Failed
    }

    /// <summary>Result of attempting to place a tag on a single element.</summary>
    public class TagResult
    {
        public ElementId ElementId { get; }
        public string CategoryName { get; }
        public string ViewName { get; }
        public TagResultStatus Status { get; }

        /// <summary>Human-readable reason, populated for Skipped/Failed (e.g. "No tag family loaded", exception message).</summary>
        public string Reason { get; }

        public TagResult(ElementId elementId, string categoryName, string viewName, TagResultStatus status, string reason = "")
        {
            ElementId = elementId;
            CategoryName = categoryName;
            ViewName = viewName;
            Status = status;
            Reason = reason;
        }
    }
}
