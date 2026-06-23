// ==================================================
// File: RoofCreationHandler.cs
// ==================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.RoofFromFloor.V008.V007.ViewModels;
using Revit26_Plugin.RoofFromFloor.V008.V007.Services;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.RoofFromFloor.V008.V007.ExternalEvents
{
    public class RoofCreationHandler : IExternalEventHandler
    {
        public RoofFromFloorViewModel ViewModel { get; set; }

        public void Execute(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc.Document;
            View view = uidoc.ActiveView;

            // ---------- VALIDATION ----------
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

            // ---------- COLLECT ALL CURVES ----------
            var allCurves = new List<Curve>();

            // Add roof curves
            if (roofCurves != null)
                allCurves.AddRange(roofCurves);

            // Add floor curves
            if (floorLoops != null)
            {
                foreach (var loop in floorLoops)
                {
                    allCurves.AddRange(loop);
                }
            }

            // ---------- REMOVE OVERLAPS (KEEP LONGEST) ----------
            var uniqueCurves = OverlapRemovalService.RemoveOverlapsKeepLongest(allCurves);

            // ---------- CREATE DETAIL LINES ----------
            using (Transaction tx = new Transaction(doc, "Create Detail Curves"))
            {
                tx.Start();

                // Create or get line style
                Category linesCat = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
                Category lineStyle = null;

                foreach (Category sub in linesCat.SubCategories)
                {
                    if (sub.Name == "RoofFromFloor_Result")
                    {
                        lineStyle = sub;
                        break;
                    }
                }

                if (lineStyle == null)
                {
                    lineStyle = doc.Settings.Categories.NewSubcategory(linesCat, "RoofFromFloor_Result");
                    lineStyle.LineColor = new Color(255, 0, 0); // Red
                    lineStyle.SetLineWeight(4, GraphicsStyleType.Projection);
                }

                GraphicsStyle gs = lineStyle.GetGraphicsStyle(GraphicsStyleType.Projection);

                // Create detail lines
                List<ElementId> createdIds = new();
                foreach (Curve c in uniqueCurves)
                {
                    DetailCurve dc = doc.Create.NewDetailCurve(view, c);
                    dc.LineStyle = gs;
                    createdIds.Add(dc.Id);
                }

                // Group results
                if (createdIds.Count > 0)
                {
                    Group group = doc.Create.NewGroup(createdIds);
                    group.GroupType.Name = "RoofFromFloor_Result";
                }

                tx.Commit();
            }

            ViewModel.LogFromExternal($"Created {uniqueCurves.Count} detail curves (overlaps removed, longest kept)");
            ViewModel.ShowWindow();
        }

        public string GetName() => "Detail Curve Creation Handler";
    }
}