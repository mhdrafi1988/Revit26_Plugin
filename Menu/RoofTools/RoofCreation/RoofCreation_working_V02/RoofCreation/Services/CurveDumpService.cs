// ==================================================
// File: CurveDumpService.cs
// ==================================================

using Autodesk.Revit.DB;
using Revit26_Plugin.RoofFromFloor.V02.Geometry;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.RoofFromFloor.V02.Services
{
    /// <summary>
    /// Debug-only service:
    /// - Removes duplicate / overlapping curves
    /// - Creates DetailCurves ONCE
    /// - Styles them (red, weight 8)
    /// - Groups all curves into a single group
    /// NO roof creation. NO duplication.
    /// </summary>
    public static class CurveDumpService
    {
        public static void DumpCleanGroupedCurves(
            Document doc,
            View view,
            IEnumerable<Curve> inputCurves,
            string groupName)
        {
            if (doc == null || view == null || inputCurves == null)
                return;

            // ==================================================
            // STEP 1 — REMOVE DUPLICATES / OVERLAPS (GEOMETRY)
            // ==================================================
            List<Curve> cleanedCurves = new();

            foreach (Curve curve in inputCurves)
            {
                bool overlaps =
                    cleanedCurves.Any(
                        existing => CurveUtils.AreCurvesOverlapping(existing, curve));

                if (!overlaps)
                {
                    cleanedCurves.Add(curve);
                }
            }

            if (cleanedCurves.Count == 0)
                return;

            List<ElementId> createdIds = new();

            // ==================================================
            // TX01 — CREATE CURVES (ONCE)
            // ==================================================
            using (Transaction tx1 = new Transaction(doc, "Create Clean Debug Curves"))
            {
                tx1.Start();

                foreach (Curve curve in cleanedCurves)
                {
                    DetailCurve dc = doc.Create.NewDetailCurve(view, curve);
                    createdIds.Add(dc.Id);
                }

                tx1.Commit();
            }

            if (createdIds.Count == 0)
                return;

            // ==================================================
            // TX02 — STYLE + GROUP
            // ==================================================
            using (Transaction tx2 = new Transaction(doc, "Style & Group Debug Curves"))
            {
                tx2.Start();

                // ---- Graphics Override ----
                OverrideGraphicSettings ogs = new OverrideGraphicSettings();
                ogs.SetProjectionLineColor(new Color(255, 0, 0)); // RED
                ogs.SetProjectionLineWeight(8);

                foreach (ElementId id in createdIds)
                {
                    view.SetElementOverrides(id, ogs);
                }

                // ---- Group ----
                Group group = doc.Create.NewGroup(createdIds);
                group.GroupType.Name = groupName;

                tx2.Commit();
            }
        }
    }
}
