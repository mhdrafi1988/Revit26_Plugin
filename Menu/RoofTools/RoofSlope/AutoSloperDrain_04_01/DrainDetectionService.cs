using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using Revit22_Plugin.Asd_V4_01.Models;

namespace Revit22_Plugin.Asd_V4_01.Services
{
    public class DrainDetectionService
    {
        // =====================================================================
        // PUBLIC ENTRY
        // =====================================================================
        public List<DrainItem> DetectDrainsFromRoof(RoofBase roof, Face ignoredTopFace)
        {
            var drains = new List<DrainItem>();

            try
            {
                // A) SOLID GEOMETRY LOOPS (real physical cuts)
                drains.AddRange(DetectFromGeometryLoops(roof));

                // B) REVIT OPENING ELEMENTS
                drains.AddRange(DetectFromOpeningElements(roof));

                // C) REMOVE DUPLICATES
                drains = RemoveDuplicateDrains(drains);

                return drains
                    .OrderBy(d => d.Width * d.Height)
                    .ToList();
            }
            catch (Exception ex)
            {
                throw new Exception($"Drain detection failed: {ex.Message}");
            }
        }

        // =====================================================================
        // A) SOLID GEOMETRY (REAL HOLES)
        // =====================================================================
        private List<DrainItem> DetectFromGeometryLoops(RoofBase roof)
        {
            var drains = new List<DrainItem>();

            Options opt = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = true,
                DetailLevel = ViewDetailLevel.Fine
            };

            GeometryElement geom = roof.get_Geometry(opt);
            if (geom == null) return drains;

            foreach (GeometryObject go in geom)
            {
                Solid solid = go as Solid;
                if (solid == null || solid.Faces.Size == 0)
                    continue;

                foreach (Face face in GetUpwardFaces(solid, 0.95))
                {
                    // Find outer loop by largest perimeter
                    int outerIndex = FindOuterLoop(face);
                    if (outerIndex < 0) continue;

                    // Loop through ALL loops, keep everything except outer
                    for (int i = 0; i < face.EdgeLoops.Size; i++)
                    {
                        if (i == outerIndex)
                            continue; // skip only the outer loop

                        var loop = face.EdgeLoops.get_Item(i);
                        var result = CreateDrainFromLoop(loop, face);
                        if (result != null)
                            drains.Add(result);
                    }
                }
            }

            return drains;
        }

        // =====================================================================
        // OUTER LOOP DETECTION (OPTION 2 - LARGEST PERIMETER)
        // =====================================================================
        private int FindOuterLoop(Face face)
        {
            int outerIndex = -1;
            double maxPerimeter = 0;

            for (int i = 0; i < face.EdgeLoops.Size; i++)
            {
                var loop = face.EdgeLoops.get_Item(i);
                double perimeter = 0;

                foreach (Edge e in loop)
                {
                    Curve c = e.AsCurve();
                    if (c != null)
                        perimeter += c.Length;
                }

                if (perimeter > maxPerimeter)
                {
                    maxPerimeter = perimeter;
                    outerIndex = i;
                }
            }

            return outerIndex;
        }

        // =====================================================================
        // B) REVIT OPENING ELEMENTS
        // =====================================================================
        private List<DrainItem> DetectFromOpeningElements(RoofBase roof)
        {
            var doc = roof.Document;
            var drains = new List<DrainItem>();

            var openings = new FilteredElementCollector(doc)
                .OfClass(typeof(Opening))
                .Cast<Opening>()
                .Where(o => o.Host != null && o.Host.Id == roof.Id)
                .ToList();

            foreach (var op in openings)
            {
                try
                {
                    CurveArray arr = op.BoundaryCurves;
                    if (arr == null || arr.Size < 2) continue;

                    var pts = new List<XYZ>();
                    var arcs = new List<Arc>();
                    var curves = new List<Curve>();

                    foreach (Curve c in arr)
                    {
                        if (c is Arc a) arcs.Add(a);
                        curves.Add(c);

                        pts.Add(c.GetEndPoint(0));
                        pts.Add(c.GetEndPoint(1));
                    }

                    var drain = ClassifyOpeningShape(pts, arcs, curves, op.Id);
                    if (drain != null)
                        drains.Add(drain);
                }
                catch
                {
                    continue;
                }
            }

            return drains;
        }

