using Autodesk.Revit.DB;
using Revit22_Plugin.RRLPV3.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit22_Plugin.RRLPV3.Services  // Fixed namespace
{
    public static class GeometryService
    {
        // Tolerance for XYZ comparisons (Revit feet)
        private const double TOLERANCE = 1e-6;

        /// <summary>
        /// 1. MAIN DETAIL LINE
        /// </summary>
        public static DetailLine CreateDetailLine(Document doc, View view, XYZ p1, XYZ p2)
        {
            try
            {
                Line ln = Line.CreateBound(p1, p2);
                DetailCurve dc = doc.Create.NewDetailCurve(view, ln);

                // Remove the LineStyle code since it's causing issues with null reference
                // Line style can be set separately if needed
                return dc as DetailLine;
            }
            catch (Exception ex)
            {
                Logger.LogError($"CreateDetailLine failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 2. PERPENDICULAR LINES
        /// </summary>
        public static List<DetailLine> CreatePerpendicularLines(Document doc, View view, RoofBase roof, XYZ p1, XYZ p2)
        {
            List<DetailLine> result = new List<DetailLine>();
            try
            {
                XYZ midpoint = (p1 + p2) * 0.5;
                XYZ dir = (p2 - p1).Normalize();
                if (dir.IsZeroLength())
                {
                    Logger.LogWarning("Direction vector is zero.");
                    return result;
                }

                // Pure 2D perpendicular (XY plane)
                XYZ perp = new XYZ(-dir.Y, dir.X, 0.0).Normalize();

                // Get roof boundary curves
                List<Curve> boundary = GetRoofBoundaryCurves(roof);
                if (boundary.Count == 0)
                {
                    Logger.LogWarning("No roof boundaries found.");
                    return result;
                }

                // Project to horizontal plane at midpoint Z
                double z = midpoint.Z;
                List<Line> projected = new List<Line>();
                foreach (Curve c in boundary)
                {
                    XYZ a = c.GetEndPoint(0);
                    XYZ b = c.GetEndPoint(1);
                    XYZ ap = new XYZ(a.X, a.Y, z);
                    XYZ bp = new XYZ(b.X, b.Y, z);
                    projected.Add(Line.CreateBound(ap, bp));
                }

                // Perpendicular rays from midpoint
                double maxRayLen = 100.0; // Reasonable default length
                Line rayFront = Line.CreateBound(midpoint, midpoint + perp * maxRayLen);
                Line rayBack = Line.CreateBound(midpoint, midpoint - perp * maxRayLen);

                List<XYZ> frontHits = IntersectWithBoundaries(rayFront, projected);
                List<XYZ> backHits = IntersectWithBoundaries(rayBack, projected);

                // Front perpendicular
                if (frontHits.Any())
                {
                    XYZ closest = frontHits.OrderBy(h => h.DistanceTo(midpoint)).First();
                    DetailLine frontLine = CreateDetailLine(doc, view, midpoint, closest);
                    if (frontLine != null) result.Add(frontLine);
                }

                // Back perpendicular
                if (backHits.Any())
                {
                    XYZ closest = backHits.OrderBy(h => h.DistanceTo(midpoint)).First();
                    DetailLine backLine = CreateDetailLine(doc, view, midpoint, closest);
                    if (backLine != null) result.Add(backLine);
                }

                Logger.LogInfo($"Created {result.Count} perpendicular lines.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"CreatePerpendicularLines failed: {ex.Message}");
            }
            return result;
        }

        /// <summary>
        /// 3. SHAPE POINTS (renamed from AddRoofEdgePoints)
        /// </summary>
        public static int AddShapePoints(Document doc, RoofBase roof, List<DetailLine> perpendicularLines, double interval = 1.0)
        {
            int count = 0;
            try
            {
                if (roof == null || perpendicularLines == null || perpendicularLines.Count == 0)
                    return 0;

                SlabShapeEditor editor = roof.GetSlabShapeEditor();
                if (editor == null)
                    return 0;

                PlanarFace topFace = GetTopFace(roof);
                if (topFace == null)
                    return 0;

                foreach (DetailLine line in perpendicularLines)
                {
                    Curve curve = line.GeometryCurve;
                    if (curve == null) continue;

                    XYZ start = curve.GetEndPoint(0);
                    XYZ end = curve.GetEndPoint(1);

                    // Calculate number of intervals
                    double length = start.DistanceTo(end);
                    int intervals = (int)(length / interval);

                    if (intervals > 0)
                    {
                        for (int i = 1; i < intervals; i++)
                        {
                            double t = (double)i / intervals;
                            XYZ point = start + (end - start) * t;

                            // Project to top face
                            IntersectionResult proj = topFace.Project(point);
                            if (proj != null)
                            {
                                editor.AddPoint(proj.XYZPoint);
                                count++;
                            }
                        }
                    }
                }
                Logger.LogInfo($"Added {count} shape points.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"AddShapePoints failed: {ex.Message}");
            }
            return count;
        }

        /// <summary>
        /// 4. ROOF BOUNDARY CURVES
        /// </summary>
        public static List<Curve> GetRoofBoundaryCurves(RoofBase roof)
        {
            List<Curve> curves = new List<Curve>();
            try
            {
                if (roof is FootPrintRoof fp)
                {
                    foreach (ModelCurveArray arr in fp.GetProfiles())
                    {
                        foreach (ModelCurve mc in arr)
                        {
                            if (mc?.GeometryCurve != null)
                                curves.Add(mc.GeometryCurve);
                        }
                    }
                }
                else if (roof is ExtrusionRoof er)
                {
                    // Handle extrusion roof boundaries
                    Options opt = new Options();
                    GeometryElement geo = roof.get_Geometry(opt);
                    foreach (GeometryObject obj in geo)
                    {
                        Solid s = obj as Solid;
                        if (s == null) continue;
                        foreach (Edge e in s.Edges)
                        {
                            curves.Add(e.AsCurve());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"GetRoofBoundaryCurves failed: {ex.Message}");
            }
            return curves;
        }

        /// <summary>
        /// 5. TOP FACE
        /// </summary>
        public static PlanarFace GetTopFace(RoofBase roof)
        {
            try
            {
                Options opt = new Options();
                GeometryElement geo = roof.get_Geometry(opt);
                PlanarFace best = null;
                double maxArea = 0;

                foreach (GeometryObject obj in geo)
                {
                    Solid s = obj as Solid;
                    if (s == null) continue;

                    foreach (Face f in s.Faces)
                    {
                        PlanarFace pf = f as PlanarFace;
                        if (pf?.FaceNormal.Z > 0.5 && pf.Area > maxArea)
                        {
                            maxArea = pf.Area;
                            best = pf;
                        }
                    }
                }
                return best;
            }
            catch (Exception ex)
            {
                Logger.LogError($"GetTopFace failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Helper: Intersect line with boundary projections
        /// </summary>
        private static List<XYZ> IntersectWithBoundaries(Line ray, List<Line> boundaries)
        {
            List<XYZ> hits = new List<XYZ>();
            foreach (Line edge in boundaries)
            {
                IntersectionResultArray arr;
                SetComparisonResult comp = edge.Intersect(ray, out arr);
                if (comp == SetComparisonResult.Overlap || comp == SetComparisonResult.Equal)
                {
                    if (arr != null)
                    {
                        foreach (IntersectionResult ir in arr)
                        {
                            hits.Add(ir.XYZPoint);
                        }
                    }
                }
            }
            return hits.Distinct(new XyzEqualityComparer(TOLERANCE)).ToList();
        }
    }

    /// <summary>
    /// XYZ Equality Comparer
    /// </summary>
    public class XyzEqualityComparer : IEqualityComparer<XYZ>
    {
        private readonly double tol;
        public XyzEqualityComparer(double tolerance) => tol = tolerance;

        public bool Equals(XYZ a, XYZ b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            return a.DistanceTo(b) < tol;
        }

        public int GetHashCode(XYZ p)
        {
            if (p is null) return 0;
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + p.X.GetHashCode();
                hash = hash * 23 + p.Y.GetHashCode();
                hash = hash * 23 + p.Z.GetHashCode();
                return hash;
            }
        }
    }
}