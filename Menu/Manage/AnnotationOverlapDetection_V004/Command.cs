using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AnnotationOverlapDetection.V004.Helpers;
using Revit26_Plugin.AnnotationOverlapDetection.V004.ViewModels;
using Revit26_Plugin.AnnotationOverlapDetection.V004.Views;
using Revit26_Plugin.Utilities;

namespace Revit26_Plugin.AnnotationOverlapDetection.V004
{
    /// <summary>
    /// Annotation Overlap Detection: finds overlapping annotations in the active 2D view.
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    [Regeneration(RegenerationOption.Manual)]
    public class Command : IExternalCommand
    {
        /// <inheritdoc />
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
            => global::Revit26_Plugin.Shared.Services.ToolGuard.RunCommand(GetType(), commandData, ref message, elements, ExecuteUnguarded);

        private Result ExecuteUnguarded(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                return ExecuteInternal(commandData, ref message, elements);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (System.Exception ex)
            {
                Logger.Error("Command", ex);
                message = ex.Message;
                TaskDialog.Show("Annotation Overlap Detection", $"An unexpected error occurred: {ex.Message}");
                return Result.Failed;
            }
        }

        private Result ExecuteInternal(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc.Document;
            View activeView = uiDoc.ActiveView;

            // Step 1: view validation - exit cleanly if the view is not a 2D view
            if (!OverlapDetectionHelper.IsSupportedView(activeView))
            {
                TaskDialog.Show("Annotation Overlap Detection",
                    "Please run this command from a plan, section, elevation, detail, drafting or legend view.");
                return Result.Cancelled;
            }

            // External events must be created here in Execute(), not from a UI event handler
            var zoomHandler = new ZoomToElementEventHandler();
            ExternalEvent zoomEvent = ExternalEvent.Create(zoomHandler);
            var refreshHandler = new RefreshAnnotationsEventHandler();
            ExternalEvent refreshEvent = ExternalEvent.Create(refreshHandler);

            var viewModel = new AnnotationOverlapViewModel(doc, activeView, zoomEvent, zoomHandler,
                refreshEvent, refreshHandler);

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
