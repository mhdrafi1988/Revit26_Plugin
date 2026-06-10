using Autodesk.Revit.DB;
using Revit26_Plugin.PonitOnCurvesInnerandOuter.V01.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.PonitOnCurvesInnerandOuter.V01.Services
{
    public class RoofGeometryService
    {
        /// <summary>
        /// Extracts ALL loops (Outer and Inner) from the roof's top planar face.
        /// Returns them with shape classification, perimeter, and curved-length in metres.
        /// </summary>
        public List<RoofLoopModel> ExtractLoops(RoofBase roof)
        {
            var result = new List<RoofLoopModel>();
            Options opt = new Options { ComputeReferences = true };
            GeometryElement geoElem = roof.get_Geometry(opt);

            foreach (GeometryObject obj in geoElem)
            {
                if (!(obj is Solid solid)) continue;

                foreach (Face face in solid.Faces)
                {
                    if (!(face is PlanarFace pf)) continue;
                    if (!pf.FaceNormal.IsAlmostEqualTo(XYZ.BasisZ)) continue;

                    var loops = pf.GetEdgesAsCurveLoops().ToList();
                    if (!loops.Any()) continue;

                    double maxPerimeter = loops.Max(cl => cl.Sum(c => c.Length));
                    int idx = 0;

                    foreach (var cl in loops)
                    {
                        idx++;
                        double perimeterFt = cl.Sum(c => c.Length);
                        string loopType    = Math.Abs(perimeterFt - maxPerimeter) < 1e-6 ? "Outer" : "Inner";

                        string shapeType = ClassifyLoopShape(cl,
                            out XYZ center, out double radiusFt, out double curvedFt);

                        double perimeterM    = UnitUtils.ConvertFromInternalUnits(perimeterFt,  UnitTypeId.Meters);
                        double curvedM       = UnitUtils.ConvertFromInternalUnits(curvedFt,     UnitTypeId.Meters);
                        double radiusM       = UnitUtils.ConvertFromInternalUnits(radiusFt,     UnitTypeId.Meters);

                        var model = new RoofLoopModel
                        {
                            Index             = idx,
                            LoopType          = loopType,
                            LoopShapeType     = shapeType,
                            IsCircular        = shapeType == "Circular",
                            HasCurvedSegments = curvedFt > 1e-6,
                            PerimeterM        = perimeterM,
                            CurvedLengthM     = curvedM,
                            RadiusM           = radiusM,
                            Center            = center,
                            Radius            = radiusFt,
                            Geometry          = cl,
                        };

                        // Default point count based on perimeter (3 pts/m, min 4)
                        model.ManualCount = Math.Max(4, (int)Math.Round(3.0 * perimeterM));
                        model.RecalcPoints();

                        result.Add(model);
                    }
                }
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Shape classification
        // ─────────────────────────────────────────────────────────────────────────
        private string ClassifyLoopShape(CurveLoop loop,
            out XYZ center, out double radius, out double curvedLength)
        {
            center       = XYZ.Zero;
            radius       = 0;
            curvedLength = 0;

            var curves = loop.ToList();
            var arcs   = curves.OfType<Arc>().ToList();
            var lines  = curves.OfType<Line>().ToList();

            curvedLength = arcs.Sum(a => a.Length);

            // ── CIRCULAR: all arcs, same centre+radius, 360° total sweep ─────────
            if (arcs.Count == curves.Count && arcs.Count >= 1)
            {
                var centers   = arcs.Select(a => a.Center).ToList();
                var avgCenter = new XYZ(
                    centers.Average(p => p.X),
                    centers.Average(p => p.Y),
                    centers.Average(p => p.Z));

                bool centersClose = centers.All(c => c.DistanceTo(avgCenter) < 0.1);

                var    radii     = arcs.Select(a => a.Radius).ToList();
                double avgRadius = radii.Average();
                bool   radiusOk  = radii.All(r => Math.Abs(r - avgRadius) / avgRadius < 0.01);

                if (centersClose && radiusOk)
                {
                    double totalAngle = ComputeTotalArcAngle(arcs);
                    if (Math.Abs(totalAngle - 2 * Math.PI) < 0.05)
                    {
                        center = avgCenter;
                        radius = avgRadius;
                        return "Circular";
                    }
                }

                // All arcs but not a perfect circle → Oval
                return "Oval";
            }

            // ── OVAL/STADIUM: mix of arcs and lines ───────────────────────────────
            if (arcs.Count >= 1 && lines.Count >= 1)
            {
                bool looksOval = arcs.Count >= 2 && lines.Count >= 2;
                return looksOval ? "Oval" : "Arc";
            }

            // ── RECTANGLE: 4 straight lines at 90° ───────────────────────────────
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

        private double ComputeTotalArcAngle(List<Arc> arcs)
        {
            double total = 0;
            foreach (var a in arcs)
            {
                XYZ arcCenter = a.Center;
                XYZ startVec  = (a.GetEndPoint(0) - arcCenter).Normalize();
                XYZ endVec    = (a.GetEndPoint(1) - arcCenter).Normalize();
                total        += startVec.AngleTo(endVec);
            }
            return total;
        }
    }
}
