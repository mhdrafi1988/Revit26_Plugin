using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.RoofFromFloor.V010.ViewModels;
using Revit26_Plugin.RoofFromFloor.V010.Services;
using Revit26_Plugin.Shared.Models;
using System.Collections.Generic;

namespace Revit26_Plugin.RoofFromFloor.V010.ExternalEvents
{
    public class RoofCreationHandler : IExternalEventHandler
    {
        private const string LineStyleName = "RoofFromFloor_Result";

        public RoofFromFloorViewModel ViewModel { get; set; }

        public void Execute(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            Document   doc   = uidoc.Document;
            View       view  = uidoc.ActiveView;

            // ── Validation ──────────────────────────────────────────────
            if (ViewModel?.RoofContext == null)
            {
                ViewModel?.LogFromExternal("ABORT: RoofContext is null.", LogLevel.Error);
                ViewModel?.ShowWindow();
                return;
            }

            var roofCurves = ViewModel.RoofContext.RoofFootprintCurves;
            var floorLoops = ViewModel.CleanLoops;

            bool hasRoof  = roofCurves  != null && roofCurves.Count  > 0;
            bool hasFloor = floorLoops  != null && floorLoops.Count  > 0;

            if (!hasRoof && !hasFloor)
            {
                ViewModel.LogFromExternal("ABORT: No roof or floor curves available.", LogLevel.Error);
                ViewModel.ShowWindow();
                return;
            }

            // ── Collect all curves ───────────────────────────────────────
            var allCurves = new List<Curve>();

            if (hasRoof)  allCurves.AddRange(roofCurves);
            if (hasFloor) foreach (var loop in floorLoops) allCurves.AddRange(loop);

            // ── Remove overlaps ──────────────────────────────────────────
            var uniqueCurves = OverlapRemovalService.RemoveOverlapsKeepLongest(allCurves);
            ViewModel.LogFromExternal($"Overlap removal: {allCurves.Count} → {uniqueCurves.Count} unique curves.");

            // ── Create detail lines ──────────────────────────────────────
            using (var tx = new Transaction(doc, "Create Detail Curves — RoofFromFloor"))
            {
                tx.Start();

                GraphicsStyle gs = GetOrCreateLineStyle(doc);
                var createdIds   = new List<ElementId>(uniqueCurves.Count);

                foreach (Curve c in uniqueCurves)
                {
                    DetailCurve dc = doc.Create.NewDetailCurve(view, c);
                    dc.LineStyle   = gs;
                    createdIds.Add(dc.Id);
                }

                // Group all created lines for easy selection / deletion
                if (createdIds.Count > 0)
                {
                    Group group           = doc.Create.NewGroup(createdIds);
                    group.GroupType.Name  = LineStyleName;
                }

                tx.Commit();
            }

            ViewModel.LogFromExternal($"Done — {uniqueCurves.Count} detail curves placed.", LogLevel.Success);
            ViewModel.SetSummary(uniqueCurves.Count);
            ViewModel.ShowWindow();
        }

        public string GetName() => "RoofFromFloor · Detail Curve Creation";

        // ── Helpers ──────────────────────────────────────────────────────
        private static GraphicsStyle GetOrCreateLineStyle(Document doc)
        {
            Category linesCat = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);

            foreach (Category sub in linesCat.SubCategories)
            {
                if (sub.Name == LineStyleName)
                    return sub.GetGraphicsStyle(GraphicsStyleType.Projection);
            }

            // First run: create the subcategory
            Category created = doc.Settings.Categories.NewSubcategory(linesCat, LineStyleName);
            created.LineColor = new Color(255, 0, 0); // Red
            created.SetLineWeight(4, GraphicsStyleType.Projection);
            return created.GetGraphicsStyle(GraphicsStyleType.Projection);
        }
    }
}
