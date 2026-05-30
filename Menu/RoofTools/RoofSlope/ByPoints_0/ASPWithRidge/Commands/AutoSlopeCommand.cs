// =======================================================
// File: AutoSlopeCommand.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.WithRidge
// Notes:
//   AutoSlopeEventManager.Init() is NOT called here.
//   Call it once from IExternalApplication.OnStartup.
//   ESC during drain selection rolls back Enable() via
//   TransactionGroup. Each picked point is validated to
//   belong to the selected roof before proceeding.
// =======================================================

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.AutoSlopeByPoint.WithRidge.UI.Views;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByPoint.WithRidge.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class AutoSlopeCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string msg, ElementSet elems)
        {
            UIDocument uidoc = data.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // ── 1. Pick the roof ─────────────────────────────────────────────
            Reference roofRef;
            try
            {
                roofRef = uidoc.Selection.PickObject(
                    ObjectType.Element, "Select a footprint roof");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            RoofBase roof = doc.GetElement(roofRef) as RoofBase;
            if (roof == null)
            {
                TaskDialog.Show("AutoSlope", "Selected element is not a valid roof.");
                return Result.Cancelled;
            }

            // ── 2 + 3. Enable shape editing AND pick drains inside one group ──
            IList<Reference> picks = null;

            using (TransactionGroup tg = new TransactionGroup(doc, "AutoSlope Setup"))
            {
                tg.Start();

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
                        tg.RollBack();
                        TaskDialog.Show("AutoSlope",
                            "Cannot enable shape editing:\n" + ex.Message);
                        return Result.Failed;
                    }
                    tx.Commit();
                }

                var editor = roof.GetSlabShapeEditor();
                if (!editor.IsEnabled)
                {
                    tg.RollBack();
                    TaskDialog.Show("AutoSlope",
                        "Shape editing could not be enabled. " +
                        "Roof type may not support shape editing.");
                    return Result.Failed;
                }

                try
                {
                    picks = uidoc.Selection.PickObjects(
                        ObjectType.PointOnElement,
                        "Pick drain points on the roof, then click Finish");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    tg.RollBack();
                    return Result.Cancelled;
                }

                var drains = new List<XYZ>();
                foreach (var r in picks)
                {
                    if (r.ElementId != roof.Id)
                    {
                        tg.RollBack();
                        TaskDialog.Show("AutoSlope",
                            "One or more drain points were not picked on the selected roof.\n" +
                            "Please re-run the command and click only on the roof surface.");
                        return Result.Cancelled;
                    }
                    drains.Add(r.GlobalPoint);
                }

                if (drains.Count == 0)
                {
                    tg.RollBack();
                    TaskDialog.Show("AutoSlope", "No drain points were selected.");
                    return Result.Cancelled;
                }

                tg.Assimilate();

                // ── 4. Open the AutoSlope window ─────────────────────────────
                var win = new AutoSlopeWindow(uidoc, data.Application, roof.Id, drains);
                win.Show();
            }

            return Result.Succeeded;
        }
    }
}
