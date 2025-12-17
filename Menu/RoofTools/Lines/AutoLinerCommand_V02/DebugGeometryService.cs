// ============================================================
// File: DebugGeometryService.cs
// Namespace: Revit26_Plugin.AutoLiner_V02.Services
// ============================================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.AutoLiner_V02.Services
{
    public static class DebugGeometryService
    {
        public static void DrawDebugLines(
            Document doc,
            View view,
            IList<XYZ> corners,
            IList<XYZ> drains,
            Action<string> log)
        {
            if (view is not ViewPlan)
            {
                log("⚠ Debug skipped: not a plan view.");
                return;
            }

            log("🟡 Drawing debug detail curves...");

            foreach (XYZ c in corners)
            {
                XYZ d = drains.OrderBy(x => x.DistanceTo(c)).First();

                XYZ p0 = ProjectToViewPlane(c, view);
                XYZ p1 = ProjectToViewPlane(d, view);

                if (p0.IsAlmostEqualTo(p1))
                    continue;

                Line line = Line.CreateBound(p0, p1);
                doc.Create.NewDetailCurve(view, line);
            }

            log("🟢 Debug detail curves created.");
        }

        private static XYZ ProjectToViewPlane(XYZ p, View view)
        {
            Plane plane = Plane.CreateByNormalAndOrigin(
                view.ViewDirection,
                view.Origin);

            plane.Project(p, out _, out double dist);
            return p - view.ViewDirection * dist;
        }
    }
}
