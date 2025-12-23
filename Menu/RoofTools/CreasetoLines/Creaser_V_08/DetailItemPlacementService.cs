using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.Creaser_V08.Commands.Services
{
    /// <summary>
    /// Places either:
    /// 1) Line-based Detail Item family instances (projected to view plane), or
    /// 2) Detail Lines (fallback).
    ///
    /// This is the ONLY service allowed to call doc.Create.
    /// </summary>
    public class DetailItemPlacementService
    {
        public int Place(
            Document doc,
            View view,
            IList<Line> lines,
            FamilySymbol detailItemSymbol = null)
        {
            if (doc == null || view == null || lines == null || lines.Count == 0)
                return 0;

            // Detail items / curves not allowed here
            if (view is View3D || view is ViewSheet)
                return 0;

            int created = 0;

            using (Transaction t = new Transaction(doc, "Place Creaser Output"))
            {
                t.Start();

                // ------------------------------------------------------------
                // Ensure view has a SketchPlane
                // ------------------------------------------------------------
                Plane plane;
                if (view.SketchPlane == null)
                {
                    plane = Plane.CreateByNormalAndOrigin(
                        view.ViewDirection,
                        view.Origin);

                    view.SketchPlane = SketchPlane.Create(doc, plane);
                }
                else
                {
                    plane = view.SketchPlane.GetPlane();
                }

                // ============================================================
                // CASE 1: LINE-BASED DETAIL ITEM FAMILY
                // ============================================================
                if (detailItemSymbol != null)
                {
                    if (!detailItemSymbol.IsActive)
                        detailItemSymbol.Activate();

                    foreach (Line line in lines)
                    {
                        Line projected = ProjectLineToPlane(line, plane);
                        if (projected == null)
                            continue;

                        try
                        {
                            doc.Create.NewFamilyInstance(
                                projected,
                                detailItemSymbol,
                                view);

                            created++;
                        }
                        catch
                        {
                            // Not valid for this family or view
                            continue;
                        }
                    }

                    t.Commit();
                    return created;
                }

                // ============================================================
                // CASE 2: DETAIL LINES (FALLBACK)
                // ============================================================

                foreach (Line line in lines)
                {
                    Line projected = ProjectLineToPlane(line, plane);
                    if (projected == null)
                        continue;

                    DetailCurve curve =
                        doc.Create.NewDetailCurve(view, projected);

                    if (curve != null)
                        created++;
                }

                t.Commit();
            }

            return created;
        }

        // =============================================================
        // Geometry helpers
        // =============================================================

        private static Line ProjectLineToPlane(Line line, Plane plane)
        {
            XYZ p0 = ProjectPoint(line.GetEndPoint(0), plane);
            XYZ p1 = ProjectPoint(line.GetEndPoint(1), plane);

            if (p0.IsAlmostEqualTo(p1))
                return null;

            return Line.CreateBound(p0, p1);
        }

        private static XYZ ProjectPoint(XYZ point, Plane plane)
        {
            XYZ v = point - plane.Origin;
            double d = v.DotProduct(plane.Normal);
            return point - d * plane.Normal;
        }
    }
}
