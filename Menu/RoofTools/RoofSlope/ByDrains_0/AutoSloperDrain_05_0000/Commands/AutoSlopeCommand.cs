using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.AutoSlope.V5_00.UI.Views;
using Revit26_Plugin.AutoSlope.V5_00.Infrastructure.ExternalEvents;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlope.V5_00.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class AutoSlopeCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                UIDocument uidoc = uiApp.ActiveUIDocument;
                Document doc = uidoc.Document;

                // Select roof
                Reference roofRef = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    new RoofSelectionFilter(),
                    "Select a roof for auto-slope application");

                RoofBase roof = doc.GetElement(roofRef) as RoofBase;
                if (roof == null)
                {
                    TaskDialog.Show("Auto Slope V5", "Selected element is not a valid roof.");
                    return Result.Cancelled;
                }

                // Check if roof can be edited (works with Worksharing)
                if (doc.IsWorkshared)
                {
                    WorksharingUtils.CheckoutElements(doc, new List<ElementId> { roof.Id });
                }

                // Initialize event manager FIRST
                AutoSlopeEventManager.Init();

                // Initialize roof geometry with proper transaction
                bool initialized = InitializeRoofGeometry(roof, doc);
                if (!initialized)
                {
                    TaskDialog.Show("Auto Slope V5",
                        "Failed to initialize roof geometry.\n\n" +
                        "This may happen because:\n" +
                        "• The roof doesn't support shape editing\n" +
                        "• The roof is a complex type (like an extrusion roof)\n" +
                        "• The roof is based on a footprint with no editable vertices\n\n" +
                        "Try using a different roof type or create a basic roof with shape editing enabled first.");
                    return Result.Failed;
                }

                // Show main window
                var window = new MainWindow(uidoc, uiApp, roof.Id);
                window.ShowDialog();

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                TaskDialog.Show("Auto Slope V5", "Operation cancelled by user.");
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Auto Slope V5", $"Error: {ex.Message}\n\nStack Trace: {ex.StackTrace}");
                return Result.Failed;
            }
        }

        private bool InitializeRoofGeometry(RoofBase roof, Document doc)
        {
            try
            {
                // Step 1: Get the Slab Shape Editor
                var editor = roof.GetSlabShapeEditor();
                if (editor == null)
                {
                    return false;
                }

                // Step 2: Enable shape editing if not already enabled
                if (!editor.IsEnabled)
                {
                    using (Transaction tx = new Transaction(doc, "Enable Shape Editing"))
                    {
                        tx.Start();
                        try
                        {
                            editor.Enable();
                            tx.Commit();
                        }
                        catch (Exception ex)
                        {
                            tx.RollBack();
                            System.Diagnostics.Debug.WriteLine($"Failed to enable shape editing: {ex.Message}");
                            return false;
                        }
                    }
                }

                // Step 3: Wait a moment and refresh to ensure vertices are available
                System.Threading.Thread.Sleep(100);
                doc.Regenerate();

                // Step 4: Get vertices
                var vertices = editor.SlabShapeVertices?.Cast<SlabShapeVertex>().ToList() ?? new List<SlabShapeVertex>();

                // If still no vertices, we can't proceed
                if (vertices.Count == 0)
                {
                    // Instead of trying to draw points (which doesn't exist), we'll just return false
                    // The roof doesn't have editable vertices
                    return false;
                }

                // Step 5: Reset vertices to zero in a separate transaction
                if (vertices.Count > 0)
                {
                    using (Transaction tx = new Transaction(doc, "Reset Roof Vertices"))
                    {
                        // Set failure handling
                        var failureOptions = tx.GetFailureHandlingOptions();
                        failureOptions.SetFailuresPreprocessor(new SlopeWarningSwallower());
                        tx.SetFailureHandlingOptions(failureOptions);

                        tx.Start();
                        try
                        {
                            // Reset each vertex to zero elevation
                            foreach (var vertex in vertices)
                            {
                                if (vertex != null)
                                {
                                    editor.ModifySubElement(vertex, 0.0);
                                }
                            }

                            tx.Commit();
                        }
                        catch (Exception ex)
                        {
                            tx.RollBack();
                            System.Diagnostics.Debug.WriteLine($"Failed to reset vertices: {ex.Message}");
                            return false;
                        }
                    }
                }
                else
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in InitializeRoofGeometry: {ex.Message}");
                return false;
            }
        }
    }

    // Warning swallower for expected slope warnings
    public class SlopeWarningSwallower : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            var failList = failuresAccessor.GetFailureMessages();

            foreach (var fail in failList)
            {
                try
                {
                    // Get failure information
                    var id = fail.GetFailureDefinitionId();
                    string description = fail.GetDescriptionText();
                    FailureSeverity severity = fail.GetSeverity();

                    // Check for common slope-related warnings by description
                    bool isSlopeWarning = !string.IsNullOrEmpty(description) && (
                        (description.IndexOf("slope", StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (description.IndexOf("drain", StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (description.IndexOf("elevation", StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (description.IndexOf("Slope Arrow", StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (description.IndexOf("shape edit", StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (description.IndexOf("vertex", StringComparison.OrdinalIgnoreCase) >= 0));

                    // Also check by known failure IDs
                    bool isKnownWarning = id != null && (
                        id.Guid.ToString().IndexOf("slope", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        id.Guid.ToString().IndexOf("drain", StringComparison.OrdinalIgnoreCase) >= 0);

                    if (isSlopeWarning || isKnownWarning || severity == FailureSeverity.Warning)
                    {
                        failuresAccessor.DeleteWarning(fail);
                    }
                }
                catch
                {
                    // Ignore if can't delete
                }
            }

            return FailureProcessingResult.Continue;
        }
    }

    public class RoofSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is RoofBase;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}