        // =====================================================================
        // CREATE DRAIN FROM LOOP
        // =====================================================================
        private DrainItem CreateDrainFromLoop(EdgeArray loop, Face face)
        {
            var pts = new List<XYZ>();
            var arcs = new List<Arc>();
            var curves = new List<Curve>();

            foreach (Edge e in loop)
            {
                Curve c = e.AsCurve();
                if (c == null) continue;

                curves.Add(c);
                if (c is Arc arc) arcs.Add(arc);

                // project loop endpoints to face
                pts.Add(face.Project(c.GetEndPoint(0)).XYZPoint);
                pts.Add(face.Project(c.GetEndPoint(1)).XYZPoint);
            }

            return ClassifyOpeningShape(pts, arcs, curves, null);
        }

        // =====================================================================
        // SHAPE CLASSIFICATION
        // =====================================================================
        private DrainItem ClassifyOpeningShape(List<XYZ> pts, List<Arc> arcs, List<Curve> curves, ElementId id)
        {
            if (pts.Count < 4) return null;

            // A) Arc Cluster Circle
            var circ = TryArcClusterCircle(arcs);
            if (circ != null)
            {
                var (c, rFt) = circ.Value;
                double mm = rFt * 2 * 304.8;
                return new DrainItem(c, mm, mm, "Circle", id);
            }

            // B) Best Fit Circle
            var fit = TryBestFitCircle(pts);
            if (fit != null && fit.Value.residual / fit.Value.radiusFt < 0.10)
            {
                var (c, r, _) = fit.Value;
                double mm = r * 2 * 304.8;
                return new DrainItem(c, mm, mm, "Circle", id);
            }

            // C) Ellipse → becomes "Other"
            if (IsEllipse(pts))
            {
                BoundingDims(pts, out XYZ c, out double W, out double H);
                return new DrainItem(c, W, H, "Other", id);
            }

            // D) Rotated Rectangle
            if (IsRotatedRectangle(pts, out double rw, out double rh))
            {
                XYZ c = GetCentroid(pts);
                string norm = NormalizeShape("Rectangle", rw, rh);
                return new DrainItem(c, rw, rh, norm, id);
            }

            // E) Quadrilateral Soft Angles
            if (IsQuadWithSoftAngles(pts))
            {
                BoundingDims(pts, out XYZ c, out double W, out double H);
                string norm = NormalizeShape("Rectangle", W, H);
                return new DrainItem(c, W, H, norm, id);
            }

            // F) Polygon fallback
            BoundingDims(pts, out XYZ cp, out double WW, out double HH);
            string raw =
                (Math.Abs(WW - HH) < 5) ? "Square" :
                curves.All(c => c is Line) ? "Rectangle" :
                arcs.Count > 0 ? "Mixed" : "Polygon";

            string normalized = NormalizeShape(raw, WW, HH);

            return new DrainItem(cp, WW, HH, normalized, id);
        }

        // =====================================================================
        // NORMALIZE SHAPE (Circle, Rectangle, Square, Other)
        // =====================================================================
        private string NormalizeShape(string rawShape, double w, double h)
        {
            double diff = Math.Abs(w - h);

            // Circle already labeled earlier
            if (rawShape.Equals("Circle", StringComparison.OrdinalIgnoreCase))
                return "Circle";

            // Square
            if (diff < 5)
                return "Square";

            // Rectangle
            if (rawShape.Contains("Rectangle"))
                return "Rectangle";

            // Everything else
            return "Other";
        }

