// ==============================================
// File: AutoDeleteFailuresPreprocessor.cs
// Layer: Helpers
// ==============================================

using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.DwgToDetailLines.V010.Helpers
{
    /// <summary>
    /// Auto-resolves Revit's commit-time geometry failures (e.g. "Line is too
    /// short.", the "Error - cannot be ignored" dialog) by deleting the
    /// offending element(s) instead of surfacing Revit's blocking failure UI.
    ///
    /// WHY THIS IS NEEDED: the pre-filters in CadGeometryExtractor / RunLinePass
    /// (curve.Length &lt; doc.Application.ShortCurveTolerance) catch most
    /// degenerate curves before creation, but Revit's own COMMIT-TIME geometry
    /// validation for DetailCurve/FilledRegion elements can still reject a
    /// curve that passed that pre-filter (view-scale-dependent, or geometry
    /// that only becomes degenerate after Revit's internal processing). That
    /// rejection surfaces as a native FailureMessage at Transaction.Commit(),
    /// which a plain try/catch around NewDetailCurve/FilledRegion.Create does
    /// NOT catch (the API call itself succeeds; only Commit() fails) — hence
    /// this IFailuresPreprocessor, attached to the outer Transaction only
    /// (SubTransaction does not support SetFailureHandlingOptions).
    ///
    /// Scope (per Rafi, confirmed): broad — resolves ANY failure on this
    /// transaction that offers a "delete element(s)" resolution, not just
    /// short-curve failures specifically, via HasResolutionOfType /
    /// SetCurrentResolutionType / ResolveFailure. Deleted-element counts are
    /// handed back via DeletedElementCount for the caller to fold into
    /// ConversionMetrics.Skipped and log as [WARN], never shown as a dialog.
    /// </summary>
    public class AutoDeleteFailuresPreprocessor : IFailuresPreprocessor
    {
        public int DeletedElementCount { get; private set; }
        public List<string> ResolvedMessages { get; } = new();

        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            IList<FailureMessageAccessor> failures = failuresAccessor.GetFailureMessages();

            if (failures.Count == 0)
                return FailureProcessingResult.Continue;

            bool resolvedAny = false;

            foreach (FailureMessageAccessor failure in failures)
            {
                // Only auto-resolve failures that are actually resolvable by
                // deleting the offending element(s) — anything else (e.g. a
                // failure with no delete-elements resolution option) is left
                // for Revit's default handling so we don't silently swallow
                // something we can't safely fix.
                if (!failure.HasResolutionOfType(FailureResolutionType.DeleteElements))
                    continue;

                DeletedElementCount += failure.GetFailingElementIds().Count;
                ResolvedMessages.Add(failure.GetDescriptionText());

                failure.SetCurrentResolutionType(FailureResolutionType.DeleteElements);
                failuresAccessor.ResolveFailure(failure);

                resolvedAny = true;
            }

            return resolvedAny
                ? FailureProcessingResult.ProceedWithCommit
                : FailureProcessingResult.Continue;
        }
    }
}
