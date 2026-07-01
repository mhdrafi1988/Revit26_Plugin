using Autodesk.Revit.DB;
using Revit26_Plugin.RoofFromFloor.V008.Models;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.RoofFromFloor.V008.Services
{
    public static class FloorProfileService
    {
        public static List<ProfileLoop> ExtractFloorProfilesFromLink(
            Document hostDoc,
            RevitLinkInstance linkInstance,
            BoundingBoxXYZ roofBbox,
            double targetZ)
        {
            var results = new List<ProfileLoop>();

            Document linkDoc = linkInstance.GetLinkDocument();
            if (linkDoc == null) return results;

            Transform linkTransform = linkInstance.GetTransform();

            var floors = new FilteredElementCollector(linkDoc)
                .OfClass(typeof(Floor))
                .Cast<Floor>();

            foreach (var floor in floors)
            {
                var geom = floor.get_Geometry(new Options());
                if (geom == null) continue;

                foreach (var obj in geom)
                {
                    if (obj is Solid solid && solid.Faces.Size > 0)
                    {
                        var topFaces = solid.Faces
                            .OfType<PlanarFace>()
                            .Where(f => f.FaceNormal.IsAlmostEqualTo(XYZ.BasisZ));

                        foreach (var face in topFaces)
                        {
                            var loops = face.GetEdgesAsCurveLoops();
                            foreach (var loop in loops)
                            {
                                var profile = new ProfileLoop
                                {
                                    Source = ProfileSourceType.Floor
                                };

                                foreach (var c in loop)
                                {
                                    Curve hostCurve = c.CreateTransformed(linkTransform);
                                    // FIX: preserve curve type when flattening to Z
                                    Curve flat = FlattenCurveToZ(hostCurve, targetZ);

                                    if (IsCurveInsideXY(flat, roofBbox))
                                        profile.Curves.Add(flat);
                                }

                                if (profile.Curves.Count > 0)
                                    results.Add(profile);
                            }
                        }
                    }
                }
            }

            return results;
        }

        private static bool IsCurveInsideXY(Curve curve, BoundingBoxXYZ bbox)
        {
            XYZ p = curve.Evaluate(0.5, true);
            return p.X >= bbox.Min.X && p.X <= bbox.Max.X
                && p.Y >= bbox.Min.Y && p.Y <= bbox.Max.Y;
        }

        /// <summary>
        /// Projects a curve onto a flat Z plane while preserving its geometric type.
        /// Line → new Line. Arc → Arc with center at z. Splines → control points at z.
        /// </summary>
        private static Curve FlattenCurveToZ(Curve curve, double z)
        {
            if (curve is Line line)
            {
                XYZ p0 = line.GetEndPoint(0);
                XYZ p1 = line.GetEndPoint(1);
                return Line.CreateBound(
                    new XYZ(p0.X, p0.Y, z),
                    new XYZ(p1.X, p1.Y, z));
            }

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

            if (curve is NurbSpline ns)
            {
                IList<XYZ> flatPts = ns.CtrlPoints
                    .Select(p => new XYZ(p.X, p.Y, z))
                    .ToList();
                IList<double> weights = ns.Weights.Cast<double>().ToList();
                return NurbSpline.CreateCurve(flatPts, weights);
            }

            if (curve is HermiteSpline hs)
            {
                IList<XYZ> flatPts = hs.ControlPoints
                    .Select(p => new XYZ(p.X, p.Y, z))
                    .ToList();
                return HermiteSpline.Create(flatPts, hs.IsPeriodic);
            }

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

            // Fallback chord — unknown curve type
            XYZ f0 = curve.GetEndPoint(0);
            XYZ f1 = curve.GetEndPoint(1);
            return Line.CreateBound(
                new XYZ(f0.X, f0.Y, z),
                new XYZ(f1.X, f1.Y, z));
        }
    }
}