        // =====================================================================
        // UPWARD FACES
        // =====================================================================
        private IEnumerable<Face> GetUpwardFaces(Solid solid, double minNormalZ)
        {
            foreach (Face f in solid.Faces)
            {
                UV uv = new UV(0.5, 0.5);
                XYZ n = f.ComputeNormal(uv)?.Normalize();
                if (n == null) continue;

                if (n.Z >= minNormalZ)
                    yield return f;
            }
        }

        // =====================================================================
        // ARC CLUSTER CIRCLE
        // =====================================================================
        private (XYZ center, double radiusFt)? TryArcClusterCircle(List<Arc> arcs)
        {
            if (arcs.Count < 2) return null;

            var cluster = arcs
                .GroupBy(a => (Math.Round(a.Center.X, 2), Math.Round(a.Center.Y, 2)))
                .OrderByDescending(g => g.Count())
                .First();

            XYZ c = new XYZ(
                cluster.Average(a => a.Center.X),
                cluster.Average(a => a.Center.Y),
                cluster.Average(a => a.Center.Z));

            double radius = cluster.Average(a => a.Radius);

            return (c, radius);
        }

        // =====================================================================
        // BEST-FIT CIRCLE
        // =====================================================================
        private (XYZ center, double radiusFt, double residual)? TryBestFitCircle(List<XYZ> pts)
        {
            if (pts.Count < 3) return null;

            double avgX = pts.Average(p => p.X);
            double avgY = pts.Average(p => p.Y);

            double Sxx = 0, Syy = 0, Sxy = 0;
            double Sx = 0, Sy = 0;
            double Sx2y = 0, Sxy2 = 0;

            foreach (var p in pts)
            {
                double x = p.X - avgX;
                double y = p.Y - avgY;

                Sxx += x * x;
                Syy += y * y;
                Sxy += x * y;

                Sx += x;
                Sy += y;

                Sx2y += x * x * y;
                Sxy2 += x * y * y;
            }

            double C = pts.Count * Sxx - Sx * Sx;
            double E = pts.Count * Syy - Sy * Sy;
            double D = pts.Count * Sxy - Sx * Sy;

            double G = 0.5 * (pts.Count * (Sx2y + Sxy2) - Sx * (Sxx + Syy));
            double H = 0.5 * (pts.Count * (Sxy2 + Sx2y) - Sy * (Sxx + Syy));

            double den = (C * E - D * D);
            if (Math.Abs(den) < 1e-8) return null;

            double a = (G * E - D * H) / den;
            double b = (C * H - D * G) / den;

            double r = Math.Sqrt(
                (Sxx + Syy - 2 * a * Sx - 2 * b * Sy + pts.Count * (a * a + b * b)) / pts.Count);

            XYZ center = new XYZ(a + avgX, b + avgY, pts[0].Z);

            double residual = pts.Average(p => Math.Abs(p.DistanceTo(center) - r));

            return (center, r, residual);
        }

        // =====================================================================
        // ELLIPSE CHECK
        // =====================================================================
        private bool IsEllipse(List<XYZ> pts)
        {
            double meanX = pts.Average(p => p.X);
            double meanY = pts.Average(p => p.Y);

            double varX = pts.Sum(p => (p.X - meanX) * (p.X - meanX));
            double varY = pts.Sum(p => (p.Y - meanY) * (p.Y - meanY));

            double ratio = varX / varY;

            return (ratio > 1.3 && ratio < 7.0);
        }

        // =====================================================================
        // ROTATED RECTANGLE
        // =====================================================================
        private bool IsRotatedRectangle(List<XYZ> pts, out double w, out double h)
        {
            w = h = 0;
            if (pts.Count < 4) return false;

            XYZ c = GetCentroid(pts);
            var centered = pts.Select(p => new XYZ(p.X - c.X, p.Y - c.Y, 0)).ToList();

            double Sxx = centered.Sum(p => p.X * p.X);
            double Syy = centered.Sum(p => p.Y * p.Y);
            double Sxy = centered.Sum(p => p.X * p.Y);

            double theta = 0.5 * Math.Atan2(2 * Sxy, Sxx - Syy);

            double cos = Math.Cos(theta);
            double sin = Math.Sin(theta);

            var aligned = centered.Select(p =>
                new XYZ(p.X * cos + p.Y * sin, -p.X * sin + p.Y * cos, 0)).ToList();

            double minX = aligned.Min(p => p.X);
            double maxX = aligned.Max(p => p.X);
            double minY = aligned.Min(p => p.Y);
            double maxY = aligned.Max(p => p.Y);

            w = (maxX - minX) * 304.8;
            h = (maxY - minY) * 304.8;

            return true;
        }

