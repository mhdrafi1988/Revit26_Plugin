using Autodesk.Revit.DB;

namespace Revit26_Plugin.RoofDrainCalloutPlacing.VByDrain.V004.Services
{
    /// <summary>
    /// Registered on every transaction (callout creation).
    /// Suppresses expected/non-critical warnings so the UI never blocks with a
    /// modal Revit dialog mid-run. Real errors still fail the transaction.
    /// </summary>
    public class RoofDrainFailuresPreprocessor : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            var failures = failuresAccessor.GetFailureMessages();
            foreach (var failure in failures)
            {
                var severity = failure.GetSeverity();

                if (severity == FailureSeverity.Warning)
                {
                    failuresAccessor.DeleteWarning(failure);
                }
                else if (severity == FailureSeverity.Error)
                {
                    // Let resolvable errors attempt their default resolution;
                    // unresolvable ones will still roll back the transaction.
                    if (failure.HasResolutions())
                        failuresAccessor.ResolveFailure(failure);
                }
            }
            return FailureProcessingResult.Continue;
        }
    }
}
