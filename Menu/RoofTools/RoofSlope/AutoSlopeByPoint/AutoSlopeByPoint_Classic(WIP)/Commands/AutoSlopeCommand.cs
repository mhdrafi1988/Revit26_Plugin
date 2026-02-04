using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

using Revit26_Plugin.AutoSlopeByPoint_WIP2.Views;
//using Revit26_Plugin.AutoSlopeByPoint_WIP2.ViewModels;
using Revit26_Plugin.AutoSlopeByPoint_WIP2.ExternalEvents;

using System;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByPoint_WIP2.Commands
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
            UIApplication app = data.Application;
            Document doc = uidoc.Document;

            try
            {
                // =====================================================
                // 1) SELECT ROOF
                // =====================================================
                Reference roofRef = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    "Select a footprint roof");

                RoofBase roof = doc.GetElement(roofRef) as RoofBase;
                if (roof == null)
                {
                    TaskDialog.Show("AutoSlope", "Selected element is not a valid roof.");
                    return Result.Cancelled;
                }

                // =====================================================
                // 2) ENABLE SHAPE EDITING
                // =====================================================
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
                    TaskDialog.Show(
                        "AutoSlope",
                        "Shape editing could not be enabled. Roof type may not support shape editing.");
                    return Result.Failed;
                }

                // =====================================================
                // 3) RESET ALL VERTICES TO ZERO (Optional but recommended)
                // =====================================================
                using (Transaction tx = new Transaction(doc, "Reset Roof Vertices"))
                {
                    tx.Start();

                    foreach (SlabShapeVertex vertex in editor.SlabShapeVertices)
                    {
                        editor.ModifySubElement(vertex, 0);
                    }

                    tx.Commit();
                }

                // =====================================================
                // 4) PICK DRAIN POINTS
                // =====================================================
                IList<Reference> picks = uidoc.Selection.PickObjects(
                    ObjectType.PointOnElement,
                    "Pick drain points and click Finish");

                if (picks == null || picks.Count == 0)
                {
                    TaskDialog.Show("AutoSlope", "No drain points selected.");
                    return Result.Cancelled;
                }

                var drains = new List<XYZ>();
                foreach (var r in picks)
                    drains.Add(r.GlobalPoint);

                // =====================================================
                // 5) INIT EXTERNAL EVENT (CRITICAL STEP)
                // =====================================================
                AutoSlopeEventManager.Init(doc);

                // =====================================================
                // 6) CREATE WINDOW FIRST
                // =====================================================
                var win = new AutoSlopeWindow();

                // =====================================================
                // 7) CREATE VIEWMODEL (Pass window reference)
                // =====================================================
                var vm = new AutoSlopeViewModel(
                    uidoc,
                    app,
                    roof.Id,
                    drains,
                    win);

                win.DataContext = vm;
                win.Show();

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // User pressed ESC
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                TaskDialog.Show(
                    "AutoSlope – Fatal Error",
                    ex.ToString());

                return Result.Failed;
            }
        }
    }
}