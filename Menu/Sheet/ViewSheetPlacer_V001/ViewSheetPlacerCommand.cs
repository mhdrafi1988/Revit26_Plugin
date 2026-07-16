using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Revit26_Plugin.Tools.ViewSheetPlacer
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public sealed class ViewSheetPlacerCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                if (uidoc == null)
                {
                    message = "No active document.";
                    return Result.Failed;
                }

                Document doc = uidoc.Document;

                // Read-only scan up front (API context).
                ViewScan scan = ViewCollector.Scan(doc);

                var handler = new ViewSheetPlacerHandler();
                ExternalEvent evt = ExternalEvent.Create(handler);

                var vm = new ViewSheetPlacerViewModel(doc, scan, evt, handler);
                var window = new ViewSheetPlacerView(vm);

                // Modeless so the external event can run against the live doc.
                window.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
