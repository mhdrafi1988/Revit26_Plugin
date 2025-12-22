// ============================================================
// File: DetailItemCreationService.cs
// Namespace: Revit26_Plugin.AutoLiner_V02.Services
// ============================================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.AutoLiner_V02.Services
{
    public static class DetailItemCreationService
    {
        private const double TWO_METERS =
            2.0 / 0.3048; // feet (Revit internal units)

        // --------------------------------------------------------
        // TRANSACTION 1 — Corner → Drain
        // --------------------------------------------------------
        public static void CreateCornerToDrainLines(
            Document doc,
            View view,
            FamilySymbol symbol,
            IList<XYZ> corners,
            IList<XYZ> drains,
            Action<string> log)
        {
            if (view is not ViewPlan)
                throw new InvalidOperationException("Active view must be a Plan View.");

            ValidateFamily(symbol);

            using Transaction tx =
                new Transaction(doc, "AutoLiner – Corner to Drain");

            tx.Start();
            log("🟡 Corner → Drain transaction started");

            ActivateSymbolIfNeeded(doc, symbol);

            foreach (XYZ corner in corners)
            {
                XYZ nearestDrain =
                    drains.OrderBy(d => d.DistanceTo(corner)).First();

                XYZ p0 = ProjectToViewPlane(corner, view);
                XYZ p1 = ProjectToViewPlane(nearestDrain, view);

                if (p0.IsAlmostEqualTo(p1))
                    continue;

                Line line = Line.CreateBound(p0, p1);
                doc.Create.NewFamilyInstance(line, symbol, view);
            }

            tx.Commit();
            log("🟢 Corner → Drain transaction committed");
        }

        // --------------------------------------------------------
        // TRANSACTION 2 — Ridge → Drain
        // --------------------------------------------------------
        public static void CreateRidgeToDrainLines(
            Document doc,
            View view,
            FamilySymbol symbol,
            IList<XYZ> drains,
            Action<string> log)
        {
            if (view is not ViewPlan)
                throw new InvalidOperationException("Active view must be a Plan View.");

            ValidateFamily(symbol);

            using Transaction tx =
                new Transaction(doc, "AutoLiner – Ridge to Drain");

            tx.Start();
            log("🟡 Ridge → Drain transaction started");

            ActivateSymbolIfNeeded(doc, symbol);

            for (int i = 0; i < drains.Count; i++)
            {
                for (int j = i + 1; j < drains.Count; j++)
                {
                    XYZ d1 = drains[i];
                    XYZ d2 = drains[j];

                    if (d1.DistanceTo(d2) <= TWO_METERS)
                        continue;

                    XYZ ridge =
                        new XYZ(
                            (d1.X + d2.X) / 2,
                            (d1.Y + d2.Y) / 2,
                            (d1.Z + d2.Z) / 2);

                    XYZ rp = ProjectToViewPlane(ridge, view);
                    XYZ p1 = ProjectToViewPlane(d1, view);
                    XYZ p2 = ProjectToViewPlane(d2, view);

                    if (!rp.IsAlmostEqualTo(p1))
                    {
                        Line l1 = Line.CreateBound(rp, p1);
                        doc.Create.NewFamilyInstance(l1, symbol, view);
                    }

                    if (!rp.IsAlmostEqualTo(p2))
                    {
                        Line l2 = Line.CreateBound(rp, p2);
                        doc.Create.NewFamilyInstance(l2, symbol, view);
                    }
                }
            }

            tx.Commit();
            log("🟢 Ridge → Drain transaction committed");
        }

        // --------------------------------------------------------
        // Helpers
        // --------------------------------------------------------
        private static void ValidateFamily(FamilySymbol symbol)
        {
            if (symbol.Family.FamilyPlacementType
                != FamilyPlacementType.CurveBasedDetail)
            {
                throw new InvalidOperationException(
                    "Selected family must be Curve-Based Detail.");
            }
        }

        private static void ActivateSymbolIfNeeded(
            Document doc,
            FamilySymbol symbol)
        {
            if (!symbol.IsActive)
            {
                symbol.Activate();
                doc.Regenerate();
            }
        }

        private static XYZ ProjectToViewPlane(XYZ p, View view)
        {
            Plane plane =
                Plane.CreateByNormalAndOrigin(
                    view.ViewDirection,
                    view.Origin);

            plane.Project(p, out _, out double dist);
            return p - view.ViewDirection * dist;
        }
    }
}
