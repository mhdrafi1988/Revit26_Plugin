using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA003.Core.Services
{
    /// <summary>
    /// Projects host-coordinate 3D curves onto the active plan view's sketch plane
    /// (flattens Z to the view's level elevation) so Detail Curves — which are
    /// inherently 2D, view-specific elements — can be created from them.
    ///
    /// Only Z is flattened; X/Y are untouched. Because Line/Arc/Ellipse curves stay
    /// planar under this flattening (dropping Z from a curve already close to
    /// horizontal does not change its curve type), CreateTransformed with a
    /// non-uniform flatten isn't used — instead each curve is rebuilt from its own
    /// analytic parameters with Z fixed, preserving exactness.
    /// </summary>
    public class GeometryProjectionService
    {
        public List<Curve> ProjectToPlan(IEnumerable<Curve> curves, double planZ)
        {
            var result = new List<Curve>();
            foreach (var curve in curves)
                result.Add(ProjectCurve(curve, planZ));
            return result;
        }

        public List<List<Curve>> ProjectLoop(IEnumerable<List<Curve>> loops, double planZ)
        {
            return loops.Select(loop => ProjectToPlan(loop, planZ)).ToList();
        }

        private Curve ProjectCurve(Curve curve, double planZ)
        {
            switch (curve)
            {
                case Line line:
                {
                    XYZ p0 = Flatten(line.GetEndPoint(0), planZ);
                    XYZ p1 = Flatten(line.GetEndPoint(1), planZ);
                    return Line.CreateBound(p0, p1);
                }
                case Arc arc:
                {
                    XYZ p0 = Flatten(arc.GetEndPoint(0), planZ);
                    XYZ p1 = Flatten(arc.GetEndPoint(1), planZ);
                    XYZ mid = Flatten(arc.Evaluate(0.5, true), planZ);
                    // Rebuild via 3-point constructor — re-derives center/radius exactly
                    // in the flattened plane, rather than reusing the original (possibly
                    // tilted) center/radius which would be wrong once Z changes.
                    return Arc.Create(p0, p1, mid);
                }
                case Ellipse ellipse:
                {
                    XYZ center = Flatten(ellipse.Center, planZ);
                    // Ellipse axes (XDirection/YDirection) are directions, not points —
                    // safe to reuse unchanged as long as the ellipse was already
                    // near-planar-horizontal (true for Profile-group Floor/Roof faces,
                    // which is the only place Ellipse edges occur in this pipeline).
                    return Ellipse.CreateCurve(center, ellipse.RadiusX, ellipse.RadiusY,
                        ellipse.XDirection, ellipse.YDirection, ellipse.GetEndParameter(0), ellipse.GetEndParameter(1)) as Curve
                        ?? Line.CreateBound(Flatten(ellipse.GetEndPoint(0), planZ), Flatten(ellipse.GetEndPoint(1), planZ));
                }
                case HermiteSpline:
                {
                    // Flatten each control/tessellation point and rebuild.
                    var pts = curve.Tessellate().Select(p => Flatten(p, planZ)).ToList();
                    return HermiteSpline.Create(pts, false);
                }
                default:
                {
                    // Unknown curve type reaching this stage should not happen given
                    // GeometryExtractionService already normalizes everything to
                    // Line/Arc/Ellipse/HermiteSpline — defensive fallback to chord.
                    XYZ p0 = Flatten(curve.GetEndPoint(0), planZ);
                    XYZ p1 = Flatten(curve.GetEndPoint(1), planZ);
                    return Line.CreateBound(p0, p1);
                }
            }
        }

        private static XYZ Flatten(XYZ p, double z) => new(p.X, p.Y, z);
    }
}
