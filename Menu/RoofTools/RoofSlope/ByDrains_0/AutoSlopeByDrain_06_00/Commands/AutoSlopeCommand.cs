using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.AutoSlopeByDrain_06_00.UI.Views;
using Revit26_Plugin.AutoSlopeByDrain_06_00.Infrastructure.Services;
using Revit26_Plugin.AutoSlopeByDrain_06_00.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByDrain_06_00.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class AutoSlopeCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string msg, ElementSet elems)
        {
            UIDocument uidoc = data.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // Step 1: Select roof
                Reference roofRef = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    new RoofSelectionFilter(),
                    "Select a roof with drain openings");

                RoofBase roof = doc.GetElement(roofRef) as RoofBase;
                if (roof == null)
                {
                    TaskDialog.Show("AutoSlope", "Selected element is not a valid roof.");
                    return Result.Cancelled;
                }

                // Step 2: Enable shape editing and reset vertices
                using (Transaction tx = new Transaction(doc, "Enable Shape Editing"))
                {
                    tx.Start();
                    try
                    {
                        roof.GetSlabShapeEditor().Enable();
                    }
                    catch (Exception ex)
                    {
                        tx.RollBack();
                        TaskDialog.Show("AutoSlope", "Cannot enable shape editing:\n" + ex.Message);
                        return Result.Failed;
                    }
                    tx.Commit();
                }

                var editor = roof.GetSlabShapeEditor();
                if (!editor.IsEnabled)
                {
                    TaskDialog.Show("AutoSlope",
                        "Shape editing could not be enabled. Roof type may not support shape editing.");
                    return Result.Failed;
                }

                // Step 3: Reset vertices to zero
                using (Transaction tx = new Transaction(doc, "Reset Roof Vertices to Zero"))
                {
                    tx.Start();
                    foreach (SlabShapeVertex vertex in editor.SlabShapeVertices)
                        editor.ModifySubElement(vertex, 0);
                    tx.Commit();
                }

                // Step 4: Auto-detect drains
                TaskDialog.Show("AutoSlope", "Detecting drain openings in roof...");

                Face topFace = GetTopFace(roof);
                if (topFace == null)
                {
                    TaskDialog.Show("AutoSlope", "Could not identify top face of roof.");
                    return Result.Failed;
                }

                var drainService = new DrainDetectionService();
                List<DrainItem> detectedDrains = drainService.DetectDrainsFromRoof(roof, topFace);

                if (detectedDrains.Count == 0)
                {
                    var result = TaskDialog.Show("AutoSlope",
                        "No drain openings detected in the roof.\n\nDo you want to manually pick drain points?",
                        TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                        TaskDialogResult.No);

                    if (result == TaskDialogResult.Yes)
                    {
                        // Fallback to manual picking
                        IList<Reference> picks = uidoc.Selection.PickObjects(
                            ObjectType.PointOnElement,
                            "Pick drain points and click Finish");

                        var manualDrains = new List<XYZ>();
                        foreach (var r in picks)
                            manualDrains.Add(r.GlobalPoint);

                        // Convert manual points to DrainItems
                        detectedDrains = manualDrains.Select((p, i) =>
                            new DrainItem(p, 100, 100, $"Manual {i + 1}")).ToList();
                    }
                    else
                    {
                        return Result.Cancelled;
                    }
                }

                // Step 5: Show UI with detected drains
                var win = new AutoSlopeWindow(uidoc, data.Application, roof.Id, detectedDrains);
                win.ShowDialog();

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                TaskDialog.Show("AutoSlope", "Operation cancelled by user.");
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("AutoSlope Error", $"Unexpected error:\n{ex.Message}");
                return Result.Failed;
            }
        }

        private Face GetTopFace(RoofBase roof)
        {
            Options opt = new Options
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = false
            };

            GeometryElement geom = roof.get_Geometry(opt);
            if (geom == null) return null;

            Face topFace = null;
            double maxZ = double.MinValue;

            foreach (GeometryObject obj in geom)
            {
                if (obj is not Solid solid || solid.Faces.Size == 0)
                    continue;

                foreach (Face face in solid.Faces)
                {
                    BoundingBoxUV bb = face.GetBoundingBox();
                    if (bb == null) continue;

                    UV mid = new UV(
                        (bb.Min.U + bb.Max.U) * 0.5,
                        (bb.Min.V + bb.Max.V) * 0.5);

                    XYZ p = face.Evaluate(mid);
                    if (p == null) continue;

                    if (p.Z > maxZ)
                    {
                        maxZ = p.Z;
                        topFace = face;
                    }
                }
            }
            return topFace;
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