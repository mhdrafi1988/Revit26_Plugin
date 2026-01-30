using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByPoint
{
    [Transaction(TransactionMode.Manual)]
    public class AutoSlopeCommand_03 : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // ----------------------------------------------------
                // 1) USER SELECTS ROOF
                // ----------------------------------------------------
                Reference roofRef = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    "Select a FootPrint Roof");

                RoofBase roof = doc.GetElement(roofRef) as RoofBase;
                if (roof == null)
                {
                    TaskDialog.Show("AutoSlope", "Selected element is not a valid roof.");
                    return Result.Cancelled;
                }

                // ----------------------------------------------------
                // 2) ENABLE SHAPE EDITING IMMEDIATELY
                // ----------------------------------------------------
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

                // ----------------------------------------------------
                // 3) RESET ALL VERTICES TO ZERO (v10 WORKING METHOD)
                // ----------------------------------------------------
                using (Transaction tx = new Transaction(doc, "Reset Roof Vertices to Zero"))
                {
                    tx.Start();

                    int count = 0;
                    foreach (SlabShapeVertex vertex in editor.SlabShapeVertices)
                    {
                        editor.ModifySubElement(vertex, 0);   // <-- Correct overload
                        count++;
                    }

                    tx.Commit();
                }

                // ----------------------------------------------------
                // 4) USER PICKS DRAIN POINTS (WITH FINISH)
                // ----------------------------------------------------
                TaskDialog.Show("Drain Points",
                    "Select drain points on the roof.\nClick FINISH (green checkmark) when done.");

                IList<Reference> drainPicks = uidoc.Selection.PickObjects(
                    ObjectType.PointOnElement,
                    "Select drain points");

                List<XYZ> drainPoints = new List<XYZ>();
                foreach (Reference r in drainPicks)
                    drainPoints.Add(r.GlobalPoint);

                if (drainPoints.Count == 0)
                {
                    TaskDialog.Show("AutoSlope", "You must select at least one drain point.");
                    return Result.Cancelled;
                }

                // ----------------------------------------------------
                // 5) OPEN UI WINDOW (PASS ROOF + DRAINS)
                // ----------------------------------------------------
                var window = new AutoSlopeV3.Views.AutoSlopeWindow(
                    uidoc,
                    commandData.Application,
                    roof.Id,
                    drainPoints);

                window.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("AutoSlope Error", ex.Message);
                return Result.Failed;
            }
        }
    }
}
