using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit22_Plugin.callout.Views;

namespace Revit22_Plugin.callout
{
    [Transaction(TransactionMode.Manual)]
    public class CalloutExternalCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Get the Revit application and document
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            // Launch the callout window as a modal dialog
            var window = new SectionViewWindow(uiapp, doc);
            window.ShowDialog();

            return Result.Succeeded;
        }
    }
}
