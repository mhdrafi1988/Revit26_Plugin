using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.LinkedDetailLineGenerator.VA007.Core.Models;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA007.Core.Services
{
    /// <summary>
    /// Computes ElementMetrics for a representative linked-document instance.
    /// Bounding-box Width/Height apply to every RepresentationGroup; Perimeter/
    /// Area only make sense for Profile group and are left null otherwise.
    /// </summary>
    public class ElementMetricsService
    {
        private readonly GeometryExtractionService _extraction = new();

        public ElementMetrics ComputeMetrics(Element representativeInstance, RepresentationGroup group)
        {
            var metrics = new ElementMetrics();

            BoundingBoxXYZ? bbox = representativeInstance.get_BoundingBox(null);
            if (bbox != null)
            {
                // Local (untransformed) extents are used directly — Width/Height are
                // a filtering aid, not a design dimension, and a link's placement
                // transform is always rigid (no scaling), so local extents already
                // match what the user would measure in the host view.
                metrics.WidthFeet = bbox.Max.X - bbox.Min.X;
                metrics.HeightFeet = bbox.Max.Y - bbox.Min.Y;
            }

            if (group == RepresentationGroup.Profile)
            {
                var complexCurveSettings = new ComplexCurveSettings();
                ExtractedProfile? profile = _extraction.ExtractProfile(representativeInstance, complexCurveSettings);
                if (profile != null && profile.OuterLoops.Count > 0)
                {
                    List<Curve> outer = profile.OuterLoops[0];
                    metrics.PerimeterFeet = outer.Sum(c => c.Length);
                    metrics.AreaSqFt = ComputeArea(outer);
                }
            }

            return metrics;
        }

        /// <summary>Shoelace formula on each curve's tessellated points — an
        /// approximation for Arc/Ellipse edges, exact for straight-edged loops.
        /// Good enough for a filter threshold; not used for output geometry.</summary>
        private double ComputeArea(List<Curve> loop)
        {
            var pts = new List<XYZ>();
            foreach (var c in loop)
            {
                var tess = c.Tessellate();
                for (int i = 0; i < tess.Count - 1; i++)
                    pts.Add(tess[i]);
            }
            if (pts.Count < 3) return 0;

            double area = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                XYZ p1 = pts[i];
                XYZ p2 = pts[(i + 1) % pts.Count];
                area += (p1.X * p2.Y - p2.X * p1.Y);
            }
            return System.Math.Abs(area) / 2.0;
        }
    }
}
