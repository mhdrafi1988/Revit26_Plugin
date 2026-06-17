using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V60.Services
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
        /// <summary>
        /// Returns every interior void/opening loop on <paramref name="roof"/>
        /// (Z=0, tessellated), excluding the outer boundary loop itself.
        /// </summary>
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
                        double area = RoofGeometry2D.ApproximateLoopArea(loop);
                        loops.Add((loop, area));
                    }

                    if (loops.Count <= 1) return new List<List<XYZ>>(); // no inner loops

                    // Largest loop = outer boundary; everything else = inner
                    double maxArea = loops.Max(l => l.area);
                    return loops
                        .Where(l => l.area < maxArea)
                        .Select(l => RoofGeometry2D.TessellateLoop(l.loop))
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
                        var pts = RoofGeometry2D.TessellateEdgeLoop(loop);
                        double area = RoofGeometry2D.ApproximatePolygonArea(pts);
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
    }
}
