using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V53.Services
{
    /// <summary>
    /// Extracts all interior void/opening loops from a RoofBase element.
    /// Uses the same two-pass strategy as RoofBoundaryService:
    ///   Pass 1 — Sketch dependent element (Revit 2025+)
    ///   Pass 2 — Geometry solid bottom-face edge loops (fallback)
    /// The largest loop is the outer boundary (returned by RoofBoundaryService).
    /// Every other closed loop is an interior opening and is returned here.
    /// All vertices are flattened to Z=0 and tessellated.
    /// No transaction required — read-only API.
    /// </summary>
    public class InnerLoopService
    {
        public List<List<XYZ>> ExtractInnerLoops(RoofBase roof)
        {
            if (roof == null) throw new ArgumentNullException(nameof(roof));

            var result = TryExtractFromSketch(roof);
            if (result != null && result.Count > 0)
                return result;

            return ExtractFromGeometry(roof);
        }

        // ── Pass 1: Sketch element ────────────────────────────────────────────────

        private List<List<XYZ>> TryExtractFromSketch(RoofBase roof)
        {
            try
            {
                var doc = roof.Document;
                var dependentIds = roof.GetDependentElements(new ElementClassFilter(typeof(Sketch)));

                foreach (ElementId id in dependentIds)
                {
                    if (!(doc.GetElement(id) is Sketch sketch)) continue;

                    // Collect all loops with their areas
                    var loops = new List<(CurveArray loop, double area)>();
                    foreach (CurveArray loop in sketch.Profile)
                    {
                        double area = ApproximateLoopArea(loop);
                        loops.Add((loop, area));
                    }

                    if (loops.Count <= 1) return new List<List<XYZ>>(); // no inner loops

                    // Largest loop = outer boundary; everything else = inner
                    double maxArea = loops.Max(l => l.area);
                    return loops
                        .Where(l => l.area < maxArea)
                        .Select(l => TessellateLoop(l.loop))
                        .Where(pts => pts.Count >= 3)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[InnerLoopService] Sketch pass skipped: {ex.Message}");
            }
            return null;
        }

        // ── Pass 2: Geometry solid ────────────────────────────────────────────────

        private List<List<XYZ>> ExtractFromGeometry(RoofBase roof)
        {
            var opts = new Options
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = false,
                DetailLevel = ViewDetailLevel.Fine
            };

            // Collect all edge loops from horizontal-ish faces grouped by area
            var allLoops = new List<(List<XYZ> pts, double area)>();

            foreach (GeometryObject obj in roof.get_Geometry(opts))
            {
                if (!(obj is Solid solid) || solid.Volume < 1e-6) continue;

                foreach (Face face in solid.Faces)
                {
                    XYZ normal = face.ComputeNormal(new UV(0.5, 0.5));
                    if (Math.Abs(normal.Z) < 0.3) continue; // skip vertical faces

                    foreach (EdgeArray loop in face.EdgeLoops)
                    {
                        var pts = TessellateEdgeLoop(loop);
                        double area = ApproximatePolygonArea(pts);
                        allLoops.Add((pts, area));
                    }
                }
            }

            if (allLoops.Count <= 1) return new List<List<XYZ>>();

            double maxArea = allLoops.Max(l => l.area);
            return allLoops
                .Where(l => l.area < maxArea && l.pts.Count >= 3)
                .Select(l => l.pts)
                .ToList();
        }

        // ── Tessellation ──────────────────────────────────────────────────────────

        private List<XYZ> TessellateLoop(CurveArray loop)
        {
            var pts = new List<XYZ>();
            foreach (Curve c in loop) AppendCurve(c, pts);
            return Flatten(pts);
        }

        private List<XYZ> TessellateEdgeLoop(EdgeArray loop)
        {
            var pts = new List<XYZ>();
            foreach (Edge e in loop) AppendCurve(e.AsCurve(), pts);
            return Flatten(pts);
        }

        private static void AppendCurve(Curve c, List<XYZ> pts)
        {
            if (c is Line)
                pts.Add(c.GetEndPoint(0));
            else
            {
                IList<XYZ> tess = c.Tessellate();
                for (int i = 0; i < tess.Count - 1; i++) pts.Add(tess[i]);
            }
        }

        private static List<XYZ> Flatten(List<XYZ> pts)
        {
            const double snapTol = 1e-6;
            var flat = new List<XYZ>(pts.Count);
            XYZ prev = null;
            foreach (var p in pts)
            {
                var fp = new XYZ(p.X, p.Y, 0);
                if (prev != null && fp.DistanceTo(prev) < snapTol) continue;
                flat.Add(fp);
                prev = fp;
            }
            return flat;
        }

        // ── Area helpers ──────────────────────────────────────────────────────────

        private static double ApproximateLoopArea(CurveArray loop)
        {
            var pts = new List<XYZ>();
            foreach (Curve c in loop) pts.Add(c.GetEndPoint(0));
            return ApproximatePolygonArea(Flatten(pts));
        }

        public static double ApproximatePolygonArea(List<XYZ> pts)
        {
            int n = pts.Count;
            if (n < 3) return 0;
            double area = 0;
            for (int i = 0; i < n; i++)
            {
                var a = pts[i]; var b = pts[(i + 1) % n];
                area += a.X * b.Y - b.X * a.Y;
            }
            return Math.Abs(area) / 2.0;
        }
    }
}