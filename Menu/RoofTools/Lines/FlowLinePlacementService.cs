using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoLiner_V01.Services
{
    public class FlowLinePlacementService
    {
        /// <summary>
        /// Places line-based Detail Item family instances along a path.
        /// </summary>
        public int PlaceFlowLines(
            Document doc,
            View view,
            FamilySymbol lineSymbol,
            List<XYZ> path,
            double minSegmentLengthMm)
        {
            // ================= BASIC GUARDS =================
            if (doc == null || view == null || lineSymbol == null)
                return 0;

            if (path == null || path.Count < 2)
                return 0;

            // Detail items only work in plan or drafting views
            if (!(view is ViewPlan) && view.ViewType != ViewType.DraftingView)
                return 0;

            // Must be Detail Components
            if ((BuiltInCategory)lineSymbol.Family.FamilyCategory.Id.Value
                != BuiltInCategory.OST_DetailComponents)
                return 0;

            // ================= UNITS =================
            double minLenFt = minSegmentLengthMm / 304.8;
            int created = 0;

            // ================= TRANSACTION =================
            using (Transaction t = new Transaction(doc, "AutoLiner – Place Flow Lines"))
            {
                t.Start();

                // Activate symbol properly
                if (!lineSymbol.IsActive)
                {
                    lineSymbol.Activate();
                    doc.Regenerate();
                }

                double viewZ = GetViewElevation(view);

                for (int i = 0; i < path.Count - 1; i++)
                {
                    // 🔥 PROJECT TO VIEW PLANE (CRITICAL)
                    XYZ p1 = new XYZ(path[i].X, path[i].Y, viewZ);
                    XYZ p2 = new XYZ(path[i + 1].X, path[i + 1].Y, viewZ);

                    if (p1.DistanceTo(p2) < minLenFt)
                        continue;

                    try
                    {
                        Line line = Line.CreateBound(p1, p2);

                        doc.Create.NewFamilyInstance(
                            line,
                            lineSymbol,
                            view);

                        created++;
                    }
                    catch
                    {
                        // swallow and continue
                    }
                }

                t.Commit();
            }

            return created;
        }

        // ================= HELPERS =================
        private double GetViewElevation(View view)
        {
            if (view is ViewPlan vp && vp.GenLevel != null)
                return vp.GenLevel.Elevation;

            return 0.0;
        }
    }
}
