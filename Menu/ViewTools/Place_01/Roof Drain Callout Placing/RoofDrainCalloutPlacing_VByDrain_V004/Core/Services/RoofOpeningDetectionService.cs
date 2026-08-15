// File: RoofOpeningDetectionService.cs
// Location: Core/Services/
//
// REWRITTEN (V004, second pass) — ported from
// Revit26_Plugin.AutoSlopeByDrain.V005.Core.Services.DrainDetectionService,
// per Rafi's confirmed decision to switch detection from raw solid/face
// geometry to the roof's Sketch profile. This is a more reliable source:
// no "guess which face is the top" step is needed (the old V004 first-pass
// FindTopFace-by-normal heuristic is removed entirely), and the ported
// TryArcBasedCircle test classifies small-radius circles correctly by
// checking the loop's real Arc/Ellipse curves (shared center/radius)
// instead of a tessellated-point count, which was unreliable for small
// circles that Revit tessellates to very few points.
//
// CONFIRMED WITH RAFI:
//   - Roof type restricted to FootPrintRoof only (Sketch-based detection
//     requires a roof with a Sketch; RoofBase without one has nothing to
//     read). The roof picker (RoofSelectionFilter) must only allow
//     FootPrintRoof going forward.
//   - "Polygon" (the ported source's name for an irregular loop) maps to
//     this tool's OpeningShape.Other.
//   - Diameter/Perimeter/Area are NOT present in the ported source (it only
//     produces Width/Height) — added here to keep OpeningItem's existing
//     DataGrid columns populated: Diameter from the real arc radius for
//     circles (not derived from tessellated perimeter), Perimeter from
//     real curve lengths, Area via the ported Shoelace helper.
//   - Outer boundary is detected (needed to correctly separate it from real
//     openings — largest-area loop in the sketch) but never returned in the
//     results list, matching this tool's existing "detect but don't list"
//     behavior.
//
// Flow:
//   1. Read the roof's dependent Sketch (FootPrintRoof only). Compute each
//      profile loop's area (Shoelace, full arc tessellation — not just
//      curve start-points, which would collapse a circular boundary to
//      near-zero area and pick the wrong loop as "outer"). Largest = outer
//      boundary (discarded from results), rest = opening candidates.
//   2. Tessellate each candidate loop to a flattened (Z=0) XYZ point list.
//   3. Deduplicate loops by bounding-box-center proximity, BEFORE
//      classification (so no duplicate work happens downstream).
//   4. Classify each surviving unique loop: Circle first (via the loop's
//      real Arc/Ellipse curves sharing a common center/radius — works
//      regardless of tessellation density), then Rectangle/Square (4
//      tessellated vertices, adjacent edges perpendicular, width≈height
//      within 5mm -> Square), else Other.
//   5. Build an OpeningItem per surviving loop, converting the Sketch's
//      CurveArray to a CurveLoop (straight segments between consecutive
//      tessellated points) so OpeningItem's existing CurveLoop-typed
//      LoopGeometry field is unchanged.

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using Revit26_Plugin.RoofDrainCalloutPlacing.VByDrain.V004.Models;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoofDrainCalloutPlacing.VByDrain.V004.Services
{
    public static class RoofOpeningDetectionService
    {
        private const double LoopDedupTolFt = 0.01; // ~3mm, matches the ported source's tolerance
        private const double SquareToleranceMm = 5.0;
        private const double CircleCenterTolFt = 5.0 / 304.8; // 5mm
        private const double CircleRadiusTolFt = 5.0 / 304.8; // 5mm

        /// <summary>
        /// Extract inner-loop openings from the roof's Sketch profile and classify each by shape.
        /// Requires a FootPrintRoof (Sketch-based); other RoofBase types return no results.
        /// </summary>
        public static List<OpeningItem> DetectOpeningsOnRoof(
            RoofBase roof,
            Document doc,
            Action<LogEntry> logCallback = null)
        {
            var openings = new List<OpeningItem>();

            try
            {
                if (roof == null || doc == null)
                {
                    logCallback?.Invoke(new LogEntry(LogLevel.Error, "Roof or document is null."));
                    return openings;
                }

                if (!(roof is FootPrintRoof))
                {
                    logCallback?.Invoke(new LogEntry(LogLevel.Error,
                        "Sketch-based opening detection requires a FootPrintRoof. " +
                        "(This should not be reachable if the roof picker is restricted to FootPrintRoof.)"));
                    return openings;
                }

                var loopCandidates = ExtractOpeningLoopsFromSketch(roof, logCallback);
                int loopsFound = loopCandidates.Count;

                var uniqueLoops = DeduplicateLoops(loopCandidates);
                int duplicatesRemoved = loopsFound - uniqueLoops.Count;

                if (duplicatesRemoved > 0)
                    logCallback?.Invoke(new LogEntry(LogLevel.Info, $"Removed {duplicatesRemoved} duplicate opening loop(s)."));

                int rejectedDegenerate = 0, rejectedException = 0;

                for (int i = 0; i < uniqueLoops.Count; i++)
                {
                    var (originalLoop, pts) = uniqueLoops[i];
                    try
                    {
                        if (pts.Count < 3)
                        {
                            rejectedDegenerate++;
                            logCallback?.Invoke(new LogEntry(LogLevel.Warning,
                                $"Loop {i + 1} rejected — only {pts.Count} boundary point(s) found (degenerate loop)."));
                            continue;
                        }

                        var opening = BuildOpeningFromLoop(originalLoop, pts, i + 1);
                        if (opening != null)
                            openings.Add(opening);
                    }
                    catch (Exception ex)
                    {
                        rejectedException++;
                        logCallback?.Invoke(new LogEntry(LogLevel.Warning, $"Loop {i + 1} rejected — {ex.Message}"));
                    }
                }

                // Sort by shape type, then by area (largest first) within each type
                openings = openings
                    .GroupBy(o => o.ShapeType)
                    .SelectMany(g => g.OrderByDescending(o => o.Area))
                    .ToList();

                logCallback?.Invoke(new LogEntry(LogLevel.Success,
                    $"{loopsFound} opening loop(s) found in sketch, {duplicatesRemoved} duplicate(s) removed, " +
                    $"{openings.Count} accepted into {openings.Select(o => o.ShapeType).Distinct().Count()} type(s) " +
                    $"({rejectedDegenerate} degenerate, {rejectedException} error(s) rejected)."));

                return openings;
            }
            catch (Exception ex)
            {
                logCallback?.Invoke(new LogEntry(LogLevel.Error, $"Detection service error: {ex.Message}"));
                return openings;
            }
        }

        // ── Step 1-2: Sketch profile → opening loop candidates ──────────────────

        /// <summary>
        /// Reads the roof's dependent Sketch, computes each profile loop's area,
        /// discards the largest (outer boundary — detected here so it's correctly
        /// excluded, but never added to the returned openings list), and returns
        /// the rest as (original CurveArray, tessellated Z=0 points) pairs. The
        /// original CurveArray is kept alongside the tessellated points so shape
        /// classification can inspect real Arc/Ellipse curve types.
        /// </summary>
        private static List<(CurveArray originalLoop, List<XYZ> pts)> ExtractOpeningLoopsFromSketch(
            RoofBase roof, Action<LogEntry> log)
        {
            var doc = roof.Document;
            var dependentIds = roof.GetDependentElements(new ElementClassFilter(typeof(Sketch)));

            foreach (ElementId id in dependentIds)
            {
                if (!(doc.GetElement(id) is Sketch sketch)) continue;

                var loops = new List<(CurveArray loop, double area)>();
                foreach (CurveArray loop in sketch.Profile)
                {
                    double area = ApproximateLoopArea(loop);
                    loops.Add((loop, area));
                }

                if (loops.Count <= 1)
                {
                    log?.Invoke(new LogEntry(LogLevel.Info, "Sketch has only one loop (outer boundary) — no openings."));
                    return new List<(CurveArray, List<XYZ>)>();
                }

                double maxArea = loops.Max(l => l.area);
                log?.Invoke(new LogEntry(LogLevel.Info,
                    $"Outer boundary identified (largest loop, {maxArea * 304.8 * 304.8:N0} mm²) — excluded from results."));

                return loops
                    .Where(l => l.area < maxArea)
                    .Select(l => (l.loop, pts: TessellateLoop(l.loop)))
                    .Where(l => l.pts.Count >= 3)
                    .ToList();
            }

            log?.Invoke(new LogEntry(LogLevel.Warning, "No Sketch element found on this roof — no openings detected."));
            return new List<(CurveArray, List<XYZ>)>();
        }

        // ── Step 3: dedup at the loop level, BEFORE classification ─────────────

        /// <summary>Removes duplicate opening loops by bounding-box-center proximity.</summary>
        private static List<(CurveArray originalLoop, List<XYZ> pts)> DeduplicateLoops(
            List<(CurveArray originalLoop, List<XYZ> pts)> loops)
        {
            var unique = new List<(CurveArray, List<XYZ>)>();
            var centers = new List<XYZ>();

            foreach (var (originalLoop, pts) in loops)
            {
                XYZ center = BoundingBoxCenter(pts);
                bool isDuplicate = centers.Any(c => c.DistanceTo(center) < LoopDedupTolFt);
                if (!isDuplicate)
                {
                    unique.Add((originalLoop, pts));
                    centers.Add(center);
                }
            }

            return unique;
        }

        // ── Step 4-5: classify + build OpeningItem ──────────────────────────────

        /// <summary>
        /// For a single opening loop: classifies its shape, computes Width/Height
        /// (bounding box), Diameter (real arc radius for circles, diagonal
        /// otherwise), Perimeter (real curve lengths), and Area (Shoelace), then
        /// builds an OpeningItem. LoopGeometry is a CurveLoop built from straight
        /// segments between consecutive tessellated points (the Sketch profile's
        /// original curve types aren't preserved through tessellation).
        /// </summary>
        private static OpeningItem BuildOpeningFromLoop(CurveArray originalLoop, List<XYZ> pts, int loopIndex)
        {
            double minX = pts.Min(p => p.X), maxX = pts.Max(p => p.X);
            double minY = pts.Min(p => p.Y), maxY = pts.Max(p => p.Y);
            double widthFt = maxX - minX;
            double heightFt = maxY - minY;
            double widthMm = widthFt * 304.8;
            double heightMm = heightFt * 304.8;

            var (shape, arcRadiusFt) = ClassifyShape(originalLoop, pts, widthMm, heightMm);

            double perimeterFt = 0;
            foreach (Curve c in originalLoop)
                perimeterFt += c.Length;
            double perimeterMm = perimeterFt * 304.8;

            double diameterMm = shape == OpeningShape.Circle && arcRadiusFt > 0
                ? 2.0 * arcRadiusFt * 304.8
                : Math.Sqrt(widthMm * widthMm + heightMm * heightMm); // diagonal, for sorting/reference

            double areaMm2 = ApproximatePolygonArea(pts) * 304.8 * 304.8;

            var center = BoundingBoxCenter(pts);
            var loopGeometry = BuildStraightCurveLoop(pts);

            return new OpeningItem(
                loopGeometry: loopGeometry,
                center: center,
                shape: shape,
                loopIdentifier: $"Loop {loopIndex}",
                width: widthMm,
                height: heightMm,
                perimeter: perimeterMm,
                diameter: diameterMm,
                area: areaMm2);
        }

        /// <summary>
        /// Rectangle: exactly 4 tessellated vertices, consecutive edges perpendicular
        ///   (further split into Square when width and height are within 5mm).
        /// Circle: tried FIRST via TryArcBasedCircle, which inspects the loop's REAL
        ///   (pre-tessellation) Arc/Ellipse curves directly — reliable regardless of
        ///   tessellation density, unlike a tessellated-point-count/stddev test which
        ///   fails for small-radius circles Revit tessellates to very few points.
        ///   Falls back to a tessellated-point stddev heuristic only if the real-curve
        ///   test was inconclusive.
        /// Other: neither test passes.
        /// Returns the classified shape plus the circle's real radius in feet (0 if
        /// not a circle or the fallback stddev path was used), so BuildOpeningFromLoop
        /// can compute an exact Diameter rather than falling back to the diagonal.
        /// </summary>
        private static (OpeningShape shape, double circleRadiusFt) ClassifyShape(
            CurveArray originalLoop, List<XYZ> pts, double widthMm, double heightMm)
        {
            if (TryArcBasedCircle(originalLoop, out double radiusFt))
                return (OpeningShape.Circle, radiusFt);

            if (pts.Count == 4)
            {
                bool isRect = true;
                for (int i = 0; i < 4; i++)
                {
                    var a = pts[i];
                    var b = pts[(i + 1) % 4];
                    var c = pts[(i + 2) % 4];
                    XYZ e1 = new XYZ(b.X - a.X, b.Y - a.Y, 0);
                    XYZ e2 = new XYZ(c.X - b.X, c.Y - b.Y, 0);
                    double dot = e1.X * e2.X + e1.Y * e2.Y;
                    if (Math.Abs(dot) > 1e-6) { isRect = false; break; }
                }

                if (isRect)
                    return (Math.Abs(widthMm - heightMm) < SquareToleranceMm ? OpeningShape.Square : OpeningShape.Rectangle, 0);

                return (OpeningShape.Other, 0);
            }

            // Fallback stddev-based circle test — only reachable if the real-curve
            // test above was inconclusive (e.g. mixed arc/line loop that isn't a
            // clean circle by curve type, but still tessellates round).
            if (pts.Count >= 12)
            {
                double minX = pts.Min(p => p.X), maxX = pts.Max(p => p.X);
                double minY = pts.Min(p => p.Y), maxY = pts.Max(p => p.Y);
                var centroid = new XYZ((minX + maxX) / 2.0, (minY + maxY) / 2.0, 0);

                double avgDist = pts.Average(p => Dist2D(p, centroid));
                if (avgDist > 1e-9)
                {
                    double stdDev = Math.Sqrt(pts.Average(p => Math.Pow(Dist2D(p, centroid) - avgDist, 2)));
                    if (stdDev / avgDist < 0.05)
                        return (OpeningShape.Circle, avgDist); // avgDist approximates radius here
                }
            }

            return (OpeningShape.Other, 0);
        }

        /// <summary>
        /// Real-curve circle test: a loop is a circle if every curve in it is an
        /// Arc (or Ellipse with equal radii — a true circle, not an oval) sharing a
        /// common center and radius within a small tolerance. Works regardless of
        /// tessellation density. Handles both the single-Arc/single-Ellipse case
        /// (a circle drawn as one closed curve) and the multi-Arc case (a circle
        /// drawn as 2+ arc segments). Outputs the shared radius in feet on success.
        /// </summary>
        private static bool TryArcBasedCircle(CurveArray originalLoop, out double radiusFt)
        {
            radiusFt = 0;
            if (originalLoop == null) return false;

            XYZ refCenter = null;
            double refRadius = -1;
            int curveCount = 0;

            foreach (Curve c in originalLoop)
            {
                curveCount++;

                XYZ center;
                double radius;

                if (c is Arc arc)
                {
                    center = arc.Center;
                    radius = arc.Radius;
                }
                else if (c is Ellipse ell)
                {
                    // Only a true circle (equal radii) counts — an oval opening
                    // should not be classified as "Circle".
                    if (Math.Abs(ell.RadiusX - ell.RadiusY) > CircleRadiusTolFt) return false;
                    center = ell.Center;
                    radius = ell.RadiusX;
                }
                else
                {
                    // Any Line (or other non-circular curve type) in the loop means
                    // this is not a pure circle.
                    return false;
                }

                if (refCenter == null)
                {
                    refCenter = center;
                    refRadius = radius;
                }
                else if (center.DistanceTo(refCenter) > CircleCenterTolFt || Math.Abs(radius - refRadius) > CircleRadiusTolFt)
                {
                    return false; // curves don't share a common center/radius
                }
            }

            if (curveCount == 0) return false;

            radiusFt = refRadius;
            return true;
        }

        // ── Geometry primitives (ported from SketchGeometryHelper) ─────────────

        /// <summary>
        /// Tessellates a closed sketch-profile loop (lines kept as endpoints, curves
        /// tessellated) and flattens it to Z=0, de-duplicating adjacent points within
        /// snap tolerance.
        /// </summary>
        private static List<XYZ> TessellateLoop(CurveArray loop)
        {
            var pts = new List<XYZ>();
            foreach (Curve c in loop) AppendCurve(c, pts);
            return Flatten(pts);
        }

        private static void AppendCurve(Curve c, List<XYZ> pts)
        {
            if (c is Line)
                pts.Add(c.GetEndPoint(0));
            else
            {
                IList<XYZ> tess = c.Tessellate();
                for (int i = 0; i < tess.Count - 1; i++) pts.Add(tess[i]);
            }
        }

        private static List<XYZ> Flatten(List<XYZ> pts)
        {
            const double snapTol = 1e-6;
            var flat = new List<XYZ>(pts.Count);
            XYZ prev = null;
            foreach (var p in pts)
            {
                var fp = new XYZ(p.X, p.Y, 0);
                if (prev != null && fp.DistanceTo(prev) < snapTol) continue;
                flat.Add(fp);
                prev = fp;
            }
            return flat;
        }

        /// <summary>
        /// Approximate planar area of a CurveArray loop (Shoelace formula), using
        /// the SAME arc tessellation as TessellateLoop. Uses full tessellation
        /// (not just each curve's start point) so a circular boundary/opening
        /// measures its true footprint rather than collapsing to near-zero area.
        /// </summary>
        private static double ApproximateLoopArea(CurveArray loop)
        {
            return ApproximatePolygonArea(TessellateLoop(loop));
        }

        /// <summary>Shoelace-formula area of a flattened (Z=0) polygon point list, in ft².</summary>
        private static double ApproximatePolygonArea(List<XYZ> pts)
        {
            int n = pts.Count;
            if (n < 3) return 0;
            double area = 0;
            for (int i = 0; i < n; i++)
            {
                var a = pts[i]; var b = pts[(i + 1) % n];
                area += a.X * b.Y - b.X * a.Y;
            }
            return Math.Abs(area) / 2.0;
        }

        /// <summary>2D (XY-plane) distance between two points; Z is ignored.</summary>
        private static double Dist2D(XYZ a, XYZ b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static XYZ BoundingBoxCenter(List<XYZ> pts)
        {
            double minX = pts.Min(p => p.X), maxX = pts.Max(p => p.X);
            double minY = pts.Min(p => p.Y), maxY = pts.Max(p => p.Y);
            return new XYZ((minX + maxX) / 2.0, (minY + maxY) / 2.0, 0);
        }

        /// <summary>Builds a closed CurveLoop of straight segments connecting consecutive tessellated loop points.</summary>
        private static CurveLoop BuildStraightCurveLoop(List<XYZ> pts)
        {
            var curveLoop = new CurveLoop();
            for (int i = 0; i < pts.Count; i++)
            {
                XYZ a = pts[i];
                XYZ b = pts[(i + 1) % pts.Count];
                if (a.DistanceTo(b) < 0.001) continue; // skip degenerate zero-length segments
                curveLoop.Append(Line.CreateBound(a, b));
            }
            return curveLoop;
        }
    }
}
