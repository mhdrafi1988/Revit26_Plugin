using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.LinkedDetailLineGenerator.VA003.Core.Models;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA003.Core.Services
{
    /// <summary>Outcome of clipping one boundary loop against the processing boundary.</summary>
    public class ClipResult
    {
        /// <summary>One or more open or closed curve chains ready for Detail Line creation.</summary>
        public List<List<Curve>> Chains { get; set; } = new();

        /// <summary>True if the original loop was entirely outside the boundary (no output).</summary>
        public bool FullyOutside { get; set; }

        /// <summary>True if the original loop was entirely inside the boundary (untouched, single closed chain).</summary>
        public bool FullyInside { get; set; }

        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Stage 2 accurate clipping (spec Section 13/15) plus the trim/close-loop
    /// finishing logic discussed with Rafi:
    ///
    /// 1. TrimToBoundary=false -> loop passed through untouched (even if partially
    ///    outside - matches "no clipping" user intent; NOT spec-recommended but
    ///    Rafi's toggles give explicit control).
    /// 2. TrimToBoundary=true -> loop is intersected against the boundary polygon.
    ///    - Fully inside -> untouched closed loop.
    ///    - Fully outside -> dropped (FullyOutside=true, reported to caller).
    ///    - Partially inside -> one or more open chains, each ending exactly on the
    ///      boundary polygon edge.
    /// 3. For partial clips, if CloseOpenLoops=true: walk the boundary polygon between
    ///    each chain's open ends (shorter path) and stitch a closing segment along the
    ///    boundary itself - never a straight chord through the interior.
    ///    If CapOpenEnds=true instead: cap with a single straight Line directly between
    ///    the two open ends (fabricated chord, cheaper, less geometrically "honest").
    ///    If neither: chains are left open (dangling ends at the crop edge).
    ///
    /// Implementation note: clipping math here uses a polygon (2D X/Y, Z from the
    /// plan projection) with Sutherland-Hodgman-style edge clipping extended to
    /// preserve Arc/Ellipse curve segments where a curve doesn't cross the boundary
    /// (only Line segments are inserted for the new boundary-following edges -
    /// Arc/Ellipse curves are only ever kept whole or dropped whole in Phase 2;
    /// an Arc that itself crosses the boundary mid-arc is split into two Arcs using
    /// its own parameter at the intersection point, still analytically exact).
    /// </summary>
    public class GeometryClippingService
    {
        private const double Tolerance = 1e-6;

        public ClipResult ClipLoop(
            List<Curve> loop,
            List<XYZ> boundaryPolygon,
            bool trimToBoundary,
            LoopClosingSettings loopClosing,
            Action<string>? onWarning = null)
        {
            var result = new ClipResult();

            if (!trimToBoundary)
            {
                result.Chains.Add(loop);
                return result;
            }

            bool allInside = loop.All(c => PointInPolygon(c.GetEndPoint(0), boundaryPolygon))
                           && loop.All(c => PointInPolygon(c.GetEndPoint(1), boundaryPolygon));
            if (allInside && !AnyCurveCrossesBoundary(loop, boundaryPolygon))
            {
                result.FullyInside = true;
                result.Chains.Add(loop);
                return result;
            }

            bool allOutside = loop.All(c => !PointInPolygon(c.GetEndPoint(0), boundaryPolygon))
                            && loop.All(c => !PointInPolygon(c.GetEndPoint(1), boundaryPolygon))
                            && !AnyCurveCrossesBoundary(loop, boundaryPolygon);
            if (allOutside)
            {
                result.FullyOutside = true;
                return result;
            }

            List<Curve> insideSegments = ClipCurvesToPolygon(loop, boundaryPolygon, result.Warnings);

            if (insideSegments.Count == 0)
            {
                result.FullyOutside = true;
                return result;
            }

            List<List<Curve>> chains = ChainSegments(insideSegments);

            if (loopClosing.CloseOpenLoops && chains.Count > 0)
            {
                chains = CloseChainsAlongBoundary(chains, boundaryPolygon, result.Warnings);
            }
            else if (loopClosing.CapOpenEnds && chains.Count > 0)
            {
                chains = CapChainsWithStraightLine(chains);
            }

            result.Chains = chains;
            return result;
        }

        // -- Point/polygon tests --------------------------------------------

        private bool PointInPolygon(XYZ p, List<XYZ> polygon)
        {
            bool inside = false;
            int n = polygon.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                XYZ pi = polygon[i], pj = polygon[j];
                bool intersect = ((pi.Y > p.Y) != (pj.Y > p.Y)) &&
                    (p.X < (pj.X - pi.X) * (p.Y - pi.Y) / (pj.Y - pi.Y) + pi.X);
                if (intersect) inside = !inside;
            }
            return inside;
        }

        private bool AnyCurveCrossesBoundary(List<Curve> curves, List<XYZ> polygon)
        {
            foreach (var curve in curves)
            {
                var tessellated = curve.Tessellate();
                for (int i = 0; i < tessellated.Count - 1; i++)
                {
                    for (int j = 0; j < polygon.Count; j++)
                    {
                        XYZ b0 = polygon[j];
                        XYZ b1 = polygon[(j + 1) % polygon.Count];
                        if (SegmentsIntersect(tessellated[i], tessellated[i + 1], b0, b1))
                            return true;
                    }
                }
            }
            return false;
        }

        private bool SegmentsIntersect(XYZ p1, XYZ p2, XYZ p3, XYZ p4)
        {
            double d1 = Cross(p4 - p3, p1 - p3);
            double d2 = Cross(p4 - p3, p2 - p3);
            double d3 = Cross(p2 - p1, p3 - p1);
            double d4 = Cross(p2 - p1, p4 - p1);
            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
                   ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
        }

        private double Cross(XYZ a, XYZ b) => a.X * b.Y - a.Y * b.X;

        // -- Clipping ---------------------------------------------------------

        /// <summary>
        /// Splits each curve in the loop at its intersection(s) with the boundary
        /// polygon edges, keeping only the portions whose midpoint is inside the
        /// polygon. Line/Arc curves are split using their own parameterization,
        /// preserving exact curve type: an arc cut by the boundary is rebuilt as a
        /// shorter Arc using the new start/end and a recomputed midpoint on the same
        /// circle, not chopped into line segments.
        /// </summary>
        private List<Curve> ClipCurvesToPolygon(List<Curve> loop, List<XYZ> polygon, List<string> warnings)
        {
            var survivors = new List<Curve>();

            foreach (var curve in loop)
            {
                var splitParams = new List<double> { curve.GetEndParameter(0), curve.GetEndParameter(1) };

                foreach (var edge in PolygonEdges(polygon))
                {
                    var intersections = IntersectCurveWithSegment(curve, edge.Item1, edge.Item2);
                    splitParams.AddRange(intersections);
                }

                splitParams = splitParams.Distinct().OrderBy(p => p).ToList();

                for (int i = 0; i < splitParams.Count - 1; i++)
                {
                    double tA = splitParams[i];
                    double tB = splitParams[i + 1];
                    if (tB - tA < Tolerance) continue;

                    Curve? segment = TrySubCurve(curve, tA, tB, warnings);
                    if (segment == null) continue;

                    XYZ mid = segment.Evaluate(0.5, true);
                    if (PointInPolygon(mid, polygon))
                        survivors.Add(segment);
                }
            }

            return survivors;
        }

        private IEnumerable<Tuple<XYZ, XYZ>> PolygonEdges(List<XYZ> polygon)
        {
            for (int i = 0; i < polygon.Count; i++)
                yield return Tuple.Create(polygon[i], polygon[(i + 1) % polygon.Count]);
        }

        /// <summary>Returns the curve-parameter values (not points) where the curve
        /// crosses the given boundary segment, using Revit's own curve/curve
        /// intersection so Arc/Ellipse curves are split analytically, not by
        /// tessellated approximation.</summary>
        private List<double> IntersectCurveWithSegment(Curve curve, XYZ segStart, XYZ segEnd)
        {
            var results = new List<double>();
            try
            {
                Line boundaryLine = Line.CreateBound(segStart, segEnd);
                SetComparisonResult cmp = curve.Intersect(boundaryLine, out IntersectionResultArray? irArray);
                if (cmp == SetComparisonResult.Overlap && irArray != null)
                {
                    foreach (IntersectionResult ir in irArray)
                    {
                        results.Add(curve.Project(ir.XYZPoint).Parameter);
                    }
                }
            }
            catch
            {
                // Curve/curve intersection can throw for degenerate or non-overlapping
                // cases - treated as "no intersection" rather than propagating.
            }
            return results;
        }

        private Curve? TrySubCurve(Curve curve, double tA, double tB, List<string> warnings)
        {
            try
            {
                return SubCurve(curve, tA, tB);
            }
            catch (Exception ex)
            {
                warnings.Add("Failed to split curve at boundary: " + ex.Message);
                return null;
            }
        }

        private Curve SubCurve(Curve curve, double tA, double tB)
        {
            if (curve is Line)
            {
                return Line.CreateBound(curve.Evaluate(tA, false), curve.Evaluate(tB, false));
            }
            if (curve is Arc arc)
            {
                XYZ p0 = arc.Evaluate(tA, false);
                XYZ p1 = arc.Evaluate(tB, false);
                XYZ pm = arc.Evaluate((tA + tB) / 2.0, false);
                return Arc.Create(p0, p1, pm);
            }
            if (curve is Ellipse ellipse)
            {
                Curve? subCurve = Ellipse.CreateCurve(ellipse.Center, ellipse.RadiusX, ellipse.RadiusY,
                    ellipse.XDirection, ellipse.YDirection, tA, tB) as Curve;
                return subCurve ?? Line.CreateBound(curve.Evaluate(tA, false), curve.Evaluate(tB, false));
            }
            // HermiteSpline or other: fall back to straight chord for the
            // sub-segment rather than attempting a partial spline rebuild.
            return Line.CreateBound(curve.Evaluate(tA, false), curve.Evaluate(tB, false));
        }

        // -- Chaining survivors into contiguous open polylines ------------------

        private List<List<Curve>> ChainSegments(List<Curve> segments)
        {
            var remaining = new List<Curve>(segments);
            var chains = new List<List<Curve>>();

            while (remaining.Count > 0)
            {
                var chain = new List<Curve> { remaining[0] };
                remaining.RemoveAt(0);

                bool extended = true;
                while (extended)
                {
                    extended = false;
                    XYZ chainEnd = chain[chain.Count - 1].GetEndPoint(1);

                    for (int i = 0; i < remaining.Count; i++)
                    {
                        if (chainEnd.IsAlmostEqualTo(remaining[i].GetEndPoint(0), Tolerance))
                        {
                            chain.Add(remaining[i]);
                            remaining.RemoveAt(i);
                            extended = true;
                            break;
                        }
                        if (chainEnd.IsAlmostEqualTo(remaining[i].GetEndPoint(1), Tolerance))
                        {
                            chain.Add(ReverseCurve(remaining[i]));
                            remaining.RemoveAt(i);
                            extended = true;
                            break;
                        }
                    }
                }
                chains.Add(chain);
            }

            return chains;
        }

        private Curve ReverseCurve(Curve curve)
        {
            if (curve is Line)
                return Line.CreateBound(curve.GetEndPoint(1), curve.GetEndPoint(0));
            if (curve is Arc arc)
                return Arc.Create(curve.GetEndPoint(1), curve.GetEndPoint(0), arc.Evaluate(0.5, true));
            return Line.CreateBound(curve.GetEndPoint(1), curve.GetEndPoint(0));
        }

        // -- Close open loops along the boundary (walk shorter path) -----------

        /// <summary>
        /// For each open chain, connects its two open ends by walking along the
        /// boundary polygon perimeter (shorter direction) rather than drawing a
        /// straight chord through the interior. If multiple chains came from the
        /// same clipped loop, their ends are stitched sequentially in boundary-
        /// perimeter order.
        /// </summary>
        private List<List<Curve>> CloseChainsAlongBoundary(
            List<List<Curve>> chains, List<XYZ> boundary, List<string> warnings)
        {
            if (chains.Count == 0) return chains;

            var perimeterVerts = boundary;
            double totalPerimeter = 0;
            var edgeLengths = new List<double>();
            for (int i = 0; i < perimeterVerts.Count; i++)
            {
                double len = perimeterVerts[i].DistanceTo(perimeterVerts[(i + 1) % perimeterVerts.Count]);
                edgeLengths.Add(len);
                totalPerimeter += len;
            }

            Func<XYZ, double> paramOf = (pt) =>
            {
                double best = double.MaxValue;
                double bestParam = 0;
                double cum = 0;
                for (int i = 0; i < perimeterVerts.Count; i++)
                {
                    XYZ a = perimeterVerts[i];
                    XYZ b = perimeterVerts[(i + 1) % perimeterVerts.Count];
                    double segLen = edgeLengths[i];
                    XYZ dir = segLen > Tolerance ? (b - a).Normalize() : XYZ.Zero;
                    double t = (pt - a).DotProduct(dir);
                    t = Math.Max(0, Math.Min(segLen, t));
                    XYZ proj = a + dir * t;
                    double dist = proj.DistanceTo(pt);
                    if (dist < best)
                    {
                        best = dist;
                        bestParam = cum + t;
                    }
                    cum += segLen;
                }
                return bestParam;
            };

            if (chains.Count == 1)
            {
                var chain = chains[0];
                XYZ startPt = chain[0].GetEndPoint(0);
                XYZ endPt = chain[chain.Count - 1].GetEndPoint(1);

                if (!startPt.IsAlmostEqualTo(endPt, Tolerance))
                {
                    var closingSegments = BuildBoundaryWalk(endPt, startPt, perimeterVerts, edgeLengths, totalPerimeter, paramOf);
                    chain.AddRange(closingSegments);
                }
                return chains;
            }

            var ordered = chains.OrderBy(c => paramOf(c[0].GetEndPoint(0))).ToList();

            var stitched = new List<Curve>();
            for (int i = 0; i < ordered.Count; i++)
            {
                stitched.AddRange(ordered[i]);
                XYZ from = ordered[i][ordered[i].Count - 1].GetEndPoint(1);
                XYZ to = ordered[(i + 1) % ordered.Count][0].GetEndPoint(0);

                if (!from.IsAlmostEqualTo(to, Tolerance))
                {
                    var walk = BuildBoundaryWalk(from, to, perimeterVerts, edgeLengths, totalPerimeter, paramOf);
                    stitched.AddRange(walk);
                }
            }

            return new List<List<Curve>> { stitched };
        }

        private List<Curve> BuildBoundaryWalk(
            XYZ from, XYZ to, List<XYZ> perimeterVerts, List<double> edgeLengths,
            double totalPerimeter, Func<XYZ, double> paramOf)
        {
            double pFrom = paramOf(from);
            double pTo = paramOf(to);

            double forwardDist = ((pTo - pFrom) % totalPerimeter + totalPerimeter) % totalPerimeter;
            double backwardDist = totalPerimeter - forwardDist;

            bool goForward = forwardDist <= backwardDist;

            var waypoints = new List<XYZ> { from };
            double cum = 0;
            for (int i = 0; i < perimeterVerts.Count; i++)
            {
                double segLen = edgeLengths[i];
                double vertParam = cum + segLen;
                bool between = goForward
                    ? IsParamBetweenForward(pFrom, pTo, vertParam, totalPerimeter)
                    : IsParamBetweenForward(pTo, pFrom, vertParam, totalPerimeter);
                if (between)
                    waypoints.Add(perimeterVerts[(i + 1) % perimeterVerts.Count]);
                cum += segLen;
            }
            if (!goForward) waypoints.Reverse();
            waypoints.Add(to);

            var segments = new List<Curve>();
            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                if (waypoints[i].DistanceTo(waypoints[i + 1]) > Tolerance)
                    segments.Add(Line.CreateBound(waypoints[i], waypoints[i + 1]));
            }
            return segments;
        }

        private bool IsParamBetweenForward(double pStart, double pEnd, double p, double total)
        {
            double span = ((pEnd - pStart) % total + total) % total;
            double offset = ((p - pStart) % total + total) % total;
            return offset > Tolerance && offset < span - Tolerance;
        }

        // -- Cap open ends with a single straight fabricated segment -----------

        private List<List<Curve>> CapChainsWithStraightLine(List<List<Curve>> chains)
        {
            foreach (var chain in chains)
            {
                XYZ startPt = chain[0].GetEndPoint(0);
                XYZ endPt = chain[chain.Count - 1].GetEndPoint(1);
                if (!startPt.IsAlmostEqualTo(endPt, Tolerance))
                    chain.Add(Line.CreateBound(endPt, startPt));
            }
            return chains;
        }

        // -- Linear-group clipping (Phase 3): single open curve, no loop-closing --

        /// <summary>
        /// Clips a single Linear-group curve (Wall/Beam centerline) against the
        /// processing boundary. Unlike ClipLoop, there is no "closed loop" concept
        /// for a Linear element — CloseOpenLoops/CapOpenEnds from ProcessingScope do
        /// NOT apply here (a wall centerline crossing the view boundary should stay
        /// a simple trimmed segment, never get an artificial closing segment). If
        /// the curve crosses the boundary multiple times, each inside portion becomes
        /// its own separate Detail Line segment.
        /// </summary>
        public LinearClipResult ClipLinearCurve(Curve curve, List<XYZ> boundaryPolygon, bool trimToBoundary)
        {
            var result = new LinearClipResult();

            if (!trimToBoundary)
            {
                result.Segments.Add(curve);
                return result;
            }

            bool startInside = PointInPolygon(curve.GetEndPoint(0), boundaryPolygon);
            bool endInside = PointInPolygon(curve.GetEndPoint(1), boundaryPolygon);
            bool crosses = AnyCurveCrossesBoundary(new List<Curve> { curve }, boundaryPolygon);

            if (startInside && endInside && !crosses)
            {
                result.FullyInside = true;
                result.Segments.Add(curve);
                return result;
            }

            if (!startInside && !endInside && !crosses)
            {
                result.FullyOutside = true;
                return result;
            }

            var warnings = new List<string>();
            List<Curve> survivors = ClipCurvesToPolygon(new List<Curve> { curve }, boundaryPolygon, warnings);
            result.Warnings.AddRange(warnings);

            if (survivors.Count == 0)
            {
                result.FullyOutside = true;
                return result;
            }

            // Chain contiguous survivors (handles the case where the curve dips in
            // and out of the boundary more than once — each contiguous inside run
            // becomes one segment, not merged across gaps).
            result.Segments = ChainSegments(survivors).SelectMany(chain => chain).ToList();
            return result;
        }
    }

    /// <summary>Outcome of clipping a single Linear-group curve.</summary>
    public class LinearClipResult
    {
        public List<Curve> Segments { get; set; } = new();
        public bool FullyOutside { get; set; }
        public bool FullyInside { get; set; }
        public List<string> Warnings { get; set; } = new();
    }
}

