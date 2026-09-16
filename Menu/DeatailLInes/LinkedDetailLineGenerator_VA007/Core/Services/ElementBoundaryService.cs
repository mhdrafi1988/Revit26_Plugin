using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.LinkedDetailLineGenerator.VA007.Core.Models;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA007.Core.Services
{
    /// <summary>
    /// VA007's replacement boundary source for "Restrict to boundary" mode: instead
    /// of ViewBoundaryService's view-crop rectangle, the processing boundary is the
    /// pre-selected Floor/Roof's own outer edge — same (List&lt;XYZ&gt; boundary, bool
    /// isExactShape) shape ViewBoundaryService returns, so GeometryClippingService,
    /// SpatialFilterService and the processing engines need no changes at all to
    /// accept it.
    /// </summary>
    public class ElementBoundaryService
    {
        private readonly GeometryExtractionService _extraction = new();

        /// <summary>
        /// Extracts the selected element's outer boundary loop (via
        /// GeometryExtractionService.ExtractProfile, the same top-face logic used
        /// for Profile-group processing) and tessellates every curve into points so
        /// arcs/ellipses contribute a dense polygon approximation — the downstream
        /// clipping code only understands straight polygon edges for the BOUNDARY
        /// itself (it still preserves exact curve types for the geometry being
        /// clipped against it).
        /// </summary>
        public (List<XYZ> boundary, bool isExactShape) GetProcessingBoundary(
            Element selectedElement,
            ComplexCurveSettings complexCurveSettings,
            Action<string>? onLog = null)
        {
            ExtractedProfile? profile = _extraction.ExtractProfile(
                selectedElement,
                complexCurveSettings,
                (msg, id) => onLog?.Invoke($"Boundary extraction: {msg}"));

            if (profile == null || profile.OuterLoops.Count == 0)
            {
                onLog?.Invoke("Could not extract a boundary from the selected element — falling back to its bounding box.");
                return (GetBoundingBoxFallback(selectedElement), false);
            }

            List<Curve> outerLoop = profile.OuterLoops[0];
            var points = new List<XYZ>();
            foreach (Curve curve in outerLoop)
            {
                IList<XYZ> tess = curve.Tessellate();
                // Skip the last point of each curve except the final curve — it's
                // the same point as the next curve's first point (shared vertex).
                for (int i = 0; i < tess.Count - 1; i++)
                    points.Add(tess[i]);
            }
            if (points.Count == 0)
            {
                onLog?.Invoke("Extracted boundary had no usable points — falling back to bounding box.");
                return (GetBoundingBoxFallback(selectedElement), false);
            }

            onLog?.Invoke($"Boundary resolved from selected element {selectedElement.Id.Value} — {points.Count} vertex/vertices (outer edge, exact curve types preserved).");
            return (points, true);
        }

        private List<XYZ> GetBoundingBoxFallback(Element element)
        {
            BoundingBoxXYZ? bbox = element.get_BoundingBox(null);
            if (bbox == null)
            {
                // Degenerate fallback so callers never null-check — an empty/zero
                // boundary simply clips everything out, which is a safe failure mode.
                return new List<XYZ> { XYZ.Zero, XYZ.Zero, XYZ.Zero, XYZ.Zero };
            }

            Transform t = bbox.Transform;
            XYZ min = bbox.Min;
            XYZ max = bbox.Max;
            return new List<XYZ>
            {
                t.OfPoint(new XYZ(min.X, min.Y, 0)),
                t.OfPoint(new XYZ(max.X, min.Y, 0)),
                t.OfPoint(new XYZ(max.X, max.Y, 0)),
                t.OfPoint(new XYZ(min.X, max.Y, 0)),
            };
        }
    }
}
