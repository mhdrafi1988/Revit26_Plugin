using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace Revit22_Plugin.SDRV3
{
    [Transaction(TransactionMode.Manual)]
    public class BubbleRenumberCommandV3 : IExternalCommand
    {
        public Result Execute(ExternalCommandData c, ref string message, ElementSet elements)
        {
            UIDocument uidoc = c.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Failed;

            var win = new BubbleRenumberWindowV3(uidoc, c.Application);
            win.Show();   // 🔥 No dialog, no blocking

            return Result.Succeeded;
        }
    }
}
