using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA003.Core.Services
{
    /// <summary>
    /// Creates host-document Detail Curves from processed (transformed, projected,
    /// clipped) curves, one call per curve in a chain -- Revit's NewDetailCurve does
    /// not accept multi-segment chains directly, so a "closed loop" or "open chain"
    /// in this tool's model is represented as N separate DetailCurve elements sharing
    /// the same MappingId in their ExtensibleStorage metadata (grouping happens via
    /// metadata, not via a single multi-segment Revit element).
    /// </summary>
    public class DetailLineCreationService
    {
        /// <summary>Creates one DetailCurve per curve in the list. Must be called
        /// inside an active Transaction. Curves with near-zero length are skipped
        /// and reported rather than passed to Revit (which would throw).</summary>
        public List<DetailCurve> CreateDetailCurves(
            Document hostDoc,
            View activeView,
            IEnumerable<Curve> curves,
            Action<string>? onWarning = null)
        {
            var created = new List<DetailCurve>();

            foreach (var curve in curves)
            {
                if (curve.Length < hostDoc.Application.ShortCurveTolerance)
                {
                    onWarning?.Invoke($"Skipped near-zero-length curve segment (length {curve.Length:F6}).");
                    continue;
                }

                try
                {
                    DetailCurve dc = hostDoc.Create.NewDetailCurve(activeView, curve);
                    created.Add(dc);
                }
                catch (Exception ex)
                {
                    onWarning?.Invoke($"Failed to create Detail Curve: {ex.Message}");
                }
            }

            return created;
        }

        /// <summary>Applies the given Detail Line Style (GraphicsStyle) to a set of
        /// already-created DetailCurve elements.</summary>
        public void ApplyLineStyle(IEnumerable<DetailCurve> detailCurves, GraphicsStyle? style, Action<string>? onWarning = null)
        {
            if (style == null) return;

            foreach (var dc in detailCurves)
            {
                try
                {
                    dc.LineStyle = style;
                }
                catch (Exception ex)
                {
                    onWarning?.Invoke($"Failed to apply line style '{style.Name}': {ex.Message}");
                }
            }
        }
    }
}
