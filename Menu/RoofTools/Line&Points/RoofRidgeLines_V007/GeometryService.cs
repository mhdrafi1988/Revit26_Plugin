using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V08.Services
{
    public class GeometryService : IGeometryService
    {
        private readonly double _tolerance;

        public GeometryService(double tolerance = 1e-6)
        {
            _tolerance = tolerance;
        }

        public DetailLine CreateDetailLine(Document doc, View view, XYZ p1, XYZ p2)
        {
            try
            {
                Line ln = Line.CreateBound(p1, p2);
                DetailCurve dc = doc.Create.NewDetailCurve(view, ln);
                return dc as DetailLine;
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"CreateDetailLine failed: {ex.Message}");
                return null;
            }
        }

        public List<DetailLine> CreatePerpendicularLines(Document doc, View view, RoofBase roof, XYZ p1, XYZ p2)
        {
            List<DetailLine> result = new List<DetailLine>();
            try
            {
                XYZ midpoint = (p1 + p2) * 0.5;
                XYZ dir = (p2 - p1).Normalize();
                if (dir.IsZeroLength())
                {
                    LoggerService.LogWarning("Direction vector is zero.");
                    return result;
                }

                XYZ perp = new XYZ(-dir.Y, dir.X, 0.0).Normalize();
                List<Curve> boundary = GetRoofBoundaryCurves(roof);

                if (boundary.Count == 0)
                {
                    LoggerService.LogWarning("No roof boundaries found.");
                    return result;
                }

                double z = midpoint.Z;
                List<Line> projected = boundary.Select(c =>
                    Line.CreateBound(
                        new XYZ(c.GetEndPoint(0).X, c.GetEndPoint(0).Y, z),
                        new XYZ(c.GetEndPoint(1).X, c.GetEndPoint(1).Y, z)
                    )).ToList();

                double maxRayLen = 100.0;
                Line rayFront = Line.CreateBound(midpoint, midpoint + perp * maxRayLen);
                Line rayBack = Line.CreateBound(midpoint, midpoint - perp * maxRayLen);

                List<XYZ> frontHits = IntersectWithBoundaries(rayFront, projected);
                List<XYZ> backHits = IntersectWithBoundaries(rayBack, projected);

                if (frontHits.Any())
                {
                    XYZ closest = frontHits.OrderBy(h => h.DistanceTo(midpoint)).First();
                    DetailLine frontLine = CreateDetailLine(doc, view, midpoint, closest);
                    if (frontLine != null) result.Add(frontLine);
                }

                if (backHits.Any())
                {
                    XYZ closest = backHits.OrderBy(h => h.DistanceTo(midpoint)).First();
                    DetailLine backLine = CreateDetailLine(doc, view, midpoint, closest);
                    if (backLine != null) result.Add(backLine);
                }

                LoggerService.LogInfo($"Created {result.Count} perpendicular lines.");
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"CreatePerpendicularLines failed: {ex.Message}");
            }
            return result;
        }

        public int AddShapePoints(Document doc, RoofBase roof, List<DetailLine> perpendicularLines, double interval = 1.0)
        {
            int count = 0;
            try
            {
                if (roof == null || perpendicularLines == null || perpendicularLines.Count == 0)
                    return 0;

                SlabShapeEditor editor = roof.GetSlabShapeEditor();
                if (editor == null) return 0;

                PlanarFace topFace = GetTopFace(roof);
                if (topFace == null) return 0;

                foreach (DetailLine line in perpendicularLines)
                {
                    Curve curve = line.GeometryCurve;
                    if (curve == null) continue;

                    XYZ start = curve.GetEndPoint(0);
                    XYZ end = curve.GetEndPoint(1);
                    double length = start.DistanceTo(end);
                    int intervals = (int)(length / interval);

                    if (intervals > 0)
                    {
                        for (int i = 1; i < intervals; i++)
                        {
                            double t = (double)i / intervals;
                            XYZ point = start + (end - start) * t;
                            IntersectionResult proj = topFace.Project(point);
                            if (proj != null)
                            {
                                editor.AddPoint(proj.XYZPoint);
                                count++;
                            }
                        }
                    }
                }
                LoggerService.LogInfo($"Added {count} shape points.");
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"AddShapePoints failed: {ex.Message}");
            }
            return count;
        }

        public List<Curve> GetRoofBoundaryCurves(RoofBase roof)
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
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"GetRoofBoundaryCurves failed: {ex.Message}");
            }
            return curves;
        }

        public PlanarFace GetTopFace(RoofBase roof)
        {
            try
            {
                Options opt = new Options();
                GeometryElement geo = roof.get_Geometry(opt);
                PlanarFace best = null;
                double maxArea = 0;

                foreach (GeometryObject obj in geo)
                {
                    if (obj is Solid s)
                    {
                        foreach (Face f in s.Faces)
                        {
                            if (f is PlanarFace pf && pf.FaceNormal.Z > 0.5 && pf.Area > maxArea)
                            {
                                maxArea = pf.Area;
                                best = pf;
                            }
                        }
                    }
                }
                return best;
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"GetTopFace failed: {ex.Message}");
                return null;
            }
        }

        private List<XYZ> IntersectWithBoundaries(Line ray, List<Line> boundaries)
        {
            List<XYZ> hits = new List<XYZ>();
            foreach (Line edge in boundaries)
            {
                IntersectionResultArray arr;
                SetComparisonResult comp = edge.Intersect(ray, out arr);
                if ((comp == SetComparisonResult.Overlap || comp == SetComparisonResult.Equal) && arr != null)
                {
                    foreach (IntersectionResult ir in arr)
                    {
                        hits.Add(ir.XYZPoint);
                    }
                }
            }
            return hits.Distinct(new XyzEqualityComparer(_tolerance)).ToList();
        }
    }

    public class XyzEqualityComparer : IEqualityComparer<XYZ>
    {
        private readonly double _tol;
        public XyzEqualityComparer(double tolerance) => _tol = tolerance;

        public bool Equals(XYZ a, XYZ b) => a?.DistanceTo(b) < _tol;

        public int GetHashCode(XYZ p) =>
            (p.X.GetHashCode() ^ p.Y.GetHashCode() ^ p.Z.GetHashCode());
    }
}