using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using RoofTagV3.Services;
using RoofTagV3.Views;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RoofTagV3.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RoofTagCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                // Select roof
                RoofBase roof = SelectionService.SelectRoof(uiDoc);
                if (roof == null)
                {
                    TaskDialog.Show("Roof Tag V3", "No roof selected. Operation cancelled.");
                    return Result.Cancelled;
                }

                // Enable slab shape editor if needed
                SlabShapeEditor editor = roof.GetSlabShapeEditor();
                if (editor != null && !editor.IsEnabled)
                {
                    using (Transaction tx = new Transaction(doc, "Enable Slab Shape Editor"))
                    {
                        tx.Start();
                        editor.Enable();
                        tx.Commit();
                    }
                }

                // Show configuration window
                RoofTagWindow window = new RoofTagWindow(uiApp);
                if (window.ShowDialog() != true)
                    return Result.Cancelled;

                // Get points based on mode
                List<XYZ> points;
                var viewModel = window.ViewModel;

                if (viewModel.UseManualMode)
                {
                    IList<Reference> references = uiDoc.Selection.PickObjects(
                        ObjectType.PointOnElement,
                        "Select points on the roof surface");

                    points = references.Select(r => r.GlobalPoint).ToList();
                }
                else
                {
                    points = GeometryService.GetRoofVertices(roof);
                }

                if (points.Count == 0)
                {
                    TaskDialog.Show("Roof Tag V3", "No valid points found for tagging.");
                    return Result.Cancelled;
                }

                // Calculate centroid and boundary
                XYZ centroid = GeometryService.CalculateXYCentroid(points);
                List<XYZ> boundary = GeometryService.GetRoofBoundaryXY(roof);

                int successCount = 0;
                int failCount = 0;

                // Place tags
                using (Transaction tx = new Transaction(doc, "Place Roof Spot Elevations"))
                {
                    tx.Start();

                    foreach (XYZ point in points)
                    {
                        if (TaggingService.PlaceRoofSpotElevation(
                            doc,
                            roof,
                            point,
                            centroid,
                            boundary,
                            viewModel))
                        {
                            successCount++;
                        }
                        else
                        {
                            failCount++;
                        }
                    }

                    tx.Commit();
                }

                // Show results
                TaskDialog.Show("Roof Tag V3",
                    $"Tagging Complete:\n\n" +
                    $"Successfully placed: {successCount}\n" +
                    $"Failed: {failCount}\n\n" +
                    $"Total points processed: {points.Count}");

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Error", $"Failed to execute command:\n{ex.Message}");
                return Result.Failed;
            }
        }
    }
}