using System;
using Autodesk.Revit.DB;
using Revit26_Plugin.LinkedDetailLineGenerator.VA003.Core.Models;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA003.Core.Services
{
    /// <summary>
    /// Extracts the centerline curve from Linear-group elements (Walls, Beams / other
    /// LocationCurve-based elements), per spec Section 18. V1 prefers the element's
    /// LocationCurve directly over full 3D boundary extraction — a single curve per
    /// element, not a loop, so this is deliberately simpler than
    /// GeometryExtractionService (Profile group).
    ///
    /// Same exact-vs-tessellate curve handling as the Profile pipeline: a wall's
    /// LocationCurve is virtually always a Line or Arc already (Revit walls don't
    /// support spline location curves in practice), but the same ComplexCurveSettings
    /// fallback path is applied defensively in case a Linear-group element with an
    /// unusual LocationCurve type is ever encountered.
    /// </summary>
    public class LinearGeometryExtractionService
    {
        private readonly GeometryExtractionService _curveNormalizer = new();

        /// <summary>Returns the element's LocationCurve, normalized through the same
        /// exact-reconstruction-or-tessellate logic used for Profile edges. Returns
        /// null (with a warning) if the element has no LocationCurve — spec Section 18
        /// says "prefer LocationCurve where appropriate"; elements without one are
        /// skipped and reported, not silently dropped.</summary>
        public Curve? ExtractCenterline(
            Element element,
            ComplexCurveSettings complexCurveSettings,
            Action<string, ElementId?>? onWarning = null)
        {
            if (element.Location is not LocationCurve locCurve)
            {
                onWarning?.Invoke("Element has no LocationCurve — no reliable Linear representation in V1, skipped.", element.Id);
                return null;
            }

            Curve rawCurve = locCurve.Curve;
            if (rawCurve == null)
            {
                onWarning?.Invoke("LocationCurve.Curve is null — skipped.", element.Id);
                return null;
            }

            if (rawCurve.Length < 1e-6)
            {
                onWarning?.Invoke("Curve too short — skipped.", element.Id);
                return null;
            }

            // Reuse the same exact/tessellate decision as Profile edges (public method
            // on GeometryExtractionService would be cleaner than duplicating logic —
            // exposed as internal helper below).
            return _curveNormalizer.NormalizeSingleCurve(rawCurve, complexCurveSettings, element.Id, onWarning);
        }
    }
}
