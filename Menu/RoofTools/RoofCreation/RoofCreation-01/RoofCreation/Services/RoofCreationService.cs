using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.RoofFromFloor.V005.Services
{
    public static class RoofCreationService
    {
        private const double ExtraOffsetMm = 300.0;
        private const double MmToFeet = 1.0 / 304.8;
        private const double SnapToleranceFt = 10.0 / 304.8;

        public static bool TryCreateFootprintRoof(
            Document doc,
            List<CurveLoop> loops,
            RoofType roofType,
            Level level,
            double sourceRoofBaseOffset,
            Action<string> log)
        {
            if (doc == null || roofType == null || level == null || loops == null || loops.Count == 0)
            {
                log("? Invalid input(s) for roof creation.");
                return false;
            }

            // --------------------------------------------------
            // ORDER + ORIENT LOOP
            // --------------------------------------------------
            var ordered = TryOrderCurves(loops[0].ToList(), log);
            if (ordered == null || ordered.Count < 3)
            {
                log("? Failed to order footprint curves.");
                return false;
            }

            // ?? ENSURE CCW ORIENTATION
            if (!IsCounterClockwise(ordered))
            {
                log("? Footprint is clockwise. Reversing to CCW.");
                ordered.Reverse();
                ordered = ordered.Select(c => c.CreateReversed()).ToList();
            }

            log($"Final ordered footprint curves: {ordered.Count}");

            double finalBaseOffset =
                sourceRoofBaseOffset + (ExtraOffsetMm * MmToFeet);

            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    log($"--- Attempt {attempt}/3 ---");

                    using (Transaction tx = new Transaction(doc, "Create Roof From Floor"))
                    {
                        tx.Start();

                        double z = level.Elevation;

                        Plane plane = Plane.CreateByNormalAndOrigin(
                            XYZ.BasisZ,
                            new XYZ(0, 0, z));

                        SketchPlane sketchPlane = SketchPlane.Create(doc, plane);

                        CurveArray footprint = new CurveArray();

                        foreach (var c in ordered)
                        {
                            Curve flat = FlattenCurveToZ(c, z);
                            footprint.Append(flat);

                            // visual/debug only
                            doc.Create.NewModelCurve(flat, sketchPlane);
                        }

                        log("Calling NewFootPrintRoof()");

                        FootPrintRoof roof =
                            doc.Create.NewFootPrintRoof(
                                footprint,
                                level,
                                roofType,
                                out ModelCurveArray _);

                        Parameter offsetParam =
                            roof.get_Parameter(BuiltInParameter.ROOF_LEVEL_OFFSET_PARAM);

                        offsetParam?.Set(finalBaseOffset);

                        tx.Commit();
                    }

                    log("? ROOF CREATED SUCCESSFULLY");
                    return true;
                }
                catch (Exception ex)
                {
                    log($"? Attempt {attempt} failed: {ex.Message}");
                }
            }

            log("? Roof creation failed after 3 attempts.");
            return false;
        }

        // --------------------------------------------------
        // CURVE ORDERING
        // --------------------------------------------------
        private static List<Curve> TryOrderCurves(List<Curve> curves, Action<string> log)
        {
            List<Curve> ordered = new();
            Curve current = curves[0];
            ordered.Add(current);
            curves.RemoveAt(0);

            while (curves.Count > 0)
            {
                XYZ end = current.GetEndPoint(1);
                bool found = false;

                for (int i = 0; i < curves.Count; i++)
                {
                    Curve c = curves[i];

                    if (end.DistanceTo(c.GetEndPoint(0)) <= SnapToleranceFt)
                    {
                        ordered.Add(c);
                        current = c;
                        curves.RemoveAt(i);
                        found = true;
                        break;
                    }

                    if (end.DistanceTo(c.GetEndPoint(1)) <= SnapToleranceFt)
                    {
                        Curve r = c.CreateReversed();
                        ordered.Add(r);
                        current = r;
                        curves.RemoveAt(i);
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    log("? Curve ordering failed.");
                    return null;
                }
            }

            return ordered;
        }

        // --------------------------------------------------
        // ORIENTATION CHECK (CCW REQUIRED)
        // --------------------------------------------------
        private static bool IsCounterClockwise(List<Curve> curves)
        {
            double area = 0.0;

            for (int i = 0; i < curves.Count; i++)
            {
                XYZ p1 = curves[i].GetEndPoint(0);
                XYZ p2 = curves[(i + 1) % curves.Count].GetEndPoint(0);

                area += (p2.X - p1.X) * (p2.Y + p1.Y);
            }

            return area < 0; // CCW
        }

        // --------------------------------------------------
        // FLATTEN
        // --------------------------------------------------
        private static Curve FlattenCurveToZ(Curve c, double z)
        {
            XYZ p0 = c.GetEndPoint(0);
            XYZ p1 = c.GetEndPoint(1);

            return Line.CreateBound(
                new XYZ(p0.X, p0.Y, z),
                new XYZ(p1.X, p1.Y, z));
        }
    }
}
