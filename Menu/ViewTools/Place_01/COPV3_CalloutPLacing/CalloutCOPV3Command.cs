using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.Attributes;
using System;

namespace Revit22_Plugin.copv3
{
    [Transaction(TransactionMode.Manual)]
    public class CalloutCOPV3Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiapp = data.Application;
                UIDocument uidoc = uiapp.ActiveUIDocument;

                if (uidoc == null)
                {
                    TaskDialog.Show("Error", "No active Revit document found.");
                    return Result.Failed;
                }

                var win = new Views.CalloutCOPV3Window(uiapp, uidoc);
                win.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("COPV3 Error", ex.Message);
                return Result.Failed;
            }
        }
    }
}
