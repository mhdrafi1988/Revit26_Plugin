using Autodesk.Revit.DB;

namespace Revit26_Plugin.CalloutCOP.V017.Helpers
{
    /// <summary>
    /// Suppresses expected, non-critical warnings (e.g. overlapping crop regions)
    /// raised while committing reference-callout placement transactions.
    /// Registered per suite convention - was absent in V011.
    /// </summary>
    public class CalloutFailuresPreprocessor : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            var failures = failuresAccessor.GetFailureMessages();

            foreach (var failure in failures)
            {
                if (failure.GetSeverity() == FailureSeverity.Warning)
                    failuresAccessor.DeleteWarning(failure);
            }

            return FailureProcessingResult.Continue;
        }
    }
}
