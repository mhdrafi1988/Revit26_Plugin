using Autodesk.Revit.DB;
using System;

namespace Revit26_Plugin.V5_00.Infrastructure.Revit
{
    public static class RevitTransactionService
    {
        public static void Run(
            Document doc,
            string transactionName,
            Action action)
        {
            using Transaction tx = new Transaction(doc, transactionName);
            tx.Start();

            try
            {
                action();
                tx.Commit();
            }
            catch
            {
                tx.RollBack();
                throw;
            }
        }
    }
}
