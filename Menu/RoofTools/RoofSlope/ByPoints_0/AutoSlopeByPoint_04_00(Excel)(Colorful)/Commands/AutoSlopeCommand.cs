// =======================================================
// File: AutoSlopeCommand.cs
// Location: Commands/
// Change vs previous:
//   REMOVED  "Reset Roof Vertices to Zero" transaction block
//            (redundant — AutoSlopeEngine already resets all
//             vertices at the start of Execute, inside the
//             correct Revit External Event context)
//   KEPT     Enable Shape Editing transaction (required before
//             the window opens so the editor is ready)
//   KEPT     PickObjects for drain point selection
// =======================================================

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.AutoSlopeByPoint_04.UI.Views;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByPoint_04.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class AutoSlopeCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string msg, ElementSet elems)
        {
            UIDocument uidoc = data.Application.ActiveUIDocument;
            Document   doc   = uidoc.Document;

            // ── 1. Pick the roof ─────────────────────────────────────────────
            Reference roofRef = uidoc.Selection.PickObject(
                ObjectType.Element, "Select a footprint roof");

            RoofBase roof = doc.GetElement(roofRef) as RoofBase;
            if (roof == null)
            {
                TaskDialog.Show("AutoSlope", "Selected element is not a valid roof.");
                return Result.Cancelled;
            }

            // ── 2. Enable shape editing ───────────────────────────────────────
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
                    TaskDialog.Show("AutoSlope",
                        "Cannot enable shape editing:\n" + ex.Message);
                    return Result.Failed;
                }
                tx.Commit();
            }

            var editor = roof.GetSlabShapeEditor();
            if (!editor.IsEnabled)
            {
                TaskDialog.Show("AutoSlope",
                    "Shape editing could not be enabled. " +
                    "Roof type may not support shape editing.");
                return Result.Failed;
            }

            // NOTE: Vertex reset is NOT done here.
            // AutoSlopeEngine.Execute resets all vertices to 0 at the start
            // of its own transaction, inside the Revit External Event where
            // it is safe to do so. A second reset here was redundant.

            // ── 3. Pick drain points ──────────────────────────────────────────
            IList<Reference> picks = uidoc.Selection.PickObjects(
                ObjectType.PointOnElement, "Pick drain points and click Finish");

            var drains = new List<XYZ>();
            foreach (var r in picks)
                drains.Add(r.GlobalPoint);

            // ── 4. Open the AutoSlope window ──────────────────────────────────
            var win = new AutoSlopeWindow(uidoc, data.Application, roof.Id, drains);
            win.Show();

            return Result.Succeeded;
        }
    }
}
