using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using Revit26_Plugin.SDRV4.Views;
using Revit26_Plugin.SDRV4.Services;
namespace Revit26_Plugin.SDRV4.commands
{
    [Transaction(TransactionMode.Manual)]
    public class BubbleRenumberCommandV4 : IExternalCommand
    {
        public Result Execute(ExternalCommandData c, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = c.Application.ActiveUIDocument;
                if (uidoc == null)
                {
                    TaskDialog.Show("Error", "No active document.");
                    return Result.Failed;
                }

                var win = new BubbleRenumberWindowV4(uidoc, c.Application);
                win.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
                return Result.Failed;
            }
        }
    }
}
