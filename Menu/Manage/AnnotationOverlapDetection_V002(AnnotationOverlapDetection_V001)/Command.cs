using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AnnotationOverlapDetection.V002.ViewModels;
using Revit26_Plugin.AnnotationOverlapDetection.V002.Views;

namespace Revit26_Plugin.AnnotationOverlapDetection.V002
{
    [Transaction(TransactionMode.ReadOnly)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc.Document;
            View activeView = uiDoc.ActiveView;

            // Step 1: view validation - exit cleanly, no error dialog, if not a plan view
            if (activeView.ViewType != ViewType.FloorPlan)
            {
                TaskDialog.Show("Annotation Overlap Detection",
                    "Please run this command from a floor plan view.");
                return Result.Cancelled;
            }

            // External event must be created here in Execute(), not from a UI event handler
            var zoomHandler = new ZoomToElementEventHandler();
            ExternalEvent zoomEvent = ExternalEvent.Create(zoomHandler);

            var viewModel = new AnnotationOverlapViewModel(doc, activeView, zoomEvent, zoomHandler);

            if (viewModel.AnnotationFamilies.Count == 0)
            {
                TaskDialog.Show("Annotation Overlap Detection", "No annotations in this view.");
                return Result.Cancelled;
            }

            var panel = new AnnotationOverlapPanel(viewModel);
            panel.Show(); // modeless, so the user can still interact with the Revit model/view for zoom

            return Result.Succeeded;
        }
    }
}
