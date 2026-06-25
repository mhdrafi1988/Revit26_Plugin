using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V64.Services
{
    /// <summary>
    /// Extracts the 2D plan boundary polygon from a RoofBase element.
    /// Revit 2026: GetSketch() does not exist on RoofBase.
    /// Strategy: parse the roof geometry solid, project bottom face edges to Z=0.
    /// Arcs and curves are tessellated so downstream clipping treats the result as a polygon.
    /// No transaction required — read-only API.
    /// </summary>
    public class RoofBoundaryService
    {
        /// <summary>
        /// Extracts the outer boundary polygon of <paramref name="roof"/> (Z=0, tessellated).
        /// Tries the roof's sketch profile first, falling back to the solid geometry's
        /// bottom face if no sketch is found.
        /// </summary>
        public List<XYZ> ExtractBoundary(RoofBase roof)
        {
            if (roof == null) throw new ArgumentNullException(nameof(roof));

            // ── Try sketch via SketchBase filter (Revit 2026 approach) ─────────────
            var sketchResult = TryExtractFromSketchElement(roof);
            if (sketchResult != null && sketchResult.Count >= 3)
                return sketchResult;

            // ── Fallback: geometry solid bottom face ──────────────────────────────
            return ExtractFromGeometry(roof);
        }

        // ── Sketch via document filter ────────────────────────────────────────────

        private List<XYZ> TryExtractFromSketchElement(RoofBase roof)
        {
            try
            {
                // In Revit 2025+, the sketch is a separate Sketch element owned by the roof.
                // We can find it via the roof's dependent elements.
                var doc = roof.Document;
                var dependentIds = roof.GetDependentElements(new ElementClassFilter(typeof(Sketch)));

                foreach (ElementId id in dependentIds)
                {
                    if (doc.GetElement(id) is Sketch sketch)
                    {
                        var loop = GetLargestLoop(sketch.Profile);
                        if (loop != null) return RoofGeometry2D.TessellateLoop(loop);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[RoofBoundaryService] Sketch extraction skipped: {ex.Message}");
            }
            return null;
        }

        private static CurveArray GetLargestLoop(CurveArrArray profile)
        {
            CurveArray best = null;
            double maxArea = -1;
            foreach (CurveArray loop in profile)
            {
                double area = RoofGeometry2D.ApproximateLoopArea(loop);
                if (area > maxArea) { maxArea = area; best = loop; }
            }
            return best;
        }

        // ── Geometry fallback ─────────────────────────────────────────────────────

        private List<XYZ> ExtractFromGeometry(RoofBase roof)
        {
            var opts = new Options
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = false,
                DetailLevel = ViewDetailLevel.Fine
            };

            List<XYZ> bestLoop = null;
            double maxArea = -1;

            foreach (GeometryObject obj in roof.get_Geometry(opts))
            {
                if (!(obj is Solid solid) || solid.Volume < 1e-6) continue;

                foreach (Face face in solid.Faces)
                {
                    // Use the largest horizontal-ish face (roof footprint)
                    XYZ normal = face.ComputeNormal(new UV(0.5, 0.5));
                    if (Math.Abs(normal.Z) < 0.3) continue; // skip vertical faces

                    foreach (EdgeArray loop in face.EdgeLoops)
                    {
                        var pts = RoofGeometry2D.TessellateEdgeLoop(loop);
                        var area = RoofGeometry2D.ApproximatePolygonArea(pts);
                        if (area > maxArea) { maxArea = area; bestLoop = pts; }
                    }
                }
            }

            if (bestLoop == null || bestLoop.Count < 3)
                throw new InvalidOperationException(
                    "Could not extract roof boundary. Ensure the roof has a valid 3D solid geometry.");

            return bestLoop;
        }
    }
}
