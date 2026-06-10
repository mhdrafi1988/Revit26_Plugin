using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using WorksetManager_01.Views;

namespace WorksetManager_01.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    [Regeneration(RegenerationOption.Manual)]
    public class WorksetManagerCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;

            if (uiDoc == null)
            {
                message = "No active document found.";
                return Result.Failed;
            }

            Document doc = uiDoc.Document;

            if (!doc.IsWorkshared)
            {
                TaskDialog.Show("Workset Manager",
                    "This document is not workshared. Workset Manager requires a workshared model.");
                return Result.Cancelled;
            }

            var window = new WorksetManagerWindow(doc);
            window.ShowDialog();

            return Result.Succeeded;
        }
    }
}
