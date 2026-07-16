using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.PlanFromScopeBox.V001.Infrastructure.Helpers
{
    /// <summary>
    /// Targeted failure preprocessor. Deletes only the specific, known-benign warnings that
    /// can arise while creating plan views and placing viewports (never a blanket suppress).
    /// Everything else is left for Revit's default handling.
    ///
    /// Add specific FailureDefinitionId GUIDs to the allowlist as they are encountered in
    /// testing; keeping it targeted avoids masking real errors.
    /// </summary>
    public sealed class FailurePreprocessor : IFailuresPreprocessor
    {
        private readonly HashSet<System.Guid> _allow;

        public FailurePreprocessor(IEnumerable<System.Guid> allowGuids = null)
        {
            _allow = allowGuids != null
                ? new HashSet<System.Guid>(allowGuids)
                : new HashSet<System.Guid>();
        }

        public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor)
        {
            IList<FailureMessageAccessor> failures = accessor.GetFailureMessages();
            bool deletedAny = false;

            foreach (FailureMessageAccessor f in failures)
            {
                // Only delete warnings that are explicitly allowlisted.
                if (f.GetSeverity() == FailureSeverity.Warning &&
                    _allow.Contains(f.GetFailureDefinitionId().Guid))
                {
                    accessor.DeleteWarning(f);
                    deletedAny = true;
                }
            }

            return deletedAny
                ? FailureProcessingResult.ProceedWithCommit
                : FailureProcessingResult.Continue;
        }
    }
}
