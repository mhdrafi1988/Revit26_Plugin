// ==================================================
// File: ProfileExtractor.cs
// ==================================================

using Autodesk.Revit.DB;
using Revit26_Plugin.RoofFromFloor.V009.Models;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.RoofFromFloor.V009.Geometry
{
    /// <summary>
    /// Extracts roof footprint curves EXACTLY as defined in Revit.
    /// Order and direction are preserved by design.
    /// </summary>
    public static class ProfileExtractor
    {
        public static RoofMemoryContext ExtractRoofContext(
            Document doc,
            FootPrintRoof roof)
        {
            var context = new RoofMemoryContext
            {
                RoofId = roof.Id,
                RoofLevel = doc.GetElement(roof.LevelId) as Level,
                RoofBaseElevation =
                    roof.get_Parameter(
                        BuiltInParameter.ROOF_LEVEL_OFFSET_PARAM)?.AsDouble() ?? 0
            };

            context.BoundingBox = roof.get_BoundingBox(null);

            double targetZ =
                context.RoofLevel.Elevation + context.RoofBaseElevation;

            ModelCurveArrArray profiles = roof.GetProfiles();

            foreach (ModelCurveArray loop in profiles)
            {
                foreach (ModelCurve mc in loop)
                {
                    Curve flat = FlattenCurveToZ(mc.GeometryCurve, targetZ);
                    context.RoofFootprintCurves.Add(flat);
                }
            }

            return context;
        }

        private static Curve FlattenCurveToZ(Curve curve, double z)
        {
            // Line — project both endpoints and rebuild.
            if (curve is Line line)
            {
                XYZ p0 = line.GetEndPoint(0);
                XYZ p1 = line.GetEndPoint(1);
                return Line.CreateBound(
                    new XYZ(p0.X, p0.Y, z),
                    new XYZ(p1.X, p1.Y, z));
            }

            // Arc — re-project center to z, keep radius and angular span.
            if (curve is Arc arc)
            {
                XYZ c = arc.Center;
                return Arc.Create(
                    new XYZ(c.X, c.Y, z),
                    arc.Radius,
                    arc.GetEndParameter(0),
                    arc.GetEndParameter(1),
                    arc.XDirection,
                    arc.YDirection);
            }

            // NurbSpline — move every control point to z.
            if (curve is NurbSpline ns)
            {
                IList<XYZ> flatPts = ns.CtrlPoints
                    .Select(p => new XYZ(p.X, p.Y, z))
                    .ToList();
                IList<double> weights = ns.Weights.Cast<double>().ToList();
                return NurbSpline.CreateCurve(flatPts, weights);
            }

            // HermiteSpline — move every control point to z.
            if (curve is HermiteSpline hs)
            {
                IList<XYZ> flatPts = hs.ControlPoints
                    .Select(p => new XYZ(p.X, p.Y, z))
                    .ToList();
                return HermiteSpline.Create(flatPts, hs.IsPeriodic);
            }

            // EllipticalArc — project center, keep both radii and span.
            if (curve is Ellipse el)
            {
                XYZ c = el.Center;
                return Ellipse.CreateCurve(
                    new XYZ(c.X, c.Y, z),
                    el.RadiusX,
                    el.RadiusY,
                    el.XDirection,
                    el.YDirection,
                    el.GetEndParameter(0),
                    el.GetEndParameter(1));
            }

            // Fallback: chord only — logs that a curve type was not handled.
            XYZ f0 = curve.GetEndPoint(0);
            XYZ f1 = curve.GetEndPoint(1);
            return Line.CreateBound(
                new XYZ(f0.X, f0.Y, z),
                new XYZ(f1.X, f1.Y, z));
        }
    }
}
