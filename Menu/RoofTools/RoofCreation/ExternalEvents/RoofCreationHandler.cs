// ==================================================
// File: RoofCreationHandler.cs
// ==================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.RoofFromFloor.ViewModels;
using System.Collections.Generic;

namespace Revit26_Plugin.RoofFromFloor.ExternalEvents
{
    public class RoofCreationHandler : IExternalEventHandler
    {
        public RoofFromFloorViewModel ViewModel { get; set; }

        public void Execute(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc.Document;
            View view = uidoc.ActiveView;

            // ---------- HARD VALIDATION ----------
            if (ViewModel?.RoofContext == null)
            {
                ViewModel?.LogFromExternal("ABORT: RoofContext is null.");
                ViewModel?.ShowWindow();
                return;
            }

            var roofCurves = ViewModel.RoofContext.RoofFootprintCurves;
            var floorLoops = ViewModel.CleanLoops;

            if ((roofCurves == null || roofCurves.Count == 0) &&
                (floorLoops == null || floorLoops.Count == 0))
            {
                ViewModel.LogFromExternal("ABORT: No roof or floor loops available.");
                ViewModel.ShowWindow();
                return;
            }

            // ---------- T1 TRANSACTION ----------
            using (Transaction tx = new Transaction(doc, "T1_Draw_Roof_And_Floor_Loops"))
            {
                tx.Start();

                // 1. Resolve or create LineStyle (Red, Weight 8)
                Category linesCat = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
                Category redStyle = null;

                foreach (Category sub in linesCat.SubCategories)
                {
                    if (sub.Name == "T1_Red_8")
                    {
                        redStyle = sub;
                        break;
                    }
                }

                if (redStyle == null)
                {
                    redStyle = doc.Settings.Categories.NewSubcategory(linesCat, "T1_Red_8");
                    redStyle.LineColor = new Color(255, 0, 0);
                    redStyle.SetLineWeight(8, GraphicsStyleType.Projection);
                }

                GraphicsStyle gs = redStyle.GetGraphicsStyle(GraphicsStyleType.Projection);

                // 2. Create detail lines from BOTH sources
                List<ElementId> createdIds = new();

                // Roof curves
                if (roofCurves != null)
                {
                    foreach (Curve c in roofCurves)
                    {
                        DetailCurve dc = doc.Create.NewDetailCurve(view, c);
                        dc.LineStyle = gs;
                        createdIds.Add(dc.Id);
                    }
                }

                // Floor (linked) loops
                if (floorLoops != null)
                {
                    foreach (CurveLoop loop in floorLoops)
                    {
                        foreach (Curve c in loop)
                        {
                            DetailCurve dc = doc.Create.NewDetailCurve(view, c);
                            dc.LineStyle = gs;
                            createdIds.Add(dc.Id);
                        }
                    }
                }

                // 3. Group (unique name = 1)
                if (createdIds.Count > 0)
                {
                    Group g = doc.Create.NewGroup(createdIds);
                    g.GroupType.Name = "1";
                }

                tx.Commit();
            }

            ViewModel.LogFromExternal("T1 roof + floor loops drawn and grouped.");
            ViewModel.ShowWindow();
        }

        public string GetName() => "T1 Roof & Floor Detail Line Handler";
    }
}
