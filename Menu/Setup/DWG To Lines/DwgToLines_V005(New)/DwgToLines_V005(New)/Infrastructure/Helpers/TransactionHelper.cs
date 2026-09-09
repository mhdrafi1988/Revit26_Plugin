using Autodesk.Revit.DB;
using System;

namespace Revit26_Plugin.DwgToLines.V005.Infrastructure.Helpers
{
    /// <summary>
    /// Lightweight helper for safe transaction execution with rollback on failure.
    /// </summary>
    public static class TransactionHelper
    {
        public static void Run(
            Document document,
            string transactionName,
            Action action)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            using Transaction tx = new Transaction(document, transactionName);

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
