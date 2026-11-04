using System.Collections.Generic;
using System.Linq;
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
    /// Annotation Overlap Detection: finds overlapping annotations in the active 2D view,
    /// or in the views placed on the active sheet. Manual transaction mode (v4.2.0) only so
    /// leaders can be removed inside a transaction that is always rolled back; the model is never changed.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
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

            // Step 1: view validation - a supported 2D view, or a sheet (v4.2.0: its placed views)
            var startScope = new List<View>();
            if (activeView is ViewSheet sheet)
            {
                startScope.AddRange(sheet.GetAllPlacedViews()
                    .Select(id => doc.GetElement(id) as View)
                    .Where(OverlapDetectionHelper.IsSupportedView));
                if (startScope.Count == 0)
                {
                    TaskDialog.Show("Annotation Overlap Detection",
                        "This sheet has no plan, section, elevation, detail, drafting or legend views.");
                    return Result.Cancelled;
                }
            }
            else if (OverlapDetectionHelper.IsSupportedView(activeView))
            {
                startScope.Add(activeView);
            }
            else
            {
                TaskDialog.Show("Annotation Overlap Detection",
                    "Please run this command from a plan, section, elevation, detail, drafting or legend view, or a sheet.");
                return Result.Cancelled;
            }

            // External events must be created here in Execute(), not from a UI event handler
            var zoomHandler = new ZoomToElementEventHandler();
            ExternalEvent zoomEvent = ExternalEvent.Create(zoomHandler);
            var refreshHandler = new RefreshAnnotationsEventHandler();
            ExternalEvent refreshEvent = ExternalEvent.Create(refreshHandler);

            var viewModel = new AnnotationOverlapViewModel(doc, startScope, zoomEvent, zoomHandler,
                refreshEvent, refreshHandler);

            // v4.2.0: the window still opens with no annotations, so Batch Check stays reachable.
            if (viewModel.AnnotationFamilies.Count == 0)
            {
                TaskDialog.Show("Annotation Overlap Detection",
                    "No annotations in this view. You can still use Batch Check to pick other sheets or views.");
            }

            var panel = new AnnotationOverlapPanel(viewModel);
            panel.Show(); // modeless, so the user can still interact with the Revit model/view for zoom

            return Result.Succeeded;
        }
    }
}
