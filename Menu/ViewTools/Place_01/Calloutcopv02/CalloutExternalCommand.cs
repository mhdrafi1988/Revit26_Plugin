using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Revit22_Plugin.copv2
{
    [Transaction(TransactionMode.Manual)]
    public class CalloutExternalCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiapp = commandData.Application;
                UIDocument uidoc = uiapp.ActiveUIDocument;
                Document doc = uidoc.Document;

                if (doc == null)
                {
                    TaskDialog.Show("Error", "No active Revit document found.");
                    return Result.Failed;
                }

                // Launch main window
                var win = new Views.CalloutViewWindow(uiapp, doc);
                win.ShowDialog();

                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                TaskDialog.Show("Error", $"Callout Tool failed:\n{ex.Message}");
                return Result.Failed;
            }
        }
    }
}
