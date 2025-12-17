// ============================================================
// File: DetailItemCreationService.cs
// Namespace: Revit26_Plugin.AutoLiner_V02.Services
// ============================================================

using Autodesk.Revit.DB;
using Revit26_Plugin.AutoLiner_V02.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.AutoLiner_V02.Services
{
    public static class DetailItemCreationService
    {
        public static ExecutionSummary Create(
            Document doc,
            View view,
            FamilySymbol symbol,
            IList<XYZ> corners,
            IList<XYZ> drains,
            Action<string> log)
        {
            var summary = new ExecutionSummary
            {
                Corners = corners.Count,
                Drains = drains.Count
            };

            log($"Family placement type: {symbol.Family.FamilyPlacementType}");

            if (symbol.Family.FamilyPlacementType
                != FamilyPlacementType.CurveBasedDetail)
            {
                log("❌ Selected family is NOT curve-based detail.");
                return summary;
            }

            if (!symbol.IsActive)
            {
                symbol.Activate();
                doc.Regenerate();
            }

            foreach (XYZ c in corners)
            {
                XYZ d = drains.OrderBy(x => x.DistanceTo(c)).First();

                XYZ p0 = ProjectToViewPlane(c, view);
                XYZ p1 = ProjectToViewPlane(d, view);

                if (p0.IsAlmostEqualTo(p1))
                {
                    summary.Failed++;
                    continue;
                }

                try
                {
                    Line line = Line.CreateBound(p0, p1);
                    doc.Create.NewFamilyInstance(line, symbol, view);
                    summary.Created++;
                }
                catch (Exception ex)
                {
                    log($"❌ Create failed: {ex.Message}");
                    summary.Failed++;
                }
            }

            return summary;
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
