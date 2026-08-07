using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoofFromDetailLines.V007
{
    /// <summary>
    /// Turns a flat list of selected DetailLine/DetailArc curves into a set of RoofGroups
    /// (outer boundary + opening loops), ready for RoofCreationService.
    ///
    /// Pipeline (per confirmed spec):
    ///  1. Group curves by endpoint connectivity (shared endpoints within tolerance).
    ///  2. Per group:
    ///     a. Already closed  -> Ready loop.
    ///     b. Overlapping/duplicate-path segments -> resolved via the SAME extend/trim-to-
    ///        junction mechanism as corner clean-up (not "keep longest, discard smaller").
    ///     c. Near-corner gaps within CornerToleranceMm -> extend both lines to meet.
    ///     d. After (b)/(c), if still open with exactly 2 free endpoints -> auto-close with
    ///        a straight connector line.
    ///     e. Anything else (multiple disconnected fragments, >2 free endpoints, isolated
    ///        lines) -> Skipped, reason logged.
    ///  3. Containment/nesting: sort valid loops by area descending; pair outermost with the
    ///     next loop nested directly inside it as (outer, opening) -> one RoofGroup; any
    ///     loop nested inside an opening starts a fresh outer boundary of its own, recursively.
    /// </summary>
    public static class GeometryLoopService
    {
        // Endpoint coincidence tolerance for connectivity grouping — short of the user's
        // corner tolerance, so genuinely touching endpoints are always grouped regardless
        // of the corner clean-up setting. 1/16" ~= 1.5mm in feet.
        private const double ConnectivityToleranceFeet = 1.0 / 16.0 / 12.0;

        public class LoopProcessingResult
        {
            public List<LoopCandidate> ValidLoops { get; set; } = new();
            public List<SkippedGroup> SkippedGroups { get; set; } = new();
        }

        public static LoopProcessingResult BuildLoops(
            IList<Element> detailElements,
            double cornerToleranceFeet,
            Action<LogLevel, string> log)
        {
            var result = new LoopProcessingResult();

            // ── Extract raw curves, keyed to their originating element ─────────────
            // A Circle sketched with Revit's Circle tool can come through as an
            // UNBOUND Arc (IsBound == false, GetEndPoint() undefined) rather than a
            // bound arc with coincident start/end points. Unbound curves were
            // previously discarded silently here, which is why solo circles vanished
            // with no log trace. They are now converted into two bound half-circle
            // arcs (the standard, documented workaround for Revit's unbound-arc
            // limitation), which the rest of the pipeline already handles as an
            // ordinary 2-curve closed loop.
            var curveMap = new List<(ElementId Id, Curve Curve)>();
            int unboundCircleCount = 0;
            foreach (var el in detailElements)
            {
                if (!(el is CurveElement ce) || ce.GeometryCurve == null) continue;

                var curve = ce.GeometryCurve;

                if (curve.IsBound)
                {
                    curveMap.Add((el.Id, curve));
                }
                else if (curve is Arc unboundArc)
                {
                    unboundCircleCount++;
                    foreach (var half in SplitUnboundCircleIntoBoundHalves(unboundArc))
                        curveMap.Add((el.Id, half));
                }
                // Other unbound curve types (unbound lines) are not expected from
                // detail sketch tools and are intentionally not added.
            }

            if (unboundCircleCount > 0)
                log(LogLevel.Info, $"GeometryLoopService: {unboundCircleCount} unbound circle(s) detected — split into bound half-arcs for processing.");
            log(LogLevel.Info, $"GeometryLoopService: {curveMap.Count} usable curve segment(s) extracted from {detailElements.Count} selected element(s).");

            // ── Step 1: connectivity grouping ───────────────────────────────────────
            var groups = GroupByConnectivity(curveMap);
            log(LogLevel.Info, $"Connectivity grouping: {groups.Count} group(s) found.");

            int serial = 1;
            foreach (var group in groups)
            {
                ProcessGroup(group, serial, cornerToleranceFeet, result, log);
                serial++;
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Unbound circle handling
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>Splits a full, unbound circular Arc (IsBound == false) into two
        /// bound half-circle arcs sharing two endpoints — Revit's own documented
        /// workaround for the API's lack of support for a single bound 360° arc.
        /// The two returned arcs form a valid closed loop when their endpoints are
        /// matched, exactly like any other 2-curve loop the pipeline already handles.</summary>
        private static IEnumerable<Arc> SplitUnboundCircleIntoBoundHalves(Arc circle)
        {
            XYZ center = circle.Center;
            double radius = circle.Radius;
            XYZ xDir = circle.XDirection;
            XYZ yDir = circle.YDirection;

            XYZ p0 = center + xDir.Multiply(radius);           // angle 0
            XYZ p180 = center - xDir.Multiply(radius);         // angle π
            XYZ pTop = center + yDir.Multiply(radius);         // angle π/2, used as through-point
            XYZ pBottom = center - yDir.Multiply(radius);      // angle 3π/2, used as through-point

            var half1 = Arc.Create(p0, p180, pTop);
            var half2 = Arc.Create(p180, p0, pBottom);

            yield return half1;
            yield return half2;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Connectivity grouping
        // ─────────────────────────────────────────────────────────────────────────

        private static List<List<(ElementId Id, Curve Curve)>> GroupByConnectivity(
            List<(ElementId Id, Curve Curve)> curveMap)
        {
            var groups = new List<List<(ElementId Id, Curve Curve)>>();
            var remaining = new List<(ElementId Id, Curve Curve)>(curveMap);

            while (remaining.Count > 0)
            {
                var seed = remaining[0];
                remaining.RemoveAt(0);

                var group = new List<(ElementId Id, Curve Curve)> { seed };
                bool addedAny = true;

                while (addedAny)
                {
                    addedAny = false;
                    for (int i = remaining.Count - 1; i >= 0; i--)
                    {
                        var candidate = remaining[i];
                        if (group.Any(g => CurvesTouch(g.Curve, candidate.Curve)))
                        {
                            group.Add(candidate);
                            remaining.RemoveAt(i);
                            addedAny = true;
                        }
                    }
                }

                groups.Add(group);
            }

            return groups;
        }

        private static bool CurvesTouch(Curve a, Curve b)
        {
            XYZ a0 = a.GetEndPoint(0), a1 = a.GetEndPoint(1);
            XYZ b0 = b.GetEndPoint(0), b1 = b.GetEndPoint(1);
            return PointsClose(a0, b0) || PointsClose(a0, b1) || PointsClose(a1, b0) || PointsClose(a1, b1);
        }

        private static bool PointsClose(XYZ p1, XYZ p2, double tol = ConnectivityToleranceFeet)
            => p1.DistanceTo(p2) <= tol;

        // ─────────────────────────────────────────────────────────────────────────
        // Per-group processing
        // ─────────────────────────────────────────────────────────────────────────

        private static void ProcessGroup(
            List<(ElementId Id, Curve Curve)> group,
            int serial,
            double cornerToleranceFeet,
            LoopProcessingResult result,
            Action<LogLevel, string> log)
        {
            var ids = group.Select(g => g.Id).ToList();
            var curves = group.Select(g => g.Curve).ToList();

            int arcCount = curves.Count(c => c is Arc);
            if (arcCount > 0)
                log(LogLevel.Info, $"Group {serial}: contains {arcCount} arc(s) — overlap/corner clean-up only applies to straight lines; arcs pass through as-is.");

            bool wasCornerTrimmed = false;

            // ── (b) Overlap resolution: same extend/trim-to-junction mechanism ─────
            // Detect curves lying on (near) the same path and sharing more than a
            // single endpoint touch — these are overlaps rather than clean corners.
            if (TryResolveOverlaps(curves, ids, log, out var afterOverlap))
            {
                curves = afterOverlap;
                wasCornerTrimmed = true;
            }

            // ── Build endpoint adjacency to find free endpoints ─────────────────────
            var freeEndpoints = FindFreeEndpoints(curves);

            // ── (c) Corner clean-up: extend near-miss free endpoint pairs ───────────
            if (freeEndpoints.Count > 0 && cornerToleranceFeet > 0)
            {
                if (TryCornerCleanup(curves, freeEndpoints, cornerToleranceFeet, ids, log))
                {
                    wasCornerTrimmed = true;
                    freeEndpoints = FindFreeEndpoints(curves);
                }
            }

            // ── Already/now closed? ─────────────────────────────────────────────────
            if (freeEndpoints.Count == 0 && TryOrderClosedLoop(curves, out var ordered))
            {
                var loop = new LoopCandidate
                {
                    SerialNumber = serial,
                    Curves = ordered,
                    SourceLineIds = ids,
                    WasCornerTrimmed = wasCornerTrimmed,
                    Status = wasCornerTrimmed ? LoopStatus.CornerTrimmed : LoopStatus.Ready
                };
                ComputeAreaAndOrientation(loop);
                result.ValidLoops.Add(loop);

                log(LogLevel.Info, wasCornerTrimmed
                    ? $"Group {serial}: closed after corner/overlap clean-up — {ids.Count} line(s), area {loop.AreaInternal * 0.0929:0.0} m²."
                    : $"Group {serial}: already a closed loop — {ids.Count} line(s), area {loop.AreaInternal * 0.0929:0.0} m².");
                return;
            }

            // ── (d) Auto-close: exactly 2 free endpoints ────────────────────────────
            if (freeEndpoints.Count == 2)
            {
                var connector = Line.CreateBound(freeEndpoints[0], freeEndpoints[1]);
                var closedCurves = new List<Curve>(curves) { connector };

                if (TryOrderClosedLoop(closedCurves, out var orderedClosed))
                {
                    var loop = new LoopCandidate
                    {
                        SerialNumber = serial,
                        Curves = orderedClosed,
                        SourceLineIds = ids,
                        WasAutoClosed = true,
                        WasCornerTrimmed = wasCornerTrimmed,
                        Status = LoopStatus.AutoClosed
                    };
                    ComputeAreaAndOrientation(loop);
                    result.ValidLoops.Add(loop);

                    log(LogLevel.Warning,
                        $"Group {serial}: open chain, 2 free endpoints — auto-closed with a new connector line, " +
                        $"{ids.Count} source line(s), area {loop.AreaInternal * 0.0929:0.0} m².");
                    return;
                }
            }

            // ── (e) Unresolvable — skip ──────────────────────────────────────────────
            string reason = freeEndpoints.Count == 0
                ? "could not be ordered into a valid closed loop (possible self-intersection)"
                : freeEndpoints.Count == 2
                    ? "auto-close attempted but the resulting loop was invalid"
                    : $"{freeEndpoints.Count} free endpoint(s) — not a simple open chain, cannot auto-close";

            result.SkippedGroups.Add(new SkippedGroup { LineIds = ids, Reason = reason });
            log(LogLevel.Warning, $"Group {serial}: unresolved — {reason} — skipped ({string.Join(", ", ids.Select(i => i.Value))}).");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Overlap resolution (extend/trim to junction — same mechanism as corner clean-up)
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>Finds pairs of straight lines that lie on (nearly) the same infinite
        /// line and overlap along their length, and trims/extends both back to their
        /// true mutual endpoint junction rather than discarding either one. Returns true
        /// if any overlap was resolved.</summary>
        private static bool TryResolveOverlaps(
            List<Curve> curves, List<ElementId> ids, Action<LogLevel, string> log, out List<Curve> resolved)
        {
            resolved = new List<Curve>(curves);
            bool anyResolved = false;

            for (int i = 0; i < resolved.Count; i++)
            {
                if (!(resolved[i] is Line lineA)) continue;

                for (int j = i + 1; j < resolved.Count; j++)
                {
                    if (!(resolved[j] is Line lineB)) continue;

                    if (!AreCollinear(lineA, lineB)) continue;
                    if (!SegmentsOverlap(lineA, lineB)) continue;

                    // Collinear + overlapping: merge into a single trimmed segment
                    // spanning the two extreme, non-overlapping endpoints.
                    var pts = new[] { lineA.GetEndPoint(0), lineA.GetEndPoint(1), lineB.GetEndPoint(0), lineB.GetEndPoint(1) };
                    XYZ dir = (lineA.GetEndPoint(1) - lineA.GetEndPoint(0)).Normalize();
                    XYZ origin = lineA.GetEndPoint(0);

                    var ordered = pts.OrderBy(p => (p - origin).DotProduct(dir)).ToList();
                    var merged = Line.CreateBound(ordered.First(), ordered.Last());

                    resolved[i] = merged;
                    resolved.RemoveAt(j);
                    anyResolved = true;

                    log(LogLevel.Warning,
                        $"Overlap detected between lines — extended/trimmed to a single junction segment.");

                    j--; // re-check remaining curves against the merged segment
                }
            }

            return anyResolved;
        }

        private static bool AreCollinear(Line a, Line b, double angleTolRad = 0.01, double distTolFeet = 0.05)
        {
            XYZ da = (a.GetEndPoint(1) - a.GetEndPoint(0)).Normalize();
            XYZ db = (b.GetEndPoint(1) - b.GetEndPoint(0)).Normalize();
            double angle = da.AngleTo(db);
            bool parallel = angle < angleTolRad || Math.Abs(angle - Math.PI) < angleTolRad;
            if (!parallel) return false;

            // Perpendicular distance from b's start point to line a's infinite path.
            XYZ toB = b.GetEndPoint(0) - a.GetEndPoint(0);
            double perpDist = toB.Subtract(da.Multiply(toB.DotProduct(da))).GetLength();
            return perpDist <= distTolFeet;
        }

        private static bool SegmentsOverlap(Line a, Line b)
        {
            XYZ dir = (a.GetEndPoint(1) - a.GetEndPoint(0)).Normalize();
            XYZ origin = a.GetEndPoint(0);

            double aMin = 0, aMax = a.Length;
            double b0 = (b.GetEndPoint(0) - origin).DotProduct(dir);
            double b1 = (b.GetEndPoint(1) - origin).DotProduct(dir);
            double bMin = Math.Min(b0, b1), bMax = Math.Max(b0, b1);

            // Overlap (not just endpoint touch) requires real interval intersection.
            double overlap = Math.Min(aMax, bMax) - Math.Max(aMin, bMin);
            return overlap > ConnectivityToleranceFeet;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Free-endpoint detection
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>Returns endpoints that are not shared with any other curve in the
        /// group (i.e. degree-1 vertices). A clean closed loop has zero of these.</summary>
        private static List<XYZ> FindFreeEndpoints(List<Curve> curves)
        {
            var endpointUses = new List<(XYZ Pt, int Count)>();

            var allPts = new List<XYZ>();
            foreach (var c in curves)
            {
                allPts.Add(c.GetEndPoint(0));
                allPts.Add(c.GetEndPoint(1));
            }

            var free = new List<XYZ>();
            var visited = new bool[allPts.Count];

            for (int i = 0; i < allPts.Count; i++)
            {
                if (visited[i]) continue;
                int count = 1;
                visited[i] = true;
                for (int j = i + 1; j < allPts.Count; j++)
                {
                    if (!visited[j] && PointsClose(allPts[i], allPts[j]))
                    {
                        visited[j] = true;
                        count++;
                    }
                }
                // A vertex used by exactly one curve-endpoint occurrence total across
                // the whole group is free; a proper loop vertex is shared by 2 curve-ends.
                if (count == 1) free.Add(allPts[i]);
            }

            return free;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Corner clean-up (extend near-miss free endpoints to meet)
        // ─────────────────────────────────────────────────────────────────────────

        private static bool TryCornerCleanup(
            List<Curve> curves, List<XYZ> freeEndpoints, double toleranceFeet,
            List<ElementId> ids, Action<LogLevel, string> log)
        {
            bool anyFixed = false;

            for (int i = 0; i < freeEndpoints.Count; i++)
            {
                for (int j = i + 1; j < freeEndpoints.Count; j++)
                {
                    double dist = freeEndpoints[i].DistanceTo(freeEndpoints[j]);
                    if (dist <= ConnectivityToleranceFeet || dist > toleranceFeet) continue;

                    int idxA = curves.FindIndex(c => PointsClose(c.GetEndPoint(0), freeEndpoints[i]) || PointsClose(c.GetEndPoint(1), freeEndpoints[i]));
                    int idxB = curves.FindIndex(c => PointsClose(c.GetEndPoint(0), freeEndpoints[j]) || PointsClose(c.GetEndPoint(1), freeEndpoints[j]));
                    if (idxA < 0 || idxB < 0 || idxA == idxB) continue;

                    // ── Line-to-Line: true corner extend (both curves stretched to their
                    // exact mutual intersection point — geometrically precise). ──────────
                    if (curves[idxA] is Line lineA && curves[idxB] is Line lineB)
                    {
                        if (!TryLineIntersection(lineA, lineB, out XYZ meetPt)) continue;

                        curves[idxA] = ExtendLineToPoint(lineA, freeEndpoints[i], meetPt);
                        curves[idxB] = ExtendLineToPoint(lineB, freeEndpoints[j], meetPt);
                        anyFixed = true;

                        log(LogLevel.Info,
                            $"Near-corner gap {dist * 304.8:0}mm (≤{toleranceFeet * 304.8:0}mm tolerance) between two lines — extended both to meet.");
                        continue;
                    }

                    // ── Arc-to-Arc or Arc-to-Line: bridge with a straight connector line
                    // instead of re-fitting curvature (per confirmed spec — extending an
                    // arc's curvature to close a gap is geometrically ambiguous; a short
                    // straight segment is the practical, predictable fix). ───────────────
                    var connector = Line.CreateBound(freeEndpoints[i], freeEndpoints[j]);
                    curves.Add(connector);
                    anyFixed = true;

                    bool isArcArc = curves[idxA] is Arc && curves[idxB] is Arc;
                    log(LogLevel.Info,
                        $"Near-corner gap {dist * 304.8:0}mm (≤{toleranceFeet * 304.8:0}mm tolerance) " +
                        $"between {(isArcArc ? "two arcs" : "an arc and a line")} — bridged with a straight connector segment.");
                }
            }

            return anyFixed;
        }

        private static Line ExtendLineToPoint(Line line, XYZ oldEnd, XYZ newEnd)
        {
            XYZ p0 = line.GetEndPoint(0), p1 = line.GetEndPoint(1);
            return PointsClose(p0, oldEnd) ? Line.CreateBound(newEnd, p1) : Line.CreateBound(p0, newEnd);
        }

        private static bool TryLineIntersection(Line a, Line b, out XYZ point)
        {
            point = null;
            XYZ p1 = a.GetEndPoint(0), d1 = (a.GetEndPoint(1) - a.GetEndPoint(0)).Normalize();
            XYZ p2 = b.GetEndPoint(0), d2 = (b.GetEndPoint(1) - b.GetEndPoint(0)).Normalize();

            // Solve in XY plane (detail lines are planar in the view).
            double denom = d1.X * d2.Y - d1.Y * d2.X;
            if (Math.Abs(denom) < 1e-9) return false; // parallel

            double t = ((p2.X - p1.X) * d2.Y - (p2.Y - p1.Y) * d2.X) / denom;
            point = p1 + d1.Multiply(t);
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Loop ordering / validity
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>Attempts to order a curve set into a single closed traversal (each
        /// curve's endpoint connects to the next curve's start within tolerance).
        /// Returns false if the set doesn't form exactly one simple closed loop.
        /// Handles the single-curve case explicitly: a Circle drawn with Revit's Circle
        /// tool is a single bound Arc whose start and end points coincide (Revit has no
        /// separate "circle" curve type — see Arc.IsBound) — this is already a valid
        /// closed loop on its own and must not be rejected as "too few curves."</summary>
        private static bool TryOrderClosedLoop(List<Curve> curves, out List<Curve> ordered)
        {
            ordered = new List<Curve>();
            if (curves.Count == 0) return false;

            if (curves.Count == 1)
            {
                var only = curves[0];
                if (PointsClose(only.GetEndPoint(0), only.GetEndPoint(1)))
                {
                    ordered.Add(only);
                    return true;
                }
                return false; // single open curve — not a loop
            }

            var remaining = new List<Curve>(curves);
            var current = remaining[0];
            remaining.RemoveAt(0);
            ordered.Add(current);
            XYZ startPt = current.GetEndPoint(0);
            XYZ chasePt = current.GetEndPoint(1);

            while (remaining.Count > 0)
            {
                int idx = remaining.FindIndex(c => PointsClose(c.GetEndPoint(0), chasePt) || PointsClose(c.GetEndPoint(1), chasePt));
                if (idx < 0) return false; // chain broken — not a single simple loop

                var next = remaining[idx];
                remaining.RemoveAt(idx);

                XYZ nextEnd = PointsClose(next.GetEndPoint(0), chasePt) ? next.GetEndPoint(1) : next.GetEndPoint(0);
                // Normalize direction so consecutive curves read start->end continuously.
                if (!PointsClose(next.GetEndPoint(0), chasePt))
                    next = ReverseCurve(next);

                ordered.Add(next);
                chasePt = nextEnd;
            }

            return PointsClose(chasePt, startPt);
        }

        private static Curve ReverseCurve(Curve c)
        {
            // Curve.CreateReversed() is geometrically exact for both Line and Arc —
            // reconstructing an Arc from 3 points (old approach) could flip it onto
            // the wrong side of the chord for a near-180°/circle arc, breaking loop
            // closure for anything containing a circle. CreateReversed() has no such
            // ambiguity, so it is now used uniformly for every curve type.
            return c.CreateReversed();
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Area / orientation (for nesting sort + display)
        // ─────────────────────────────────────────────────────────────────────────

        private static void ComputeAreaAndOrientation(LoopCandidate loop)
        {
            var pts = Tessellate(loop.Curves);
            double signedArea = 0;
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;

            for (int i = 0; i < pts.Count; i++)
            {
                var p0 = pts[i];
                var p1 = pts[(i + 1) % pts.Count];
                signedArea += (p0.X * p1.Y - p1.X * p0.Y);

                minX = Math.Min(minX, p0.X); maxX = Math.Max(maxX, p0.X);
                minY = Math.Min(minY, p0.Y); maxY = Math.Max(maxY, p0.Y);
            }
            signedArea *= 0.5;

            loop.AreaInternal = Math.Abs(signedArea);
            loop.IsClockwise = signedArea < 0;

            var bb = new BoundingBoxXYZ
            {
                Min = new XYZ(minX, minY, 0),
                Max = new XYZ(maxX, maxY, 0)
            };
            loop.BoundingBox = bb;
        }

        private static List<XYZ> Tessellate(IList<Curve> curves)
        {
            var pts = new List<XYZ>();
            foreach (var c in curves)
            {
                var tess = c.Tessellate();
                for (int i = 0; i < tess.Count - 1; i++) pts.Add(tess[i]);
            }
            return pts;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Containment / nesting: pair outer+next-inner alternately by depth
        // ─────────────────────────────────────────────────────────────────────────

        public static List<RoofGroup> GroupByNesting(List<LoopCandidate> validLoops, Action<LogLevel, string> log)
        {
            var groups = new List<RoofGroup>();

            // Sort largest-area first — a simple, robust proxy for "outermost first"
            // given these are architectural roof footprints (no exotic concave nesting).
            var byAreaDesc = validLoops.OrderByDescending(l => l.AreaInternal).ToList();
            var consumed = new HashSet<int>();
            int roofNumber = 1;

            for (int i = 0; i < byAreaDesc.Count; i++)
            {
                if (consumed.Contains(byAreaDesc[i].SerialNumber)) continue;
                var outer = byAreaDesc[i];
                consumed.Add(outer.SerialNumber);

                // Find loops directly nested inside 'outer' that are not already
                // nested inside some other not-yet-consumed loop smaller than outer
                // but larger than them (i.e. the immediate child level only).
                var directChildren = FindImmediateChildren(outer, byAreaDesc, consumed);

                var group = new RoofGroup { RoofNumber = roofNumber++, Outer = outer };

                foreach (var child in directChildren)
                {
                    child.Status = LoopStatus.Opening;
                    group.Openings.Add(child);
                    consumed.Add(child.SerialNumber);

                    log(LogLevel.Info,
                        $"Nesting: loop {outer.SerialNumber} contains loop {child.SerialNumber} → paired as boundary+opening (Roof {group.RoofNumber}).");

                    // Grandchildren (nested inside the opening) start fresh outer
                    // boundaries of their own — handled naturally by the outer loop
                    // continuing since they remain unconsumed.
                }

                groups.Add(group);
            }

            log(LogLevel.Info, $"Nesting analysis complete: {validLoops.Count} valid loop(s) → {groups.Count} roof(s) ({groups.Count(g => g.Openings.Count > 0)} with openings).");
            return groups;
        }

        private static List<LoopCandidate> FindImmediateChildren(
            LoopCandidate outer, List<LoopCandidate> allLoopsByAreaDesc, HashSet<int> consumed)
        {
            // Candidates: unconsumed loops fully inside 'outer' by bounding-box + point test.
            var insideOuter = allLoopsByAreaDesc
                .Where(l => l.SerialNumber != outer.SerialNumber
                            && !consumed.Contains(l.SerialNumber)
                            && IsFullyInside(l, outer))
                .OrderByDescending(l => l.AreaInternal)
                .ToList();

            // Immediate children = those not themselves inside another candidate in this set
            // (i.e. the largest ones at the top of the nesting chain directly under outer).
            var immediate = new List<LoopCandidate>();
            foreach (var candidate in insideOuter)
            {
                bool isNestedDeeper = insideOuter.Any(other =>
                    other.SerialNumber != candidate.SerialNumber && IsFullyInside(candidate, other));
                if (!isNestedDeeper) immediate.Add(candidate);
            }

            return immediate;
        }

        private static bool IsFullyInside(LoopCandidate inner, LoopCandidate outer)
        {
            var ib = inner.BoundingBox; var ob = outer.BoundingBox;
            if (ib.Min.X < ob.Min.X || ib.Min.Y < ob.Min.Y || ib.Max.X > ob.Max.X || ib.Max.Y > ob.Max.Y)
                return false;

            // Confirm with a point-in-polygon test using inner's first vertex.
            var testPt = inner.Curves[0].GetEndPoint(0);
            return PointInPolygon(testPt, Tessellate(outer.Curves));
        }

        private static bool PointInPolygon(XYZ pt, List<XYZ> poly)
        {
            bool inside = false;
            int n = poly.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                XYZ pi = poly[i], pj = poly[j];
                bool intersects = ((pi.Y > pt.Y) != (pj.Y > pt.Y)) &&
                    (pt.X < (pj.X - pi.X) * (pt.Y - pi.Y) / (pj.Y - pi.Y) + pi.X);
                if (intersects) inside = !inside;
            }
            return inside;
        }
    }
}
