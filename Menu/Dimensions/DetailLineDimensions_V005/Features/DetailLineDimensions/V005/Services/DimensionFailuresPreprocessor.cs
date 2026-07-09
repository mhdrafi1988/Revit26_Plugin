using Autodesk.Revit.DB;
using System.Linq;

namespace Revit26_Plugin.DetailLIneDimensions.V005.Services
{
    /// <summary>
    /// Suppresses expected/non-critical warnings during dimension creation so Revit's
    /// modal warning dialogs never block the transaction. Real errors still roll back.
    /// </summary>
    public class DimensionFailuresPreprocessor : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            var failures = failuresAccessor.GetFailureMessages().ToList();

            foreach (var failure in failures)
            {
                var severity = failure.GetSeverity();

                if (severity == FailureSeverity.Warning)
                {
                    failuresAccessor.DeleteWarning(failure);
                }
                else if (severity == FailureSeverity.Error)
                {
                    return FailureProcessingResult.ProceedWithRollBack;
                }
            }

            return FailureProcessingResult.Continue;
        }
    }
}
