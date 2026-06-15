using Autodesk.Revit.DB;
using Revit26_Plugin.PDCV3.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.PDCV3.Services
{
    public class RoofGeometryService
    {
        /// <summary>
        /// Extracts ALL loops (Outer and Inner) from the roof's top planar face.
        /// Outer loops that contain curved segments are flagged for point placement.
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

                    // Identify outer loop by largest perimeter
                    double maxPerimeter = loops.Max(cl => cl.Sum(c => c.Length));
                    int idx = 0;

                    foreach (var cl in loops)
                    {
                        idx++;
                        double perimeter = cl.Sum(c => c.Length);
                        string loopType  = Math.Abs(perimeter - maxPerimeter) < 1e-6 ? "Outer" : "Inner";

                        string shapeType = ClassifyLoopShape(cl, out XYZ center, out double radius,
                                                              out double curvedLength);

                        bool hasCurved   = curvedLength > 1e-6;

                        result.Add(new RoofLoopModel
                        {
                            Index             = idx,
                            PerimeterMm       = UnitUtils.ConvertFromInternalUnits(perimeter, UnitTypeId.Millimeters),
                            CurvedLengthMm    = UnitUtils.ConvertFromInternalUnits(curvedLength, UnitTypeId.Millimeters),
                            LoopType          = loopType,
                            IsCircular        = shapeType == "Circular",
                            LoopShapeType     = shapeType,
                            HasCurvedSegments = hasCurved,
                            Center            = center,
                            Radius            = radius,
                            RecommendedPoints = 8,
                            Geometry          = cl
                        });
                    }
                }
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Shape classification
        // curvedLength = total arc length of all Arc segments in the loop (feet)
        // ─────────────────────────────────────────────────────────────────────────
        private string ClassifyLoopShape(CurveLoop loop, out XYZ center, out double radius,
                                          out double curvedLength)
        {
            center       = XYZ.Zero;
            radius       = 0;
            curvedLength = 0;

            var curves = loop.ToList();
            var arcs   = curves.OfType<Arc>().ToList();
            var lines  = curves.OfType<Line>().ToList();

            curvedLength = arcs.Sum(a => a.Length);

            // ── CIRCULAR: all arcs, same center+radius, 360° total sweep ──────────
            if (arcs.Count == curves.Count && arcs.Count >= 1)
            {
                var centers  = arcs.Select(a => a.Center).ToList();
                var avgCenter = new XYZ(
                    centers.Average(p => p.X),
                    centers.Average(p => p.Y),
                    centers.Average(p => p.Z));

                bool centersClose = centers.All(c => c.DistanceTo(avgCenter) < 0.1);

                var radii      = arcs.Select(a => a.Radius).ToList();
                double avgRadius = radii.Average();
                bool radiusClose = radii.All(r => Math.Abs(r - avgRadius) / avgRadius < 0.01);

                if (centersClose && radiusClose)
                {
                    double totalAngle = ComputeTotalArcAngle(arcs);
                    if (Math.Abs(totalAngle - 2 * Math.PI) < 0.05)
                    {
                        center = avgCenter;
                        radius = avgRadius;
                        return "Circular";
                    }
                }

                // All arcs but not a perfect circle → Oval (e.g. ellipse-like, stadium)
                return "Oval";
            }

            // ── OVAL/STADIUM: mix of arcs and lines forming a closed oval shape ───
            if (arcs.Count >= 1 && lines.Count >= 1)
            {
                // Check if it looks like a stadium (2 arcs + 2 lines) or rounded-rect
                bool looksOval = arcs.Count >= 2 && lines.Count >= 2;
                if (looksOval) return "Oval";

                // Single arc with lines → generic Arc shape
                return "Arc";
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
                total += startVec.AngleTo(endVec);
            }
            return total;
        }
    }
}
