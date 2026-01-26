// =======================================================
// File: AutoSlopeCommand.cs
// Purpose: Entry point for AutoSlope command
// Revit: 2026
// =======================================================

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.AutoSlopeByPoint.Views;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByPoint.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class AutoSlopeCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData data,
            ref string msg,
            ElementSet elems)
        {
            UIDocument uidoc = data.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // ----------------------------------------------------
            // VIEW VALIDATION (PLAN VIEW REQUIRED)
            // ----------------------------------------------------
            View activeView = doc.ActiveView;

            if (activeView is not ViewPlan planView)
            {
                TaskDialog.Show(
                    "AutoSlope",
                    "AutoSlope must be run from a Plan View.");
                return Result.Cancelled;
            }

            // OPTIONAL: restrict to Floor Plans only
            if (planView.ViewType != ViewType.FloorPlan)
            {
                TaskDialog.Show(
                    "AutoSlope",
                    "AutoSlope must be run from a Floor Plan view.");
                return Result.Cancelled;
            }

            // ----------------------------------------------------
            // 1) SELECT ROOF (SAFE TO START SELECTION NOW)
            // ----------------------------------------------------
            Reference roofRef = uidoc.Selection.PickObject(
                ObjectType.Element,
                "Select a footprint roof");

            RoofBase roof = doc.GetElement(roofRef) as RoofBase;
            if (roof == null)
            {
                TaskDialog.Show(
                    "AutoSlope",
                    "Selected element is not a valid footprint roof.");
                return Result.Cancelled;
            }

            // ----------------------------------------------------
            // 2) ENABLE SHAPE EDITING
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
                    TaskDialog.Show(
                        "AutoSlope",
                        "Cannot enable shape editing:\n" + ex.Message);
                    return Result.Failed;
                }
                tx.Commit();
            }

            SlabShapeEditor editor = roof.GetSlabShapeEditor();
            if (!editor.IsEnabled)
            {
                TaskDialog.Show(
                    "AutoSlope",
                    "Shape editing could not be enabled. Roof type may not support shape editing.");
                return Result.Failed;
            }

            // ----------------------------------------------------
            // 3) RESET ALL VERTICES TO ZERO
            // ----------------------------------------------------
            using (Transaction tx = new Transaction(doc, "Reset Roof Vertices to Zero"))
            {
                tx.Start();

                foreach (SlabShapeVertex vertex in editor.SlabShapeVertices)
                {
                    // Correct overload for Revit 2026
                    editor.ModifySubElement(vertex, 0);
                }

                tx.Commit();
            }

            // ----------------------------------------------------
            // 4) PICK DRAIN POINTS
            // ----------------------------------------------------
            IList<Reference> picks = uidoc.Selection.PickObjects(
                ObjectType.PointOnElement,
                "Pick drain points and click Finish");

            var drains = new List<XYZ>();
            foreach (Reference r in picks)
                drains.Add(r.GlobalPoint);

            // ----------------------------------------------------
            // 5) SHOW AUTOSLOPE WINDOW
            // ----------------------------------------------------
            var win = new AutoSlopeWindow(
                uidoc,
                data.Application,
                roof.Id,
                drains);

            win.Show();

            return Result.Succeeded;
        }
    }
}
