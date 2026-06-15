using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using AutoSlopeByPointTwoSlopes_01_00.UI.ViewModels;
using AutoSlopeByPointTwoSlopes_01_00.UI.Views;
using AutoSlopeByPointTwoSlopes_01_00.Infrastructure.Helpers;
using AutoSlopeByPointTwoSlopes_01_00.Infrastructure.ExternalEvents;
using System;
using System.Collections.Generic;

namespace AutoSlopeByPointTwoSlopes_01_00.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class AutoSlopeCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string msg, ElementSet elems)
        {
            UIDocument uidoc = data.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // Validate active view is a plan view
            if (!ViewValidationHelper.IsPlanView(doc.ActiveView))
            {
                TaskDialog.Show("AutoSlope",
                    "Please switch to a Floor Plan, Ceiling Plan, Area Plan, or Engineering Plan view before running AutoSlope.\n\n" +
                    "Vertex selection requires a plan view for proper point selection.");
                return Result.Cancelled;
            }

            // Step 1: Select roof
            Reference roofRef = null;
            try
            {
                roofRef = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    new RoofSelectionFilter(),
                    "Select a footprint roof to slope");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            if (roofRef == null)
            {
                TaskDialog.Show("AutoSlope", "No roof selected.");
                return Result.Cancelled;
            }

            RoofBase roof = doc.GetElement(roofRef) as RoofBase;
            if (roof == null)
            {
                TaskDialog.Show("AutoSlope", "Selected element is not a valid roof.\n\nPlease select a footprint roof.");
                return Result.Cancelled;
            }

            // Check if roof supports shape editing
            bool supportsShapeEditing = false;
            try
            {
                SlabShapeEditor testEditor = roof.GetSlabShapeEditor();
                supportsShapeEditing = testEditor != null;
            }
            catch
            {
                supportsShapeEditing = false;
            }

            if (!supportsShapeEditing)
            {
                TaskDialog.Show("AutoSlope",
                    "The selected roof does not support shape editing.\n\n" +
                    "Please select a footprint roof (not an extrusion roof).");
                return Result.Cancelled;
            }

            // Step 2: Enable shape editing & reset vertices to zero
            using (Transaction enableTx = new Transaction(doc, "AutoSlope - Enable Shape Editing"))
            {
                enableTx.Start();
                try
                {
                    SlabShapeEditor editor = roof.GetSlabShapeEditor();
                    if (editor == null)
                    {
                        TaskDialog.Show("AutoSlope", "Cannot access roof shape editor.");
                        enableTx.RollBack();
                        return Result.Failed;
                    }

                    if (!editor.IsEnabled)
                        editor.Enable();

                    enableTx.Commit();
                }
                catch (Exception ex)
                {
                    enableTx.RollBack();
                    TaskDialog.Show("AutoSlope",
                        "Cannot enable shape editing on this roof.\n\n" +
                        $"Error: {ex.Message}\n\n" +
                        "Make sure this is a footprint roof that supports shape editing.");
                    return Result.Failed;
                }
            }

            using (Transaction resetTx = new Transaction(doc, "AutoSlope - Reset Roof Vertices"))
            {
                resetTx.Start();
                try
                {
                    SlabShapeEditor editor = roof.GetSlabShapeEditor();
                    if (editor == null || !editor.IsEnabled)
                    {
                        TaskDialog.Show("AutoSlope", "Shape editor not available after enable.");
                        resetTx.RollBack();
                        return Result.Failed;
                    }

                    foreach (SlabShapeVertex vertex in editor.SlabShapeVertices)
                    {
                        if (vertex != null && vertex.IsValidObject)
                            editor.ModifySubElement(vertex, 0);
                    }
                    resetTx.Commit();
                }
                catch (Exception ex)
                {
                    resetTx.RollBack();
                    TaskDialog.Show("AutoSlope",
                        "Failed to reset roof vertices to zero elevation.\n\n" +
                        $"Error: {ex.Message}");
                    return Result.Failed;
                }
            }

            // Step 3: Pick DRAIN POINTS
            IList<Reference> drainPicks = null;
            try
            {
                drainPicks = uidoc.Selection.PickObjects(
                    ObjectType.PointOnElement,
                    new DrainPointSelectionFilter(),
                    "Pick DRAIN POINTS on the roof surface. Click Finish when done.");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            var drains = new List<XYZ>();
            if (drainPicks != null)
            {
                foreach (var r in drainPicks)
                {
                    if (r != null && r.GlobalPoint != null)
                        drains.Add(r.GlobalPoint);
                }
            }

            // ─────────────────────────────────────────────────────────────────
            // FIX: ExternalEvent.Create() MUST be called here, inside
            // IExternalCommand.Execute(), while Revit's API context is active.
            // Creating it later (e.g. lazily from a WPF button click) produces
            // a broken event whose handler Execute() never runs — causing the
            // window to stay hidden permanently after Hide() is called.
            // ─────────────────────────────────────────────────────────────────

            // Step 4a: Init the run event (idempotent)
            AutoSlopeEventManager.Init();

            // Step 4b: Create vertex selection handler + event RIGHT NOW
            // Pass them into the ViewModel so StartVertexSelection() just calls
            // Raise() on an already-valid event — no lazy creation needed.
            var vertexSelectionHandler = new VertexSelectionHandler(null); // ViewModel set below
            ExternalEvent vertexSelectionEvent = ExternalEvent.Create(vertexSelectionHandler);

            // Step 5: Build ViewModel, wiring the log to TaskDialog for errors
            // and passing the pre-created event so the ViewModel never creates
            // its own from the WPF thread.
            var viewModel = new AutoSlopeViewModel(
                uidoc,
                data.Application,
                roof.Id,
                drains,
                vertexSelectionHandler,
                vertexSelectionEvent);

            // Step 6: Open the window
            var window = new AutoSlopeWindow(viewModel);
            window.Show();

            return Result.Succeeded;
        }
    }

    public class RoofSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is RoofBase;
        public bool AllowReference(Reference reference, XYZ position) => true;
    }

    public class DrainPointSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is RoofBase;
        public bool AllowReference(Reference reference, XYZ position) => true;
    }
}