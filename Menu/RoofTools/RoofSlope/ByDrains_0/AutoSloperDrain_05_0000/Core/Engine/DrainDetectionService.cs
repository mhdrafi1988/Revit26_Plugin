using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlope.V5_00.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.AutoSlope.V5_00.Core.Engine
{
    public class DrainDetectionService
    {
        public List<DrainItem> DetectDrainsFromRoof(RoofBase roof, Face ignoredTopFace)
        {
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
                        var topFaces = GetUpwardFaces(solid, 0.1);

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
                catch { }
            }
            return faces;
        }

        private List<DrainItem> DrainsFromFaceLoops(Face face)
        {
            var list = new List<DrainItem>();
            if (face?.EdgeLoops == null) return list;

            for (int i = 1; i < face.EdgeLoops.Size; i++)
            {
                var edgeLoop = face.EdgeLoops.get_Item(i);
                var drain = CreateDrainFromEdgeLoop(edgeLoop, face);
                if (drain != null) list.Add(drain);
            }
            return list;
        }

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

                // Try arc cluster circle
                var circ = TryArcClusterCircle(arcs);
                if (circ != null)
                {
                    var (center, radiusFt) = circ.Value;
                    double diaMm = radiusFt * 2 * 304.8;
                    if (diaMm >= 5 && diaMm <= 2000)
                        return new DrainItem(center, diaMm, diaMm, "Circle");
                }

                // Try best-fit circle
                var fit = TryBestFitCircle(pts);
                if (fit != null)
                {
                    var (cpt, rFt, err) = fit.Value;
                    if (rFt > 0 && (err / rFt) < 0.08)
                    {
                        double diaMm = rFt * 2 * 304.8;
                        if (diaMm >= 5 && diaMm <= 2000)
                            return new DrainItem(cpt, diaMm, diaMm, "Circle");
                    }
                }

                // Fallback: bounding box
                double minX = pts.Min(p => p.X), maxX = pts.Max(p => p.X);
                double minY = pts.Min(p => p.Y), maxY = pts.Max(p => p.Y);
                double minZ = pts.Min(p => p.Z), maxZ = pts.Max(p => p.Z);

                double w = (maxX - minX) * 304.8;
                double h = (maxY - minY) * 304.8;
                if (w < 5 || h < 5 || w > 2000 || h > 2000) return null;

                var centerPt = new XYZ((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);

                string shape = (Math.Abs(w - h) < 5) ? "Square" :
                               curves.All(c => c is Line) ? "Rectangle" :
                               arcs.Count > 0 ? "Mixed Shape" : "Polygon";

                return new DrainItem(centerPt, w, h, shape);
            }
            catch { return null; }
        }

        private XYZ ProjectToFace(Face face, XYZ p)
        {
            try { return face.Project(p)?.XYZPoint ?? p; } catch { return p; }
        }

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

        private (XYZ center, double radiusFt, double residual)? TryBestFitCircle(List<XYZ> pts)
        {
            if (pts == null || pts.Count < 3) return null;

            var cx = pts.Average(p => p.X);
            var cy = pts.Average(p => p.Y);
            var cz = pts.Average(p => p.Z);
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
            const double tol = 0.01;
            foreach (var d in drains)
                if (!unique.Any(x => x.CenterPoint.DistanceTo(d.CenterPoint) < tol))
                    unique.Add(d);
            return unique;
        }
    }
}