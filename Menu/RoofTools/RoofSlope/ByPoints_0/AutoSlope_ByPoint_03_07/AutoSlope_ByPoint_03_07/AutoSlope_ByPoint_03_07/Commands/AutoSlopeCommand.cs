using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
//using Revit22_Plugin.AutoSlopeV3.Views;
using Revit26_Plugin.AutoSlopeByPoint_30_07.Views;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByPoint_30_07.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class AutoSlopeCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string msg, ElementSet elems)
        {
            UIDocument uidoc = data.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            Reference roofRef = uidoc.Selection.PickObject(
                ObjectType.Element, "Select a footprint roof");

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

            IList<Reference> picks = uidoc.Selection.PickObjects(
                ObjectType.PointOnElement, "Pick drain points and click Finish");

            var drains = new List<XYZ>();
            foreach (var r in picks) drains.Add(r.GlobalPoint);

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
