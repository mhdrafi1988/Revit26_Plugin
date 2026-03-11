// ==================================================
// File: RoofCreationHandler.cs
// ==================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.RoofFromFloor.Services;
using Revit26_Plugin.RoofFromFloor.ViewModels;
using System.Collections.Generic;
using System.Linq;

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

            // ---------- VALIDATION ----------
            if (ViewModel?.FloorProfiles == null || ViewModel.FloorProfiles.Count == 0)
            {
                ViewModel?.LogFromExternal("ABORT: No floor profiles available.");
                ViewModel?.ShowWindow();
                return;
            }

            // ---------- COLLECT ONLY FLOOR CURVES ----------
            var floorCurves = new List<Curve>();

            foreach (var profile in ViewModel.FloorProfiles)
            {
                floorCurves.AddRange(profile.Curves);
            }

            ViewModel.LogFromExternal($"Total floor curves before cleanup: {floorCurves.Count}");

            // ---------- REMOVE OVERLAPS (KEEP LONGEST) ----------
            var uniqueCurves = OverlapRemovalService.AggressiveOverlapRemoval(floorCurves);

            ViewModel.LogFromExternal($"Total floor curves after cleanup: {uniqueCurves.Count}");

            // ---------- CREATE DETAIL LINES ----------
            using (Transaction tx = new Transaction(doc, "Create Floor Detail Curves"))
            {
                tx.Start();

                // Create or get line style
                Category linesCat = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
                Category lineStyle = null;

                foreach (Category sub in linesCat.SubCategories)
                {
                    if (sub.Name == "FloorFromLink_Result")
                    {
                        lineStyle = sub;
                        break;
                    }
                }

                if (lineStyle == null)
                {
                    lineStyle = doc.Settings.Categories.NewSubcategory(linesCat, "FloorFromLink_Result");
                    lineStyle.LineColor = new Color(0, 255, 0); // Green for floors
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
                    group.GroupType.Name = "FloorFromLink_Result";
                }

                tx.Commit();
            }

            ViewModel.LogFromExternal($"Created {uniqueCurves.Count} floor detail curves");
            ViewModel.ShowWindow();
        }

        public string GetName() => "Floor Detail Curve Creation Handler";
    }
}