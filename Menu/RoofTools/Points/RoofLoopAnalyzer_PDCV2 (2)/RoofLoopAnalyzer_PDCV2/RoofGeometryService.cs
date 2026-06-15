using Autodesk.Revit.DB;
using Revit26_Plugin.PDCV2.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.PDCV2.Services
{
    public class RoofGeometryService
    {
        public List<RoofLoopModel> ExtractCircularLoops(RoofBase roof)
        {
            var result = new List<RoofLoopModel>();
            Options opt = new Options { ComputeReferences = true };
            GeometryElement geoElem = roof.get_Geometry(opt);

            foreach (GeometryObject obj in geoElem)
            {
                if (obj is Solid solid)
                {
                    foreach (Face face in solid.Faces)
                    {
                        if (face is PlanarFace pf && pf.FaceNormal.IsAlmostEqualTo(XYZ.BasisZ))
                        {
                            var loops = pf.GetEdgesAsCurveLoops();
                            int idx = 0;

                            var loopInfos = loops.Select(cl =>
                            {
                                string shapeType = ClassifyLoopShape(cl, out XYZ center, out double radius);
                                return new
                                {
                                    Index     = ++idx,
                                    CurveLoop = cl,
                                    Perimeter = cl.Sum(c => c.Length),
                                    ShapeType = shapeType,
                                    Center    = center,
                                    Radius    = radius
                                };
                            }).ToList();

                            double maxPerimeter = loopInfos.Max(l => l.Perimeter);

                            foreach (var loopInfo in loopInfos)
                            {
                                string loopType = Math.Abs(loopInfo.Perimeter - maxPerimeter) < 1e-6
                                    ? "Outer"
                                    : "Inner";

                                result.Add(new RoofLoopModel
                                {
                                    Index             = loopInfo.Index,
                                    PerimeterMm       = UnitUtils.ConvertFromInternalUnits(loopInfo.Perimeter, UnitTypeId.Millimeters),
                                    LoopType          = loopType,
                                    IsCircular        = loopInfo.ShapeType == "Circular",
                                    LoopShapeType     = loopInfo.ShapeType,
                                    Center            = loopInfo.Center,
                                    Radius            = loopInfo.Radius,
                                    RecommendedPoints = 8,
                                    Geometry          = loopInfo.CurveLoop
                                });
                            }
                        }
                    }
                }
            }

            return result;
        }

        private string ClassifyLoopShape(CurveLoop loop, out XYZ center, out double radius)
        {
            center = XYZ.Zero;
            radius = 0;

            var curves = loop.ToList();

            // CIRCULAR: All arcs with same center and radius, 360° sweep
            var arcs = curves.OfType<Arc>().ToList();
            if (arcs.Count == curves.Count && arcs.Count >= 3)
            {
                var centers = arcs.Select(a => a.Center).ToList();
                var avgCenter = new XYZ(
                    centers.Average(p => p.X),
                    centers.Average(p => p.Y),
                    centers.Average(p => p.Z));

                bool centersClose = centers.All(c => c.DistanceTo(avgCenter) < 0.1);
                if (!centersClose) return "Other";

                var radii = arcs.Select(a => a.Radius).ToList();
                double avgRadius = radii.Average();
                bool radiusClose = radii.All(r => Math.Abs(r - avgRadius) / avgRadius < 0.01);
                if (!radiusClose) return "Other";

                double totalAngle = arcs.Sum(a =>
                {
                    XYZ arcCenter = a.Center;
                    XYZ startVec  = (a.GetEndPoint(0) - arcCenter).Normalize();
                    XYZ endVec    = (a.GetEndPoint(1) - arcCenter).Normalize();
                    return startVec.AngleTo(endVec);
                });

                if (Math.Abs(totalAngle - 2 * Math.PI) > 0.05)
                    return "Other";

                center = avgCenter;
                radius = avgRadius;
                return "Circular";
            }

            // RECTANGLE: 4 straight lines with 90-degree corners
            var lines = curves.OfType<Line>().ToList();
            if (lines.Count == 4 && curves.Count == 4)
            {
                var angles = new List<double>();
                for (int i = 0; i < 4; i++)
                {
                    var a  = lines[i];
                    var b  = lines[(i + 1) % 4];
                    XYZ v1 = (a.GetEndPoint(1) - a.GetEndPoint(0)).Normalize();
                    XYZ v2 = (b.GetEndPoint(1) - b.GetEndPoint(0)).Normalize();
                    angles.Add(v1.AngleTo(v2));
                }

                if (angles.All(a => Math.Abs(a - Math.PI / 2) < 0.1))
                    return "Rectangle";
            }

            return "Other";
        }
    }
}
