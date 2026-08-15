// ==============================================
// File: TransactionHelper.cs
// Layer: Helpers
// ==============================================

using Autodesk.Revit.DB;
using System;

namespace Revit26_Plugin.DwgToDetailLines.V001.Helpers
{
    /// <summary>
    /// Lightweight helper for safe transaction execution.
    /// </summary>
    public static class TransactionHelper
    {
        /// <summary>
        /// Runs an action inside a Revit transaction.
        /// </summary>
        public static void Run(
            Document document,
            string transactionName,
            Action action)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            using Transaction tx =
                new Transaction(document, transactionName);

            tx.Start();

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
        }
    }
}
