using Autodesk.Revit.DB;
using Revit26_Plugin.Asd_19.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.Asd_19.Services
{
    public class DrainDetectionService
    {
        private const double TOLERANCE_MM = 5.0; // 5mm tolerance for drain vertex detection
        private List<SlabShapeVertex> _allShapeVertices; // Store all shape vertices for detection

        public List<DrainItem> DetectDrainsFromRoof(RoofBase roof, Face ignoredTopFace, List<SlabShapeVertex> allShapeVertices = null)
        {
            _allShapeVertices = allShapeVertices ?? new List<SlabShapeVertex>();
            var drains = new List<DrainItem>();

            try
            {
                var opts = new Options { ComputeReferences = false, DetailLevel = ViewDetailLevel.Medium };
                GeometryElement geom = roof.get_Geometry(opts);
                if (geom == null) return drains;

                foreach (GeometryObject go in geom)
                {
                    if (go is Solid solid && solid.Faces.Size > 0)
                    {
                        // Only upward-facing roof surfaces (no bottoms)
                        var topFaces = GetUpwardFaces(solid, 0.1); // normal.Z ≥ 0.1 ⇒ facing up (≈6° slope)

                        foreach (var face in topFaces)
                            drains.AddRange(DrainsFromFaceLoops(face));
                    }
                }

                drains = RemoveDuplicateDrains(drains);
                drains = drains.OrderBy(d => d.Width * d.Height).ToList();
                return drains;
            }
            catch (Exception ex)
            {
                throw new Exception($"Drain detection failed: {ex.Message}");
            }
        }

        // 🔹 Return only upward faces
        private IEnumerable<Face> GetUpwardFaces(Solid solid, double minNZ)
        {
            var faces = new List<Face>();
            foreach (Face f in solid.Faces)
            {
                try
                {
                    var bb = f.GetBoundingBox();
                    var uv = new UV((bb.Min.U + bb.Max.U) / 2, (bb.Min.V + bb.Max.V) / 2);
                    var n = f.ComputeNormal(uv);
                    if (n != null && n.Normalize().Z >= minNZ)
                        faces.Add(f);
                }
                catch { /* skip invalid faces */ }
            }
            return faces;
        }

        private List<DrainItem> DrainsFromFaceLoops(Face face)
        {
            var list = new List<DrainItem>();
            if (face?.EdgeLoops == null) return list;

            // Loop 0 = outer boundary; inner loops start from 1
            for (int i = 1; i < face.EdgeLoops.Size; i++)
            {
                var edgeLoop = face.EdgeLoops.get_Item(i);
                var drain = CreateDrainFromEdgeLoop(edgeLoop, face);
                if (drain != null) list.Add(drain);
            }

            return list;
        }

        // ✅ Updated to find shape vertices within 5mm of the loop
        private DrainItem CreateDrainFromEdgeLoop(EdgeArray loop, Face face)
        {
            try
            {
                var curves = new List<Curve>();
                var arcs = new List<Arc>();
                var pts = new List<XYZ>();

                foreach (Edge e in loop)
                {
                    var c = e.AsCurve();
                    if (c == null) continue;

                    curves.Add(c);
                    if (c is Arc a) arcs.Add(a);

                    pts.Add(ProjectToFace(face, c.GetEndPoint(0)));
                    pts.Add(ProjectToFace(face, c.GetEndPoint(1)));
                }

                if (pts.Count < 3) return null;

                // Calculate bounding box dimensions for size validation
                double minX = pts.Min(p => p.X), maxX = pts.Max(p => p.X);
                double minY = pts.Min(p => p.Y), maxY = pts.Max(p => p.Y);

                double w = (maxX - minX) * 304.8; // Width in mm
                double h = (maxY - minY) * 304.8; // Height in mm

                // Validate size range
                if (w < 5 || h < 5 || w > 2000 || h > 2000) return null;

                // NEW: Find all shape vertices within 5mm of this loop
                var drainVertices = FindShapeVerticesOnLoop(curves, _allShapeVertices);

                // Determine shape type
                string shape = DetermineShapeType(curves, arcs, w, h);

                // Create drain item with the found vertices
                return new DrainItem(drainVertices, curves, w, h, shape);
            }
            catch { return null; }
        }

        // NEW: Find shape vertices within 5mm tolerance of any curve in the loop
        private List<SlabShapeVertex> FindShapeVerticesOnLoop(List<Curve> loopCurves, List<SlabShapeVertex> allVertices)
        {
            var matchingVertices = new List<SlabShapeVertex>();
            double toleranceFeet = TOLERANCE_MM / 304.8;

            if (allVertices == null || allVertices.Count == 0)
                return matchingVertices;

            foreach (var vertex in allVertices)
            {
                if (vertex?.Position == null) continue;

                foreach (var curve in loopCurves)
                {
                    try
                    {
                        double distance = curve.Distance(vertex.Position);
                        if (distance < toleranceFeet)
                        {
                            matchingVertices.Add(vertex);
                            break; // Found a match, no need to check other curves
                        }
                    }
                    catch
                    {
                        // Skip problematic curves
                    }
                }
            }

            return matchingVertices;
        }

        // NEW: Determine shape type based on curves and dimensions
        private string DetermineShapeType(List<Curve> curves, List<Arc> arcs, double widthMm, double heightMm)
        {
            // Try circular detection first
            var circ = TryArcClusterCircle(arcs);
            if (circ != null)
            {
                return "Circle";
            }

            // Check if it's a square (within 5mm tolerance)
            if (Math.Abs(widthMm - heightMm) < 5)
            {
                return "Square";
            }

            // Check if all curves are lines
            if (curves.All(c => c is Line))
            {
                return "Rectangle";
            }

            // Mixed shape with arcs
            if (arcs.Count > 0)
            {
                return "Mixed Shape";
            }

            return "Polygon";
        }

        private XYZ ProjectToFace(Face face, XYZ p)
        {
            try { return face.Project(p)?.XYZPoint ?? p; } catch { return p; }
        }

        // group arcs within ±5 mm center & radius tolerance
        private (XYZ center, double radiusFt)? TryArcClusterCircle(List<Arc> arcs)
        {
            if (arcs == null || arcs.Count < 2) return null;
            double tolFt = 5.0 / 304.8;
            var clusters = new List<List<Arc>>();

            foreach (var a in arcs)
            {
                bool matched = false;
                foreach (var cl in clusters)
                {
                    var cAvg = new XYZ(cl.Average(x => x.Center.X),
                                       cl.Average(x => x.Center.Y),
                                       cl.Average(x => x.Center.Z));
                    double rAvg = cl.Average(x => x.Radius);

                    if (a.Center.DistanceTo(cAvg) < tolFt && Math.Abs(a.Radius - rAvg) < tolFt)
                    {
                        cl.Add(a);
                        matched = true;
                        break;
                    }
                }
                if (!matched) clusters.Add(new List<Arc> { a });
            }

            var best = clusters.OrderByDescending(c => c.Count).FirstOrDefault();
            if (best == null || best.Count < 2) return null;

            var center = new XYZ(best.Average(x => x.Center.X),
                                 best.Average(x => x.Center.Y),
                                 best.Average(x => x.Center.Z));
            double radius = best.Average(x => x.Radius);
            return (center, radius);
        }

        // best-fit circle in XY
        private (XYZ center, double radiusFt, double residual)? TryBestFitCircle(List<XYZ> pts)
        {
            if (pts == null || pts.Count < 3) return null;
            var cx = pts.Average(p => p.X); var cy = pts.Average(p => p.Y); var cz = pts.Average(p => p.Z);
            var uv = pts.Select(p => (x: p.X - cx, y: p.Y - cy)).ToList();

            double Sx = 0, Sy = 0, Sxx = 0, Syy = 0, Sxy = 0, Sxxx = 0, Syyy = 0, Sxxy = 0, Sxyy = 0;
            foreach (var p in uv)
            {
                double x = p.x, y = p.y, xx = x * x, yy = y * y;
                Sx += x; Sy += y; Sxx += xx; Syy += yy; Sxy += x * y;
                Sxxx += xx * x; Syyy += yy * y; Sxxy += xx * y; Sxyy += x * yy;
            }
            double N = uv.Count;
            double C = N * Sxx - Sx * Sx;
            double D = N * Sxy - Sx * Sy;
            double E = N * Syy - Sy * Sy;
            double G = 0.5 * (N * (Sxxx + Sxyy) - Sx * (Sxx + Syy));
            double H = 0.5 * (N * (Syyy + Sxxy) - Sy * (Sxx + Syy));
            double den = (C * E - D * D);
            if (Math.Abs(den) < 1e-9) return null;

            double a = (G * E - D * H) / den;
            double b = (C * H - D * G) / den;
            double r = Math.Sqrt((Sxx + Syy - 2 * a * Sx - 2 * b * Sy + N * (a * a + b * b)) / N);
            double res = uv.Select(p => Math.Abs(Math.Sqrt((p.x - a) * (p.x - a) + (p.y - b) * (p.y - b)) - r)).Average();

            var center = new XYZ(cx + a, cy + b, cz);
            return (center, r, res);
        }

        private List<DrainItem> RemoveDuplicateDrains(List<DrainItem> drains)
        {
            var unique = new List<DrainItem>();
            const double tol = 0.01; // ≈3 mm
            foreach (var d in drains)
                if (!unique.Any(x => x.CenterPoint.DistanceTo(d.CenterPoint) < tol))
                    unique.Add(d);
            return unique;
        }

        // ---- UI helpers ----
        public List<string> GenerateSizeCategories(List<DrainItem> drains)
        {
            var categories = new List<string> { "All" };
            categories.AddRange(drains.Select(d => d.SizeCategory).Distinct().OrderBy(s => s));
            categories.Add("Less than 100x100");
            categories.Add("100x100 - 200x200");
            categories.Add("200x200 - 300x300");
            categories.Add("Greater than 300x300");
            return categories;
        }

        public List<DrainItem> FilterDrainsBySize(List<DrainItem> drains, string filter)
        {
            if (filter == "All") return drains;
            if (filter == "Less than 100x100") return drains.Where(d => d.Width < 100 && d.Height < 100).ToList();
            if (filter == "100x100 - 200x200") return drains.Where(d => d.Width >= 100 && d.Width <= 200 && d.Height >= 100 && d.Height <= 200).ToList();
            if (filter == "200x200 - 300x300") return drains.Where(d => d.Width > 200 && d.Width <= 300 && d.Height > 200 && d.Height <= 300).ToList();
            if (filter == "Greater than 300x300") return drains.Where(d => d.Width > 300 && d.Height > 300).ToList();
            return drains.Where(d => d.SizeCategory == filter).ToList();
        }

        // UPDATED: Use pre-identified drain vertices instead of searching
        public List<SlabShapeVertex> FindVerticesForDrain(DrainItem drain, List<SlabShapeVertex> allVertices, Face topFace)
        {
            // Simply return the pre-identified drain vertices
            // These were already found during detection using 5mm tolerance
            return drain.DrainVertices ?? new List<SlabShapeVertex>();
        }
    }
}