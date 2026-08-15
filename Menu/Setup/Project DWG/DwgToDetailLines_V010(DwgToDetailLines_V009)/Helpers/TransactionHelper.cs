// ==============================================
// File: TransactionHelper.cs
// Layer: Helpers
// ==============================================

using Autodesk.Revit.DB;
using System;

namespace Revit26_Plugin.DwgToDetailLines.V010.Helpers
{
    /// <summary>
    /// Lightweight helper for safe transaction execution.
    /// </summary>
    public static class TransactionHelper
    {
        /// <summary>
        /// Runs an action inside a Revit transaction. A commit-time geometry
        /// failure (e.g. "Line is too short.") that Revit would otherwise
        /// show as a blocking "cannot be ignored" dialog is auto-resolved by
        /// deleting the offending element(s) instead — see
        /// AutoDeleteFailuresPreprocessor. The preprocessor instance is
        /// returned so the caller can read DeletedElementCount /
        /// ResolvedMessages afterward and fold them into metrics/log.
        /// </summary>
        public static AutoDeleteFailuresPreprocessor Run(
            Document document,
            string transactionName,
            Action action)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            using Transaction tx =
                new Transaction(document, transactionName);

            var preprocessor = new AutoDeleteFailuresPreprocessor();

            tx.Start();

            FailureHandlingOptions opts = tx.GetFailureHandlingOptions()
                .SetFailuresPreprocessor(preprocessor)
                .SetClearAfterRollback(true);
            tx.SetFailureHandlingOptions(opts);

            try
            {
                action();
                tx.Commit();
            }
            catch
            {
                if (tx.HasStarted() && !tx.HasEnded())
                    tx.RollBack();
                throw;
            }

            return preprocessor;
        }
    }
}