        // =====================================================================
        // SOFT ANGLE QUAD
        // =====================================================================
        private bool IsQuadWithSoftAngles(List<XYZ> pts)
        {
            if (pts.Count != 4) return false;

            XYZ c = GetCentroid(pts);
            var sorted = pts.OrderBy(p => Math.Atan2(p.Y - c.Y, p.X - c.X)).ToList();

            for (int i = 0; i < 4; i++)
            {
                XYZ v1 = sorted[(i + 1) % 4] - sorted[i];
                XYZ v2 = sorted[(i + 2) % 4] - sorted[(i + 1) % 4];

                double dot = v1.Normalize().DotProduct(v2.Normalize());
                double ang = Math.Acos(dot) * (180 / Math.PI);

                if (Math.Abs(ang - 90) > 15)
                    return false;
            }
            return true;
        }

        // =====================================================================
        // HELPERS
        // =====================================================================
        private XYZ GetCentroid(List<XYZ> pts)
        {
            return new XYZ(
                pts.Average(p => p.X),
                pts.Average(p => p.Y),
                pts.Average(p => p.Z));
        }

        private void BoundingDims(List<XYZ> pts, out XYZ c, out double w, out double h)
        {
            double minX = pts.Min(p => p.X);
            double maxX = pts.Max(p => p.X);
            double minY = pts.Min(p => p.Y);
            double maxY = pts.Max(p => p.Y);
            double minZ = pts.Min(p => p.Z);
            double maxZ = pts.Max(p => p.Z);

            c = new XYZ((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);
            w = (maxX - minX) * 304.8;
            h = (maxY - minY) * 304.8;
        }

        // =====================================================================
        // REMOVE DUPLICATES
        // =====================================================================
        private List<DrainItem> RemoveDuplicateDrains(List<DrainItem> drains)
        {
            var list = new List<DrainItem>();

            foreach (var d in drains)
            {
                if (!list.Any(x => x.CenterPoint.DistanceTo(d.CenterPoint) < 0.01))
                {
                    list.Add(d);
                }
            }

            return list;
        }

        // =====================================================================
        // UI HELPERS (UNCHANGED)
        // =====================================================================
        public List<string> GenerateSizeCategories(List<DrainItem> drains)
        {
            var categories = new List<string>();

            categories.Add("All");

            categories.AddRange(
                drains
                    .Select(d => d.SizeCategory)
                    .Distinct()
                    .OrderBy(x => x)
            );

            categories.Add("Less than 100x100");
            categories.Add("100x100 - 200x200");
            categories.Add("200x200 - 300x300");
            categories.Add("Greater than 300x300");

            return categories;
        }

        public List<DrainItem> FilterDrainsBySize(List<DrainItem> drains, string filter)
        {
            if (filter == "All")
                return drains;

            if (filter == "Less than 100x100")
                return drains.Where(d => d.Width < 100 && d.Height < 100).ToList();

            if (filter == "100x100 - 200x200")
                return drains.Where(d =>
                    d.Width >= 100 && d.Width <= 200 &&
                    d.Height >= 100 && d.Height <= 200).ToList();

            if (filter == "200x200 - 300x300")
                return drains.Where(d =>
                    d.Width > 200 && d.Width <= 300 &&
                    d.Height > 200 && d.Height <= 300).ToList();

            if (filter == "Greater than 300x300")
                return drains.Where(d =>
                    d.Width > 300 && d.Height > 300).ToList();

            return drains.Where(d => d.SizeCategory == filter).ToList();
        }
    }
}
