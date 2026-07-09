// File: AutoSlopeDrainCommand.cs
// Location: Commands/
// Changes vs CSV version:
//   CHANGED  mainWindow.ShowDialog() -> window.Show() (modeless), matching
//            AutoSlopeByPoint. The "Apply Slope" action now runs through
//            AutoSlopeDrainEventManager / AutoSlopeDrainHandler instead of
//            a direct transaction on the UI thread.
//   KEPT     Roof pick -> enable shape editing -> reset vertices to zero ->
//            initial drain detection all still happen here, synchronously,
//            BEFORE the window is shown (same as CSV version) — this is
//            safe because the window has not opened yet.

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.AutoSlopeByDrain.V003.Core.Models;
using Revit26_Plugin.AutoSlopeByDrain.V003.Core.Services;
using Revit26_Plugin.AutoSlopeByDrain.V003.UI.ViewModels;
using Revit26_Plugin.AutoSlopeByDrain.V003.UI.Views;

namespace Revit26_Plugin.AutoSlopeByDrain.V003.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class AutoSlopeByDrain : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                UIDocument uidoc = uiApp.ActiveUIDocument;
                Document doc = uidoc.Document;

                Reference roofRef;
                try
                {
                    roofRef = uidoc.Selection.PickObject(
                        ObjectType.Element,
                        new RoofFilter(),
                        "Select a roof");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return Result.Cancelled;
                }

                var roof = doc.GetElement(roofRef) as RoofBase;
                if (roof == null)
                {
                    TaskDialog.Show("AutoSlope By Drain", "Selected element is not a valid roof.");
                    return Result.Cancelled;
                }

                var roofData = new RoofData { Roof = roof };

                InitializeRoofGeometry(roof, doc);
                AnalyzeRoofGeometry(roofData);

                var detectionService = new DrainDetectionService();
                var detectedDrains = detectionService.DetectDrainsFromRoof(roof, roofData.TopFace, roofData.Vertices);
                roofData.DetectedDrains = detectedDrains;

                var viewModel = new AutoSlopeDrainViewModel(uidoc, uiApp, roofData);
                var window = new AutoSlopeDrainWindow(viewModel);
                window.Show(); // modeless — matches AutoSlopeByPoint

                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                message = $"Failed to initialize plugin: {ex.Message}";
                return Result.Failed;
            }
        }

        private void InitializeRoofGeometry(RoofBase roof, Document doc)
        {
            using (Transaction tx = new Transaction(doc, "AutoSlope By Drain - Initialize Geometry"))
            {
                tx.Start();

                var slabShapeEditor = roof.GetSlabShapeEditor();
                if (!slabShapeEditor.IsEnabled)
                {
                    slabShapeEditor.Enable();
                }

                foreach (SlabShapeVertex vertex in slabShapeEditor.SlabShapeVertices)
                {
                    slabShapeEditor.ModifySubElement(vertex, 0.0);
                }

                tx.Commit();
            }
        }

        private void AnalyzeRoofGeometry(RoofData roofData)
        {
            var roof = roofData.Roof;

            roofData.TopFace = GetTopFace(roof);
            if (roofData.TopFace == null)
                throw new System.Exception("Could not find top face of the roof.");

            roofData.Vertices.Clear();
            var slabShapeEditor = roof.GetSlabShapeEditor();
            foreach (SlabShapeVertex vertex in slabShapeEditor.SlabShapeVertices)
            {
                roofData.Vertices.Add(vertex);
            }
        }

        private Face GetTopFace(RoofBase roof)
        {
            GeometryElement geomElem = roof.get_Geometry(new Options());
            Face topFace = null;
            double maxZ = double.MinValue;

            foreach (GeometryObject geomObj in geomElem)
            {
                if (geomObj is Solid solid)
                {
                    foreach (Face face in solid.Faces)
                    {
                        if (face == null) continue;
                        BoundingBoxUV bb = face.GetBoundingBox();
                        if (bb == null) continue;

                        UV midpointUV = new UV((bb.Min.U + bb.Max.U) / 2, (bb.Min.V + bb.Max.V) / 2);
                        XYZ midpoint = face.Evaluate(midpointUV);

                        if (midpoint != null && midpoint.Z > maxZ)
                        {
                            maxZ = midpoint.Z;
                            topFace = face;
                        }
                    }
                }
            }
            return topFace;
        }
    }

    public class RoofFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is RoofBase;
        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
