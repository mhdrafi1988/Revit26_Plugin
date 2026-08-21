using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA003.Core.Services
{
    /// <summary>
    /// Transforms extracted curves from linked-document coordinates to host-document
    /// coordinates using the RevitLinkInstance's placement Transform.
    ///
    /// Curve-type-aware: Revit's Curve.CreateTransformed() correctly preserves the
    /// analytic type (Line stays Line, Arc stays Arc, Ellipse stays Ellipse,
    /// HermiteSpline stays HermiteSpline) — so this is a straight pass-through of
    /// CreateTransformed rather than manual point-by-point reconstruction. This is
    /// what keeps the "3 points for an arc" exactness intact through the transform
    /// step: Revit itself re-derives the transformed Arc's center/radius/angles
    /// analytically, not by moving sampled points.
    /// </summary>
    public class GeometryTransformService
    {
        public List<Curve> TransformCurves(IEnumerable<Curve> curves, Transform linkToHostTransform)
        {
            var result = new List<Curve>();
            foreach (var curve in curves)
            {
                Curve transformed = curve.CreateTransformed(linkToHostTransform);
                result.Add(transformed);
            }
            return result;
        }

        public List<List<Curve>> TransformLoop(IEnumerable<List<Curve>> loops, Transform linkToHostTransform)
        {
            return loops.Select(loop => TransformCurves(loop, linkToHostTransform)).ToList();
        }
    }
}
