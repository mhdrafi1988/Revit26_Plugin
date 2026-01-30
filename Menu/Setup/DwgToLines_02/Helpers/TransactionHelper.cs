using Autodesk.Revit.DB;
using System;

namespace Revit26_Plugin.DwgSymbolicConverter_V02.Helpers
{
    public static class TransactionHelper
    {
        public static void Run(Document doc, string name, Action action)
        {
            using var t = new Transaction(doc, name);
            t.Start();
            action();
            t.Commit();
        }
    }
}
