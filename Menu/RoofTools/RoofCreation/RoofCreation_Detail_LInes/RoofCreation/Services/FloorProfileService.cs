using Autodesk.Revit.DB;
using Revit26_Plugin.RoofFromFloor.Models;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.RoofFromFloor.Services
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

        private static Curve FlattenCurveToZ(Curve curve, double z)
        {
            XYZ p0 = curve.GetEndPoint(0);
            XYZ p1 = curve.GetEndPoint(1);

            return Line.CreateBound(
                new XYZ(p0.X, p0.Y, z),
                new XYZ(p1.X, p1.Y, z));
        }
    }
}
